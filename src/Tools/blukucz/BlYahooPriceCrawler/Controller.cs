using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using CsvHelper;
using CsvHelper.Configuration.Attributes;
using SqCommon;
using YahooFinanceApi;

namespace BlYahooPriceCrawler
{
    public class YFRecord
    {
        public string Date { get; set; } = string.Empty;
        public float AdjClose { get; set; }
        public float Close { get; set; }
        public float Open { get; set; }
        public float High { get; set; }
        public float Low { get; set; }
    }

    public class TickerMembers
    {
        public string Ticker { get; set; } = string.Empty;
        // public string UniverseOrFlag { get; set; } = string.Empty; // for future use
    }

    public enum RecommendationType
    {
        [Name("STRONG BUY")]
        StrongBuy,
        [Name("BUY")]
        Buy,
        [Name("SELL")]
        Sell,
        [Name("STRONG SELL")]
        StrongSell
    }

    public class Recommendation
    {
        [Name("id")]
        public int Id { get; set; } = int.MinValue;
        [Name("ticker")]
        public string Ticker { get; set; } = string.Empty;
        [Name("date")]
        public string Date { get; set; } = string.Empty;
        [Name("type")]
        public RecommendationType Type { get; set; }
    }

    public class RecommendationsFromCsv
    {
        public required Recommendation[] Recommendations { get; set; }
        public required string[] UniqueTickers { get; set; }
    }

    public class PerformanceResult
    {
        public int Id { get; set; }
        public required string Ticker { get; set; }
        public required string Date { get; set; }
        public Dictionary<int, float> FuturePerformances { get; set; } = [];
        public Dictionary<int, float> FutureSpyPerformances { get; set; } = [];
        public RecommendationType Type { get; set; }
        public int StopLossDay { get; set; } = 0;
    }

    class Controller
    {
        // static public Controller g_controller = new(); use this if you need to store persistent data in m_data fields between function calls

        // Reads the ticker universe from a CSV file
        public static string[] ReadUniverseTickers(string p_tickerFileName /*, int p_universeId = 2 */)
        {
            using StreamReader reader = new(p_tickerFileName);
            using CsvReader csv = new(reader, CultureInfo.InvariantCulture);
            List<TickerMembers> tickerMembers = csv.GetRecords<TickerMembers>().ToList();
            string[] universeTickers = tickerMembers.Select(r => r.Ticker).ToArray();

            return universeTickers;
        }

