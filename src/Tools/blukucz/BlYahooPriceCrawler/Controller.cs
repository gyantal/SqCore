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
    }

    public class TickerMembers
    {
        public string Ticker { get; set; } = string.Empty;
        // public string UniverseOrFlag { get; set; } = string.Empty; // for future use
    }

    public class Recommendation
    {
        [Name("id")]
        public int Id { get; set; } = int.MinValue;
        [Name("ticker")]
        public string Ticker { get; set; } = string.Empty;
        [Name("date")]
        public string Date { get; set; } = string.Empty;
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
        public static async void DownloadYFtoCsv(string p_tickerFileName, DateTime p_expectedHistoryStartDateET, string p_targetFolder)
        {
            string[] universeTickers = ReadUniverseTickers(p_tickerFileName);
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
                    Close = RowExtension.IsEmptyRow(r!) ? float.NaN : (float)Math.Round(r!.Close, 4)
                }).ToArray();

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
                using StreamReader reader = new($"{p_targetFolder}{ticker}.csv");
                using CsvReader csv = new(reader, CultureInfo.InvariantCulture);
                List<YFRecord> records = csv.GetRecords<YFRecord>().ToList();
                yfDataFromCsv.Add(ticker, records);
            }

            return yfDataFromCsv;
        }

        // Calculates the performance of a recommendation over various future periods
        private static PerformanceResult CalculatePerformance(List<YFRecord> p_priceRecords, Recommendation p_recommendation, int[] p_nDayinFuture)
        {
            PerformanceResult performanceResult = new()
            {
                Id = p_recommendation.Id,
                Ticker = p_recommendation.Ticker,
                Date = p_recommendation.Date
            };

            // Find the record corresponding to the recommendation date
            YFRecord? startRecord = p_priceRecords.FirstOrDefault(r => string.Compare(r.Date, p_recommendation.Date) >= 0);

            if (startRecord == null)
                return performanceResult;

            int startRecordIndex = p_priceRecords.IndexOf(startRecord);
            float startPrice = p_priceRecords[startRecordIndex].AdjClose;

            // Calculate performance for each period in the future
            for (int i = 0; i < p_nDayinFuture.Length; i++)
            {
                int nDay = p_nDayinFuture[i];
                if (startRecordIndex + nDay >= p_priceRecords.Count)
                    performanceResult.FuturePerformances[nDay] = float.NaN;
                else
                {
                    float endPrice = p_priceRecords[startRecordIndex + nDay].AdjClose;
                    float nDayPerformance = (endPrice - startPrice) / startPrice;
                    performanceResult.FuturePerformances[nDay] = nDayPerformance;
                }
            }

            return performanceResult;
        }

        // Calculates performances for all recommendations
        public static List<PerformanceResult> CalculatePerformances(RecommendationsFromCsv p_recommendations, Dictionary<string, List<YFRecord>> p_yfData, int[] p_nDayinFuture)
        {
            List<PerformanceResult> results = [];

            foreach (Recommendation recommendation in p_recommendations.Recommendations)
            {
                List<YFRecord>? tickerRecords = p_yfData.TryGetValue(recommendation.Ticker, out List<YFRecord>? value) ? value : null;

                if (tickerRecords != null)
                {
                    PerformanceResult performanceResult = CalculatePerformance(tickerRecords, recommendation, p_nDayinFuture);
                    results.Add(performanceResult);
                }
                else
                {
                    PerformanceResult performanceResult = new()
                    {
                        Id = recommendation.Id,
                        Ticker = recommendation.Ticker,
                        Date = recommendation.Date,
                        FuturePerformances = p_nDayinFuture.ToDictionary(period => period, period => float.NaN)
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
            foreach (int period in p_periods)
                csv.WriteField($"Perf_{period}d");
            csv.NextRecord();

            // Write performance results
            foreach (PerformanceResult result in p_performanceResults)
            {
                csv.WriteField(result.Id);
                csv.WriteField(result.Ticker);
                csv.WriteField(result.Date);
                foreach (int period in p_periods)
                {
                    if (result.FuturePerformances.TryGetValue(period, out float value))
                        csv.WriteField(value);
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

            string[] tickers = recommendationsFromCsv.UniqueTickers;
            Dictionary<string, List<YFRecord>> yfData = ReadYahooCsvFiles(tickers, "D:/Temp/YFHist/");

            int[] p_nDayinFuture = [3, 5, 10, 21, 42, 63, 84, 105, 126, 189];
            List<PerformanceResult> performances = CalculatePerformances(recommendationsFromCsv, yfData, p_nDayinFuture);

            string outputCsvFile = "D:/Temp/recommendationResult.csv";
            WritePerformanceResultsToCsv(outputCsvFile, performances, p_nDayinFuture);
        }
    }
}
