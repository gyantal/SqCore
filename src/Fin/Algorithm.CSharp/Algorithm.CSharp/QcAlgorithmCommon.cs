using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using QuantConnect.Data.Market;
using System.Collections.Specialized;
using System.Web;

namespace QuantConnect.Algorithm.CSharp
{
    class QcPrice
    {
        public DateTime ReferenceDate;
        public decimal Close;
    }

    class QcDividend
    {
        public DateTime ReferenceDate;
        public Dividend Dividend;
    }

    class QcSplit
    {
        public DateTime ReferenceDate;
        public Split Split;
    }

    class YfSplit
    {
        public DateTime ReferenceDate;
        public decimal SplitFactor;
    }

    public class DailyEarningsData
    {
        [JsonPropertyName("date")]
        public DateTime Date { get; set; }
        // public List<StockEarningsData> Stocks { get; set; }
    }

    public class StockEarningsData
    {
        public string Ticker { get; set; }
        public string Eps { get; set; }
        public string PercentageSurprise { get; set; }
        public string ConsensusEpsForecast { get; set; }
        public string NumberOfEstimates { get; set; }
    }
     enum StartDateAutoCalcMode { Unknown, WhenAllTickersAlive /* default if not given in params */ , WhenFirstTickerAlive, CustomGC };

    class QCAlgorithmUtils
    {
        static Dictionary<string, StartDateAutoCalcMode> g_startDateAutoCalcModeDict = new Dictionary<string, StartDateAutoCalcMode> { {"Unknown", StartDateAutoCalcMode.Unknown}, {"WhenAllTickersAlive", StartDateAutoCalcMode.WhenAllTickersAlive}, {"WhenFirstTickerAlive", StartDateAutoCalcMode.WhenFirstTickerAlive},{"CustomGC", StartDateAutoCalcMode.CustomGC} };

        public static long DateTimeUtcToUnixTimeStamp(DateTime p_utcDate) // Int would roll over to a negative in 2038 (if you are using UNIX timestamp), so long is safer
        {
            // Unix timestamp is seconds past epoch
            System.DateTime dtDateTime = new(1970, 1, 1, 0, 0, 0, 0, System.DateTimeKind.Utc);
            TimeSpan span = p_utcDate - dtDateTime;
            return (long)span.TotalSeconds;
        }

        public static void ProcessAlgorithmParam(string p_AlgorithmParam,  out DateTime p_forcedStartDate, out DateTime p_forcedEndDate, out StartDateAutoCalcMode p_startDateAutoCalcMode, out List<string> p_tickers, out Dictionary<string, decimal> p_weights, out int p_rebalancePeriodDays)
        {
            p_weights = new();
            NameValueCollection query = HttpUtility.ParseQueryString(p_AlgorithmParam); // e.g. "assets=SPY,TLT&weights=60,40&rebFreq=Daily,10d"

            // Step1: process startDate and endDate
            string startDateStr = query.Get("startDate");
            if (string.IsNullOrEmpty(startDateStr))
                p_forcedStartDate = DateTime.MinValue;
            else
            {
                if (DateTime.TryParse(startDateStr, out DateTime startDate))
                    p_forcedStartDate = startDate;
                else
                    throw new ArgumentException("Invalid date format in startDate.");
            }
            string endDateStr = query.Get("endDate");
            if (string.IsNullOrEmpty(endDateStr))
                p_forcedEndDate = DateTime.MaxValue;
            else
            {
                if (endDateStr.Equals("Now", StringComparison.OrdinalIgnoreCase))
                    p_forcedEndDate = DateTime.Now;
                else if (DateTime.TryParse(endDateStr, out DateTime endDate))
                    p_forcedEndDate = endDate;
                else
                    throw new ArgumentException("Invalid date format in endDate.");
            }
            string startDateAutoCalcModeStr = query.Get("startDateAutoCalcMode");
            if (string.IsNullOrEmpty(startDateAutoCalcModeStr))
                p_startDateAutoCalcMode = StartDateAutoCalcMode.WhenAllTickersAlive; // default if not given in params
            else
                if (!g_startDateAutoCalcModeDict.TryGetValue(startDateAutoCalcModeStr, out p_startDateAutoCalcMode))
                    throw new ArgumentException("Invalid startDateAutoCalcMode format.");


            // Step 2: process tickers/weights
            p_tickers = new();
            p_weights = new();
            p_rebalancePeriodDays = -1;  // invalid value. Come from parameters

            string[] tickers = query.Get("assets")?.Split(',', StringSplitOptions.RemoveEmptyEntries) ?? Array.Empty<string>();
            string[] weights = query.Get("weights")?.Split(',', StringSplitOptions.RemoveEmptyEntries) ?? Array.Empty<string>();
            if (tickers.Length != weights.Length)
                throw new ArgumentException("The number of assets and weights must be the same.");

            for (int i = 0; i < tickers.Length; i++)
            {
                string ticker = tickers[i];
                decimal weight = decimal.Parse(weights[i]) / 100m; // "60" => 0.6
                p_weights[ticker] = weight;
                // p_tickers.Add(ticker);
            }
            p_tickers = new List<string>(p_weights.Keys);

            
            // Step 3: process rebFreq
            string[] rebalanceParams = query.Get("rebFreq")?.Split(',', StringSplitOptions.RemoveEmptyEntries) ?? Array.Empty<string>();
            if (rebalanceParams.Length != 2)
                throw new ArgumentException($"The rebFreq {rebalanceParams} is incorrect.");

            string rebalancePeriodNumStr = rebalanceParams[1]; // second item is "10d"
            if (rebalancePeriodNumStr.Length == 0)
                p_rebalancePeriodDays = 1; // default
            char rebalancePeriodNumStrLastChar = rebalancePeriodNumStr[^1]; // 'd' or 'w' or 'm', but it is not required to be present
            if (Char.IsLetter(rebalancePeriodNumStrLastChar)) // if 'd/w/m' is given, remove it
                rebalancePeriodNumStr = rebalancePeriodNumStr[..^1];
            if (!Int32.TryParse(rebalancePeriodNumStr, out p_rebalancePeriodDays))
                throw new ArgumentException($"The rebFreq's rebalancePeriodNumStr {rebalancePeriodNumStr} cannot be converted to int.");
        }
        