        // Downloads Yahoo Finance data to CSV files for the given tickers
        public static async void DownloadYFtoCsv(string p_tickerFileName, DateTime p_expectedHistoryStartDateET, string p_targetFolder, bool p_unsafeFlag = false)
        {
            string[] universeTickers = [.. ReadUniverseTickers(p_tickerFileName), "SPY"];
            Console.WriteLine($"Number of tickers: {universeTickers.Length}");
            foreach (string ticker in universeTickers)
            {
                // Fetch historical data from Yahoo Finance: YF Open/Close/High/Low prices are correctly split adjusted (except ATER on 2024-07-11), and the AdjClose is also dividend adjusted.
                var histResult = await HistPrice.g_HistPrice.GetHistAsync(ticker, HpDataNeed.AdjClose | HpDataNeed.OHLCV, p_expectedHistoryStartDateET, DateTime.Now);

                if (histResult.ErrorStr != null || histResult.Dates == null || histResult.AdjCloses == null || histResult.Closes == null || histResult.Opens == null || histResult.Highs == null || histResult.Lows == null)
                {
                    Console.WriteLine($"Skipping ticker '{ticker}' because of an error: {histResult.ErrorStr}. Remove it from tickerFile and recommendationFile.");
                    continue;
                }

                // Map the historical data to YFRecord objects
                YFRecord[] yfRecords = histResult.Dates.Select((date, i) => new YFRecord
                {
                    Date = Utils.Date2hYYYYMMDD(date.Date),
                    AdjClose = float.IsNaN(histResult.AdjCloses[i]) ? float.NaN : (float)Math.Round(histResult.AdjCloses[i], 4),
                    Close = float.IsNaN(histResult.Closes[i]) ? float.NaN : (float)Math.Round(histResult.Closes[i], 4),
                    Open = float.IsNaN(histResult.Opens[i]) ? float.NaN : (float)Math.Round(histResult.Opens[i], 4),
                    High = float.IsNaN(histResult.Highs[i]) ? float.NaN : (float)Math.Round(histResult.Highs[i], 4),
                    Low = float.IsNaN(histResult.Lows[i]) ? float.NaN : (float)Math.Round(histResult.Lows[i], 4)
                }).ToArray();

                // Checking for significant price changes that could be YF bug. By default singinificant changes are NOT allowed. Except for a very few tickers mentioned here.
                string[] allowedSignificantChangeTickers = ["ADN", "ARVLF", "CANOQ", "EXPRQ", "FFIE", "FSRNQ", "FTCHQ", "GCT", "GEVO", "SAVE", "GTII", "HMFAF", "MTC", "NVTAQ", "OM", "RADCQ", "STIXF", "VFS", "WEWKQ"]; // Significant changes are checked manually.
                if (!p_unsafeFlag && !allowedSignificantChangeTickers.Contains(ticker)) // Check if the prices are continuous. If there is a discontinuity (e.g., missing split), then stop and do not write the file. Except if we are in unsafe mode.
                {
                    bool hasSignificantChange = false;
                    string problematicDate = string.Empty;
                    for (int i = 1; i < yfRecords.Length; i++)
                    {
                        float prevAdjClose = yfRecords[i - 1].AdjClose;
                        float currAdjClose = yfRecords[i].AdjClose;
                        float dailyPctChg = (currAdjClose - prevAdjClose) / prevAdjClose;
                        if (dailyPctChg >= 2 || dailyPctChg <= - 2.0 / 3.0)
                        {
                            hasSignificantChange = true;
                            problematicDate = yfRecords[i].Date;
                            break;
                        }
                    }

                    if (hasSignificantChange)
                    {
                        Console.WriteLine($"Warning: Skipping ticker '{ticker}' due to significant daily change on {problematicDate}.");
                        continue;
                    }
                }

                // Write the records to a CSV file
                using StreamWriter writer = new($"{p_targetFolder}{ticker}.csv");
                using CsvWriter csv = new(writer, CultureInfo.InvariantCulture);
                csv.WriteRecords(yfRecords);
                Console.WriteLine($"{ticker} OK");
            }
        }

        // Reads recommendations from a CSV file
        public static RecommendationsFromCsv ReadRecommendationsCsv(string p_recommFileName)
        {
            using StreamReader reader = new(p_recommFileName);
            using CsvReader csv = new(reader, CultureInfo.InvariantCulture);
            Recommendation[] recommRecords = csv.GetRecords<Recommendation>().ToList().ToArray();

            // Extract unique tickers from the recommendations
            string[] uniqueTickers = recommRecords.Select(r => r.Ticker).Distinct().ToArray();

            return new RecommendationsFromCsv
            {
                Recommendations = recommRecords,
                UniqueTickers = uniqueTickers
            };
        }

        // Reads Yahoo Finance data from CSV files for the given tickers
        private static Dictionary<string, List<YFRecord>> ReadYahooCsvFiles(string[] p_recommendedTickers, string p_targetFolder)
        {
            Dictionary<string, List<YFRecord>> yfDataFromCsv = [];
            foreach (string ticker in p_recommendedTickers)
            {
                try
                {
                    using StreamReader reader = new($"{p_targetFolder}{ticker}.csv");
                    using CsvReader csv = new(reader, CultureInfo.InvariantCulture);
                    List<YFRecord> records = csv.GetRecords<YFRecord>().ToList();
                    yfDataFromCsv.Add(ticker, records);
                }
                catch (FileNotFoundException)
                {
                    Console.WriteLine($"File not found: {ticker}.csv. Skipping to the next ticker.");
                    continue;
                }
            }

            return yfDataFromCsv;
        }

