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
                 IReadOnlyList<Candle?>? history;
                try
                {
                    history = await Yahoo.GetHistoricalAsync(ticker, p_expectedHistoryStartDateET, DateTime.Now, Period.Daily);
                }
                catch (System.Exception e)
                {
                    Console.WriteLine($"Skipping ticker '{ticker}' because of Exception, Msg: {e.Message}. Remove it from tickerFile and recommendationFile.");
                    continue;
                }

                // Map the historical data to YFRecord objects
                YFRecord[] yfRecords = history.Select(r => new YFRecord()
                {
                    Date = Utils.Date2hYYYYMMDD(r!.DateTime),
                    AdjClose = RowExtension.IsEmptyRow(r!) ? float.NaN : (float)Math.Round(r!.AdjustedClose, 4),
                    Close = RowExtension.IsEmptyRow(r!) ? float.NaN : (float)Math.Round(r!.Close, 4),
                    Open = RowExtension.IsEmptyRow(r!) ? float.NaN : (float)Math.Round(r!.Open, 4),
                    High = RowExtension.IsEmptyRow(r!) ? float.NaN : (float)Math.Round(r!.High, 4),
                    Low = RowExtension.IsEmptyRow(r!) ? float.NaN : (float)Math.Round(r!.Low, 4)
                }).ToArray();

                // Checking for significant price changes that could be YF bug. By default singinificant changes are NOT allowed. Except for a very few tickers mentioned here.
                string[] allowedSignificantChangeTickers = ["ARVLF", "CANOQ", "EXPRQ", "FFIE", "FSRNQ", "FTCHQ", "GTII", "HMFAF", "MTC", "NVTAQ", "OM", "RADCQ", "STIXF", "VFS", "WEWKQ"]; // Significant changes are checked manually.
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
                if (startRecordIndex + nDay >= p_priceRecords.Count)
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
                    float nDayPerformance = (endPrice - startPrice) / startPrice;
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
                            float spyNDayPerformance = (spyEndPrice - spyStartPrice) / spyStartPrice;
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

            int[] p_nDayinFuture = [3, 5, 10, 21, 42, 63, 84, 105, 126, 189];
            float stopLossPercentage = 0.5f; // Use a big number (e.g. 9999) to avoid stop-loss.
            bool useMOC = true;
            List<PerformanceResult> performances = CalculatePerformances(recommendationsFromCsv, yfData, p_nDayinFuture, stopLossPercentage, useMOC);

            string outputCsvFile = "D:/Temp/recommendationResult.csv";
            WritePerformanceResultsToCsv(outputCsvFile, performances, p_nDayinFuture);
        }
    }
}