        public static void DownloadAndProcessYfData(QCAlgorithm p_algorithm, string p_ticker, DateTime p_startDate, TimeSpan p_warmUp, DateTime p_endDate, ref Dictionary<string, List<QcPrice>> p_rawClosesFromYfLists, ref Dictionary<string, Dictionary<DateTime, decimal>> p_rawClosesFromYfDicts)
        {
            long periodStart = QCAlgorithmUtils.DateTimeUtcToUnixTimeStamp(p_startDate - p_warmUp);
            long periodEnd = QCAlgorithmUtils.DateTimeUtcToUnixTimeStamp(p_endDate.AddDays(1)); // if p_endDate is a fixed date (2023-02-28:00:00), then it has to be increased, otherwise YF doesn't give that day data.

            // Step 1. Get Split data
            string splitCsvUrl = $"https://query1.finance.yahoo.com/v7/finance/download/{p_ticker}?period1={periodStart}&period2={periodEnd}&interval=1d&events=split&includeAdjustedClose=true";
            string splitCsvData = string.Empty;
            try
            {
                splitCsvData = p_algorithm.Download(splitCsvUrl); // "Date,Stock Splits\n2023-03-07,1:4"
            }
            catch (Exception e)
            {
                p_algorithm.Log($"Exception: {e.Message}");
                return;
            }

            List<YfSplit> splits = new List<YfSplit>();
            int rowStartInd = splitCsvData.IndexOf('\n');   // jump over the header Date,Stock Splits
            rowStartInd = (rowStartInd == -1) ? splitCsvData.Length : rowStartInd + 1;
            while (rowStartInd < splitCsvData.Length) // very fast implementation without String.Split() RAM allocation
            {
                int splitStartInd = splitCsvData.IndexOf(',', rowStartInd);
                int splitMidInd = (splitStartInd != -1) ? splitCsvData.IndexOf(':', splitStartInd + 1) : -1;
                int splitEndIndExcl = (splitMidInd != -1) ? splitCsvData.IndexOf('\n', splitMidInd + 1) : splitCsvData.Length;
                if (splitEndIndExcl == -1)
                    splitEndIndExcl = splitCsvData.Length;

                string dateStr = (splitStartInd != -1) ? splitCsvData.Substring(rowStartInd, splitStartInd - rowStartInd) : string.Empty;
                string split1Str = (splitStartInd != -1 && splitMidInd != -1) ? splitCsvData.Substring(splitStartInd + 1, splitMidInd - splitStartInd - 1) : string.Empty;
                string split2Str = (splitMidInd != -1) ? splitCsvData.Substring(splitMidInd + 1, splitEndIndExcl - splitMidInd - 1) : string.Empty;

                if (DateTime.TryParseExact(dateStr, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out DateTime date))
                {
                    if (Decimal.TryParse(split1Str, out decimal split1) && Decimal.TryParse(split2Str, out decimal split2))
                        splits.Add(new YfSplit() { ReferenceDate = date, SplitFactor = decimal.Divide(split1, split2) });
                }
                rowStartInd = splitEndIndExcl + 1; // jump over the '\n'
            }

            // Step 2. Get Price history data
            string priceCsvUrl = $"https://query1.finance.yahoo.com/v7/finance/download/{p_ticker}?period1={periodStart}&period2={periodEnd}&interval=1d&events=history&includeAdjustedClose=true";
            string priceCsvData = string.Empty;
            try
            {
                priceCsvData = p_algorithm.Download(priceCsvUrl);  // ""Date,Open,High,Low,Close,Adj Close,Volume\n2022-03-21,131.279999,131.669998,129.750000,130.350006,127.057739,26122000\n" 
            }
            catch (Exception e)
            {
                p_algorithm.Log($"Exception: {e.Message}");
                return;
            }

            List<QcPrice> rawClosesFromYfList = new List<QcPrice>();
            rowStartInd = priceCsvData.IndexOf('\n');   // jump over the header Date,...
            rowStartInd = (rowStartInd == -1) ? priceCsvData.Length : rowStartInd + 1; // jump over the '\n'
            while (rowStartInd < priceCsvData.Length) // very fast implementation without String.Split() RAM allocation
            {   // chronological processing: it goes forward in time. Starting with StartDate
                // (Raw)Close is non adjusted for dividend, but adjusted for split. Get that and we will reverse Split-adjust later
                int openInd = priceCsvData.IndexOf(',', rowStartInd);
                int highInd = (openInd != -1) ? priceCsvData.IndexOf(',', openInd + 1) : -1;
                int lowInd = (highInd != -1) ? priceCsvData.IndexOf(',', highInd + 1) : -1;
                int closeInd = (lowInd != -1) ? priceCsvData.IndexOf(',', lowInd + 1) : -1;
                int adjCloseInd = (closeInd != -1) ? priceCsvData.IndexOf(',', closeInd + 1) : -1;

                string dateStr = (openInd != -1) ? priceCsvData.Substring(rowStartInd, openInd - rowStartInd) : string.Empty;
                string closeStr = (closeInd != -1 && adjCloseInd != -1) ? priceCsvData.Substring(closeInd + 1, adjCloseInd - closeInd - 1) : string.Empty;

                if (DateTime.TryParseExact(dateStr, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out DateTime date))
                {
                    if (Decimal.TryParse(closeStr, out decimal close))
                        rawClosesFromYfList.Add(new QcPrice() { ReferenceDate = date, Close = close });
                }
                rowStartInd = (closeInd != -1) ? priceCsvData.IndexOf('\n', adjCloseInd + 1) : -1;
                rowStartInd = (rowStartInd == -1) ? priceCsvData.Length : rowStartInd + 1; // jump over the '\n'
            }

            // Step 3. Reverse Adjust history data with the splits. Going backwards in time, starting from 'today'
            if (splits.Count != 0)
            {
                decimal splitMultiplier = 1m;
                int lastSplitIdx = splits.Count - 1;
                DateTime watchedSplitDate = splits[lastSplitIdx].ReferenceDate;

                for (int i = rawClosesFromYfList.Count - 1; i >= 0; i--)
                {
                    DateTime date = rawClosesFromYfList[i].ReferenceDate;
                    if (date < watchedSplitDate)
                    {
                        splitMultiplier *= splits[lastSplitIdx].SplitFactor;
                        lastSplitIdx--;
                        watchedSplitDate = (lastSplitIdx == -1) ? DateTime.MinValue : splits[lastSplitIdx].ReferenceDate;
                    }

                    rawClosesFromYfList[i].Close *= splitMultiplier;
                }
            }
            p_rawClosesFromYfLists[p_ticker] = rawClosesFromYfList;

            // Step 4. Convert List to Dictionary, because that is 6x faster to query
            var rawClosesFromYfDict = new Dictionary<DateTime, decimal>(rawClosesFromYfList.Count);
            for (int i = 0; i < rawClosesFromYfList.Count; i++)
            {
                var yfPrice = rawClosesFromYfList[i];
                rawClosesFromYfDict[yfPrice.ReferenceDate] = yfPrice.Close;
            }
            p_rawClosesFromYfDicts[p_ticker] = rawClosesFromYfDict;
        }