        // Calculates the performance of a recommendation over various future periods
        private static PerformanceResult CalculatePerformance(List<YFRecord> p_priceRecords, List<YFRecord>? p_spyRecords, Recommendation p_recommendation, int[] p_nDayinFuture, float p_stopLossPercentage, bool p_useMOC)
        {
            PerformanceResult performanceResult = new()
            {
                Id = p_recommendation.Id,
                Ticker = p_recommendation.Ticker,
                Date = p_recommendation.Date,
                Type = p_recommendation.Type
            };

            // Find the record corresponding to the recommendation date
            YFRecord? startRecord = p_priceRecords.FirstOrDefault(r => string.Compare(r.Date, p_recommendation.Date) >= 0);

            if (startRecord == null)
                return performanceResult;

            int startRecordIndex = p_priceRecords.IndexOf(startRecord);
            float startPrice = p_priceRecords[startRecordIndex].AdjClose;

            // Find the corresponding SPY record for the recommendation date
            YFRecord? spyStartRecord = null;
            int spyStartRecordIndex = -1;
            float spyStartPrice = float.NaN;
            if (p_spyRecords != null)
            {
                spyStartRecord = p_spyRecords.FirstOrDefault(r => string.Compare(r.Date, p_recommendation.Date) >= 0);
                if (spyStartRecord != null)
                {
                    spyStartRecordIndex = p_spyRecords.IndexOf(spyStartRecord);
                    spyStartPrice = spyStartRecord.AdjClose;
                }
            }

            // Calculate performance for each period in the future
            for (int i = 0; i < p_nDayinFuture.Length; i++)
            {
                int nDay = p_nDayinFuture[i];
                if (startRecordIndex + nDay >= p_priceRecords.Count || startRecordIndex + nDay < 0)
                {
                    performanceResult.FuturePerformances[nDay] = float.NaN;
                }
                else
                {
                    float endPrice = p_priceRecords[startRecordIndex + nDay].AdjClose;

                    for (int j = startRecordIndex + 1; j <= startRecordIndex + nDay; j++)
                    {
                        YFRecord record = p_priceRecords[j];

                        if (p_useMOC)
                        {
                            // Using MOC (Close price)
                            if (p_recommendation.Type == RecommendationType.Buy || p_recommendation.Type == RecommendationType.StrongBuy)
                            {
                                if (record.Close <= startPrice * (1 - p_stopLossPercentage))
                                {
                                    endPrice = record.Close;
                                    performanceResult.StopLossDay = j - startRecordIndex;
                                    break;
                                }
                            }
                            else if (p_recommendation.Type == RecommendationType.Sell || p_recommendation.Type == RecommendationType.StrongSell)
                            {
                                if (record.Close >= startPrice * (1 + p_stopLossPercentage))
                                {
                                    endPrice = record.Close;
                                    performanceResult.StopLossDay = j - startRecordIndex;
                                    break;
                                }
                            }
                        }
                        else
                        {
                            if (p_recommendation.Type == RecommendationType.Buy || p_recommendation.Type == RecommendationType.StrongBuy)
                            {
                                if (record.Open <= startPrice * (1 - p_stopLossPercentage))
                                {
                                    endPrice = record.Open;
                                    performanceResult.StopLossDay = j - startRecordIndex;
                                    break;
                                }
                                else if (record.Low <= startPrice * (1 - p_stopLossPercentage))
                                {
                                    endPrice = startPrice * (1 - p_stopLossPercentage);
                                    performanceResult.StopLossDay = j - startRecordIndex;
                                    break;
                                }
                            }
                            else if (p_recommendation.Type == RecommendationType.Sell || p_recommendation.Type == RecommendationType.StrongSell)
                            {
                                if (record.Open >= startPrice * (1 + p_stopLossPercentage))
                                {
                                    endPrice = record.Open;
                                    performanceResult.StopLossDay = j - startRecordIndex;
                                    break;
                                }
                                else if (record.High >= startPrice * (1 + p_stopLossPercentage))
                                {
                                    endPrice = startPrice * (1 + p_stopLossPercentage);
                                    performanceResult.StopLossDay = j - startRecordIndex;
                                    break;
                                }
                            }
                        }
                    }
                    float nDayPerformance = 0f;
                    if (nDay > 0)
                        nDayPerformance = (endPrice - startPrice) / startPrice;
                    else
                        nDayPerformance = (startPrice - endPrice) / endPrice;
                    performanceResult.FuturePerformances[nDay] = nDayPerformance;

                    if (p_spyRecords == null || spyStartRecord == null)
                        performanceResult.FutureSpyPerformances[nDay] = float.NaN;
                    else
                    {
                        if (spyStartRecordIndex + nDay >= p_spyRecords.Count)
                            performanceResult.FutureSpyPerformances[nDay] = float.NaN;
                        else
                        {
                            float spyEndPrice = p_spyRecords[spyStartRecordIndex + nDay].AdjClose;
                            float spyNDayPerformance = 0f;
                            if (nDay > 0)
                                spyNDayPerformance = (spyEndPrice - spyStartPrice) / spyStartPrice;
                            else
                                spyNDayPerformance = (spyStartPrice - spyEndPrice) / spyEndPrice;
                            performanceResult.FutureSpyPerformances[nDay] = spyNDayPerformance;
                        }
                    }
                }
            }

            return performanceResult;
        }

        // Calculates performances for all recommendations
        public static List<PerformanceResult> CalculatePerformances(RecommendationsFromCsv p_recommendations, Dictionary<string, List<YFRecord>> p_yfData, int[] p_nDayinFuture, float p_stopLossPercentage, bool p_useMOC)
        {
            List<PerformanceResult> results = [];

            // Load SPY data
            List<YFRecord>? spyRecords = p_yfData.TryGetValue("SPY", out List<YFRecord>? valueSpy) ? valueSpy : null;

            foreach (Recommendation recommendation in p_recommendations.Recommendations)
            {
                List<YFRecord>? tickerRecords = p_yfData.TryGetValue(recommendation.Ticker, out List<YFRecord>? value) ? value : null;

                if (tickerRecords != null)
                {
                    PerformanceResult performanceResult = CalculatePerformance(tickerRecords, spyRecords, recommendation, p_nDayinFuture, p_stopLossPercentage, p_useMOC);
                    results.Add(performanceResult);
                }
                else
                {
                    PerformanceResult performanceResult = new()
                    {
                        Id = recommendation.Id,
                        Ticker = recommendation.Ticker,
                        Date = recommendation.Date,
                        Type = recommendation.Type,
                        FuturePerformances = p_nDayinFuture.ToDictionary(period => period, period => float.NaN),
                        FutureSpyPerformances = p_nDayinFuture.ToDictionary(period => period, period => float.NaN),
                        StopLossDay = 0
                    };
                    results.Add(performanceResult);
                }
            }

            return results;
        }