        public void DownloadAndProcessEarningsData()
        {
            // string earningsDataJson = this.Download("https://data.quantpedia.com/backtesting_data/economic/earnings_dates_eps.json");

            string earningsDataJson = "[{\"date\": \"2010-01-04\"},{\"date\": \"2010-01-05\"}]";
            // List<DailyEarningsData> stockDataList = JsonSerializer.Deserialize<List<DailyEarningsData>>(earningsDataJson, new JsonSerializerOptions() { PropertyNameCaseInsensitive = true });
            List<DailyEarningsData> stockDataList = JsonSerializer.Deserialize<List<DailyEarningsData>>(earningsDataJson);
            var adas = stockDataList[0];
            // DateTime dateee = DateTime.ParseExact(adas.Date, "yyyy-MM-dd", CultureInfo.InvariantCulture);
            // Log(dateee.ToString());

            // string propName = propName.ToLower();

            // foreach (var stockData in stockDataList)
            // {
            //     DateTime date = DateTime.ParseExact(stockData.Date, "yyyy-MM-dd", CultureInfo.InvariantCulture);
            //     List<string> tickers = new List<string>();

            //     foreach (var stock in stockData.Stocks)
            //     {
            //         if (_tickers.Contains(stock.Ticker))
            //         {
            //             tickers.Add(stock.Ticker);
            //         }
            //     }

            //     if (tickers.Count > 0)
            //     {
            //         _requiredEarningsDataDict[date] = tickers;
            //     }
            // }

        }
    }
}