        // Writes performance results to a CSV file
        private static void WritePerformanceResultsToCsv(string p_filePath, List<PerformanceResult> p_performanceResults, int[] p_periods)
        {
            using StreamWriter writer = new(p_filePath);
            using CsvWriter csv = new(writer, CultureInfo.InvariantCulture);

            // Write CSV header
            csv.WriteField("Id");
            csv.WriteField("Ticker");
            csv.WriteField("Date");
            csv.WriteField("Type");
            csv.WriteField("StopLossDay");
            foreach (int period in p_periods)
                csv.WriteField($"Perf_{period}d");
            foreach (int period in p_periods)
                csv.WriteField($"SpyPerf_{period}d");
            csv.NextRecord();

            // Write performance results
            foreach (PerformanceResult result in p_performanceResults)
            {
                csv.WriteField(result.Id);
                csv.WriteField(result.Ticker);
                csv.WriteField(result.Date);
                csv.WriteField(result.Type.ToString());
                csv.WriteField(result.StopLossDay);
                foreach (int period in p_periods)
                {
                    if (result.FuturePerformances.TryGetValue(period, out float value))
                        csv.WriteField(value);
                    else
                        csv.WriteField(float.NaN);
                }
                foreach (int period in p_periods)
                {
                    if (result.FutureSpyPerformances.TryGetValue(period, out float spyValue))
                        csv.WriteField(spyValue);
                    else
                        csv.WriteField(float.NaN);
                }
                csv.NextRecord();
            }
        }

        // Main analysis function to process recommendations and calculate performances
        public static void RecommendationPerformanceAnalyser()
        {
            string recommendationFile = "D:/Temp/SATopAnalystsData.csv";
            RecommendationsFromCsv recommendationsFromCsv = ReadRecommendationsCsv(recommendationFile);

            string[] tickers = [.. recommendationsFromCsv.UniqueTickers, "SPY"];
            Dictionary<string, List<YFRecord>> yfData = ReadYahooCsvFiles(tickers, "D:/Temp/YFHist/");

            // int[] p_nDayinFuture = [-252, -189, -126, -63, -21, 3, 5, 10, 21, 42, 63, 84, 105, 126, 189];
            int[] p_nDayinFuture = [-252, -189, -126, -63, -21, -15, -10, -5, 1, 2, 3, 4, 5, 10, 15, 21, 42, 63, 84, 105, 126, 189, 252];
            float stopLossPercentage = 99999f; // Use a big number (e.g. 9999) to avoid stop-loss.
            bool useMOC = true;
            List<PerformanceResult> performances = CalculatePerformances(recommendationsFromCsv, yfData, p_nDayinFuture, stopLossPercentage, useMOC);

            string outputCsvFile = "D:/Temp/recommendationResult.csv";
            WritePerformanceResultsToCsv(outputCsvFile, performances, p_nDayinFuture);
        }

        public static void SAQuantRatingScoreCsvMerge()
        {
            string inputDirectory = @"d:\Temp\SAQR\";
            string qrScoresFile = @"d:\Temp\MergedSAQRScores.csv";
            string priceFile = @"d:\Temp\MergedSAQRPrices.csv";
            string valuationFile = @"d:\Temp\MergedSAQRValuation.csv";
            string growthFile = @"d:\Temp\MergedSAQRGrowth.csv";
            string profitabilityFile = @"d:\Temp\MergedSAQRProfitability.csv";
            string momentumFile = @"d:\Temp\MergedSAQRMomentum.csv";
            string epsRevFile = @"d:\Temp\MergedSAQREpsRev.csv";

            // Dictionary to store tickers and their date-related data
            Dictionary<string, List<(string Date,
                                     double QuantScore,
                                     double Price,
                                     string Valuation,
                                     string Growth,
                                     string Profitability,
                                     string Momentum,
                                     string EpsRev)>> tickerData = [];

            // Read all files from the directory
            foreach (string filePath in Directory.GetFiles(inputDirectory, "*.csv"))
            {
                string fileName = Path.GetFileNameWithoutExtension(filePath);
                string ticker = fileName.Split('_')[0]; // Extract ticker from file name

                if (tickerData.ContainsKey(ticker))
                    continue; // Skip if the ticker is already added

                tickerData[ticker] = [];

                // Read the file line by line
                foreach (string? line in File.ReadLines(filePath).Skip(1)) // Skip header
                {
                    if (string.IsNullOrWhiteSpace(line))
                        continue;

                    string[] columns = line.Split(',');

                    // Skip lines that are too short or contain only empty fields (e.g. ",,,,,,,,")
                    if (columns.Length < 4 || columns.All(string.IsNullOrWhiteSpace))
                        continue;

                    // Date (MM/dd/yyyy -> yyyy-MM-dd)
                    string date = DateTime.ParseExact(columns[0], "MM/dd/yyyy", CultureInfo.InvariantCulture)
                                         .ToString("yyyy-MM-dd");

                    // Price
                    double price = 0;
                    if (columns.Length >= 2 && double.TryParse(columns[1], out double p))
                        price = p;

                    // Quant Rating (text) and Quant Score (numeric or 99 if NOT COVERED)
                    string quantRating = columns.Length >= 3 ? columns[2].Trim() : string.Empty;

                    if (columns.Length >= 4)
                    {
                        if (string.IsNullOrWhiteSpace(columns[3]) && quantRating == "NOT COVERED")
                            columns[3] = "99";
                    }

                    double quantScore = 0;
                    if (columns.Length >= 4 && double.TryParse(columns[3], out double qs))
                        quantScore = qs;

                    // Letter grades – safely read, default to empty if missing
                    string valuation = columns.Length >= 5 ? columns[4].Trim() : string.Empty;
                    string growth = columns.Length >= 6 ? columns[5].Trim() : string.Empty;
                    string profitability = columns.Length >= 7 ? columns[6].Trim() : string.Empty;
                    string momentum = columns.Length >= 8 ? columns[7].Trim() : string.Empty;
                    string epsRev = columns.Length >= 9 ? columns[8].Trim() : string.Empty;

                    tickerData[ticker].Add((date,
                                            quantScore,
                                            price,
                                            valuation,
                                            growth,
                                            profitability,
                                            momentum,
                                            epsRev));
                }
            }

            // Aggregate all unique dates
            List<string> allDates = [.. tickerData
        .SelectMany(kvp => kvp.Value.Select(pair => pair.Date))
        .Distinct()
        .OrderBy(date => date)];

            // Prepare ordered ticker list once
            List<string> orderedTickers = tickerData.Keys.OrderBy(t => t).ToList();

            // Write the output CSVs
            using StreamWriter writerScores = new(qrScoresFile);
            using StreamWriter writerPrices = new(priceFile);
            using StreamWriter writerValuation = new(valuationFile);
            using StreamWriter writerGrowth = new(growthFile);
            using StreamWriter writerProfitability = new(profitabilityFile);
            using StreamWriter writerMomentum = new(momentumFile);
            using StreamWriter writerEpsRev = new(epsRevFile);
            {
                // Headers
                string header = "Date," + string.Join(",", orderedTickers);
                writerScores.WriteLine(header);
                writerPrices.WriteLine(header);
                writerValuation.WriteLine(header);
                writerGrowth.WriteLine(header);
                writerProfitability.WriteLine(header);
                writerMomentum.WriteLine(header);
                writerEpsRev.WriteLine(header);

                // Rows
                foreach (string date in allDates)
                {
                    List<string> rowScores = [date];
                    List<string> rowPrices = [date];
                    List<string> rowValuation = [date];
                    List<string> rowGrowth = [date];
                    List<string> rowProfitability = [date];
                    List<string> rowMomentum = [date];
                    List<string> rowEpsRev = [date];

                    foreach (string ticker in orderedTickers)
                    {
                        var entry = tickerData[ticker].FirstOrDefault(pair => pair.Date == date);

                        // Quant Score
                        rowScores.Add(entry.QuantScore > 0 ? entry.QuantScore.ToString("F2") : "");

                        // Price
                        rowPrices.Add(entry.Price > 0 ? entry.Price.ToString("F2") : "");

                        // Letter grades – directly write the letters (A+, B-, etc.) or empty
                        rowValuation.Add(!string.IsNullOrEmpty(entry.Valuation) ? entry.Valuation : "");
                        rowGrowth.Add(!string.IsNullOrEmpty(entry.Growth) ? entry.Growth : "");
                        rowProfitability.Add(!string.IsNullOrEmpty(entry.Profitability) ? entry.Profitability : "");
                        rowMomentum.Add(!string.IsNullOrEmpty(entry.Momentum) ? entry.Momentum : "");
                        rowEpsRev.Add(!string.IsNullOrEmpty(entry.EpsRev) ? entry.EpsRev : "");
                    }

                    writerScores.WriteLine(string.Join(",", rowScores));
                    writerPrices.WriteLine(string.Join(",", rowPrices));
                    writerValuation.WriteLine(string.Join(",", rowValuation));
                    writerGrowth.WriteLine(string.Join(",", rowGrowth));
                    writerProfitability.WriteLine(string.Join(",", rowProfitability));
                    writerMomentum.WriteLine(string.Join(",", rowMomentum));
                    writerEpsRev.WriteLine(string.Join(",", rowEpsRev));
                }
            }
        }


        public static void SteveCressRecommendationQRs()
        {
            string aggregatedFile = @"d:\Temp\AggregatedSAQRScores.csv";
            string recommendationFile = @"d:\Temp\SteveCressRecommendationQRs.csv";
            string outputFile = @"d:\Temp\SteveCressRecommendationQRs_WithScores.csv";

            // Step 1: Load AggregatedScores.csv into a structured dictionary
            Dictionary<string, SortedDictionary<DateTime, string>> aggregatedData = LoadAggregatedData(aggregatedFile);

            // Step 2: Process SteveCressRecommendationQRs.csv
            List<string> outputLines = [];

            foreach (string line in File.ReadLines(recommendationFile))
            {
                string[] columns = line.Split(',');
                if (columns.Length < 2) continue;

                string ticker = columns[0];
                string date = columns[1];

                string quantScore = GetScoreForDate(aggregatedData, ticker, date);
                outputLines.Add($"{ticker},{date},{quantScore}");
            }

            // Step 3: Write the output
            File.WriteAllLines(outputFile, outputLines);
        }

        static Dictionary<string, SortedDictionary<DateTime, string>> LoadAggregatedData(string filePath)
        {
            Dictionary<string, SortedDictionary<DateTime, string>> result = [];

            List<string> lines = File.ReadLines(filePath).ToList();
            List<string> headers = lines[0].Split(',').Skip(1).ToList(); // Skip "Date" column

            foreach (string? line in lines.Skip(1))
            {
                string[] columns = line.Split(',');
                DateTime date = DateTime.ParseExact(columns[0], "yyyy-MM-dd", CultureInfo.InvariantCulture);

                for (int i = 1; i < columns.Length; i++)
                {
                    string ticker = headers[i - 1];
                    string score = columns[i];

                    // Add or update the ticker's data
                    if (!result.ContainsKey(ticker))
                        result[ticker] = [];

                    // Add the date-score pair
                    result[ticker][date] = score;
                }
            }

            return result;
        }

        static string GetScoreForDate(Dictionary<string, SortedDictionary<DateTime, string>> data, string ticker, string dateStr)
        {
            if (!data.ContainsKey(ticker))
                return ""; // Ticker not found

            SortedDictionary<DateTime, string> tickerData = data[ticker];
            DateTime date = DateTime.ParseExact(dateStr, "yyyy-MM-dd", CultureInfo.InvariantCulture);

            // Exact match
            if (tickerData.ContainsKey(date))
                return tickerData[date];

            // Find the closest earlier date
            List<DateTime> previousDates = tickerData.Keys.Where(d => d <= date).ToList();
            if (previousDates.Any())
                return tickerData[previousDates.Last()];

            return ""; // No previous data available
        }

    }
}
