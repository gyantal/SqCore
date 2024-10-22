#define TradeInSqCore

#region imports
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using QuantConnect.Data.Market;
using System.Collections.Specialized;
using System.Web;
using QuantConnect.Securities;
#endregion

namespace QuantConnect.Algorithm.CSharp
{
    public class QcPrice
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

    public sealed class Candle
    {
        public DateTime DateTime { get; internal set; }
        public decimal Open { get; internal set; }
        public decimal High { get; internal set; }
        public decimal Low { get; internal set; }
        public decimal Close { get; internal set; }
        public long Volume { get; internal set; }
        public decimal AdjustedClose { get; internal set; }
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

    public enum StartDateAutoCalcMode { Unknown, WhenAllTickersAlive /* default if not given in params */, WhenFirstTickerAlive, CustomGC };

    public class QCAlgorithmUtils
    {
        public static DateTime g_earliestQcDay = new DateTime(1900, 01, 01); // e.g. SetStartDate() exception: "Please select a start date after January 1st, 1900.". Also DateTime.MinValue cannot be used in QC.HistoryProvider.GetHistory() as it will convert this time to UTC, but taking away 5 hours from MinDate is not possible.
        static Dictionary<string, StartDateAutoCalcMode> g_startDateAutoCalcModeDict = new Dictionary<string, StartDateAutoCalcMode> { { "Unknown", StartDateAutoCalcMode.Unknown }, { "WhenAllTickersAlive", StartDateAutoCalcMode.WhenAllTickersAlive }, { "WhenFirstTickerAlive", StartDateAutoCalcMode.WhenFirstTickerAlive }, { "CustomGC", StartDateAutoCalcMode.CustomGC } };

        public static long DateTimeUtcToUnixTimeStamp(DateTime p_utcDate) // Int would roll over to a negative in 2038 (if you are using UNIX timestamp), so long is safer
        {
            // Unix timestamp is seconds past epoch
            System.DateTime dtDateTime = new(1970, 1, 1, 0, 0, 0, 0, System.DateTimeKind.Utc);
            TimeSpan span = p_utcDate - dtDateTime;
            return (long)span.TotalSeconds;
        }

        public static void ProcessAlgorithmParam(NameValueCollection p_AlgorithmParamQuery, out DateTime p_forcedStartDate, out DateTime p_forcedEndDate, out StartDateAutoCalcMode p_startDateAutoCalcMode)
        {
            // e.g. _AlgorithmParam = "assets=SPY,TLT&weights=60,40&rebFreq=Daily,10d"

            string startDateStr = p_AlgorithmParamQuery.Get("startDate");
            if (string.IsNullOrEmpty(startDateStr))
                p_forcedStartDate = DateTime.MinValue;
            else
            {
                if (DateTime.TryParse(startDateStr, out DateTime startDate))
                    p_forcedStartDate = startDate;
                else
                    throw new ArgumentException("Invalid date format in startDate.");
            }
            string endDateStr = p_AlgorithmParamQuery.Get("endDate");
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
            string startDateAutoCalcModeStr = p_AlgorithmParamQuery.Get("startDateAutoCalcMode");
            if (string.IsNullOrEmpty(startDateAutoCalcModeStr))
                p_startDateAutoCalcMode = StartDateAutoCalcMode.WhenAllTickersAlive; // default if not given in params
            else
                if (!g_startDateAutoCalcModeDict.TryGetValue(startDateAutoCalcModeStr, out p_startDateAutoCalcMode))
                    throw new ArgumentException("Invalid startDateAutoCalcMode format.");
        }

        public static void ApplyDividendMOCAfterClose(SecurityPortfolioManager p_portfolio, Dividends p_sliceDividends, int p_multiplier)
        {
#if TradeInSqCore // do nothing in QcCloud
            // We use 'daily' (not perMinute) TradeBars in SqCore.  When OnData() callback comes with this TradeBar, the dividends of that day is already added to the Cash (by the framework). We remove this before trading, and add back after trading. See comment at ApplyDividendMOCAfterClose()
            foreach (KeyValuePair<Symbol, Dividend> kvp in p_sliceDividends)
            {
                p_portfolio.ApplyDividendMOCAfterClose(kvp.Value, p_multiplier * p_portfolio[kvp.Key].Quantity); // Portfolio[ticker].Quantity is oldPosition before MarketOnCloseOrder(), and newPosition after that (checked!)
            }
#endif
        }

        public static DateTime StartDateAutoCalculation(Dictionary<string, Symbol> p_tradedSymbols, StartDateAutoCalcMode p_startDateAutoCalcMode, out Symbol? p_symbolWithEarliestUsableDataDay)
        {
            DateTime earliestUsableDataDay = DateTime.MinValue;
            DateTime minStartDay = DateTime.MaxValue;
            DateTime maxStartDay = DateTime.MinValue;
            Symbol? _symbolWithMinStartDate = null;
            Symbol? _symbolWithMaxStartDate = null;
            Symbol? _symbolWithEarliestUsableDataDay = null;
            foreach (Symbol symbol in p_tradedSymbols.Values)
            {
                DateTime symbolStartDate = symbol.ID.Date;
                if (symbolStartDate < minStartDay)
                {
                    minStartDay = symbolStartDate;
                    _symbolWithMinStartDate = symbol;
                }
                if (symbolStartDate > maxStartDay)
                {
                    maxStartDay = symbolStartDate;
                    _symbolWithMaxStartDate = symbol;
                }
            }

            switch (p_startDateAutoCalcMode)
            {
                case StartDateAutoCalcMode.WhenFirstTickerAlive: // Usually the first day when WhenAllTickersAlive (default). Aletrnatively WhenFirstTickerAlive.
                    earliestUsableDataDay = minStartDay;
                    _symbolWithEarliestUsableDataDay = _symbolWithMinStartDate;
                    break;
                case StartDateAutoCalcMode.WhenAllTickersAlive:
                    earliestUsableDataDay = maxStartDay;
                    _symbolWithEarliestUsableDataDay = _symbolWithMaxStartDate;
                    break;
                default:
                    throw new NotImplementedException("Unrecognized _startDateAutoCalcMode.");
            }

            p_symbolWithEarliestUsableDataDay = _symbolWithEarliestUsableDataDay;
            return earliestUsableDataDay;
        }

        public static void DownloadAndProcessYfData(QCAlgorithm p_algorithm, List<string> p_tickers, DateTime p_startDate, TimeSpan p_warmUp, DateTime p_endDate, out Dictionary<string, Dictionary<DateTime, decimal>> p_rawClosesFromYfDicts)
        {
            p_rawClosesFromYfDicts = new Dictionary<string, Dictionary<DateTime, decimal>>();

            long periodStart = QCAlgorithmUtils.DateTimeUtcToUnixTimeStamp(p_startDate - p_warmUp);
            long periodEnd = QCAlgorithmUtils.DateTimeUtcToUnixTimeStamp(p_endDate.AddDays(1)); // if p_endDate is a fixed date, it has to be increased

            foreach (string ticker in p_tickers)
            {
                try
                {
                    // Step 1: Fetch all data (prices, splits, dividends) in one request
                    string url = $"https://query2.finance.yahoo.com/v8/finance/chart/{ticker}?period1={periodStart}&period2={periodEnd}&interval=1d&events=div%2Csplit&includeAdjustedClose=true";
                    string dataStr = p_algorithm.Download(url);

                    // Parse the JSON data using JsonDocument
                    using (JsonDocument jsonDoc = JsonDocument.Parse(dataStr))
                    {
                        JsonElement root = jsonDoc.RootElement;

                        // Check if the "chart" and "result" elements exist
                        if (!root.TryGetProperty("chart", out JsonElement chartElement) || !chartElement.TryGetProperty("result", out JsonElement resultArray) || resultArray.GetArrayLength() == 0)
                        {
                            p_algorithm.Log($"No data found for ticker {ticker}");
                            continue;
                        }

                        JsonElement chartResult = resultArray[0];

                        // Extract timestamps, OHLCV data
                        List<long> timestamps = new List<long>();
                        foreach (JsonElement t in chartResult.GetProperty("timestamp").EnumerateArray())
                            timestamps.Add(t.GetInt64());

                        JsonElement ohlcvData = chartResult.GetProperty("indicators").GetProperty("quote")[0];

                        List<decimal> openPrices = new List<decimal>();
                        List<decimal> highPrices = new List<decimal>();
                        List<decimal> lowPrices = new List<decimal>();
                        List<decimal> closePrices = new List<decimal>();
                        List<long> volumes = new List<long>();
                        List<decimal> adjustedClosePrices = new List<decimal>();

                        foreach (JsonElement o in ohlcvData.GetProperty("open").EnumerateArray())
                            openPrices.Add(o.GetDecimal());

                        foreach (JsonElement h in ohlcvData.GetProperty("high").EnumerateArray())
                            highPrices.Add(h.GetDecimal());

                        foreach (JsonElement l in ohlcvData.GetProperty("low").EnumerateArray())
                            lowPrices.Add(l.GetDecimal());

                        foreach (JsonElement c in ohlcvData.GetProperty("close").EnumerateArray())
                            closePrices.Add(c.GetDecimal());

                        foreach (JsonElement v in ohlcvData.GetProperty("volume").EnumerateArray())
                            volumes.Add(v.GetInt64());

                        if (chartResult.TryGetProperty("indicators", out JsonElement indicators) && indicators.TryGetProperty("adjclose", out JsonElement adjCloseElement))
                            foreach (JsonElement a in adjCloseElement[0].GetProperty("adjclose").EnumerateArray())
                                adjustedClosePrices.Add(a.GetDecimal());

                        // Process splits from the events section
                        List<YfSplit> splits = new List<YfSplit>();
                        if (chartResult.TryGetProperty("events", out JsonElement events) && events.TryGetProperty("splits", out JsonElement splitEvents))
                            foreach (JsonProperty split in splitEvents.EnumerateObject())
                            {
                                JsonElement splitEvent = split.Value;
                                long splitDateUnix = splitEvent.GetProperty("date").GetInt64();
                                DateTime splitDate = DateTimeOffset.FromUnixTimeSeconds(splitDateUnix).UtcDateTime.Date;
                                decimal numerator = splitEvent.GetProperty("numerator").GetDecimal();
                                decimal denominator = splitEvent.GetProperty("denominator").GetDecimal();
                                decimal splitFactor = numerator / denominator;
                                splits.Add(new YfSplit() { ReferenceDate = splitDate, SplitFactor = splitFactor });
                            }

                        // Step 3: Create a list of Candle objects to hold the OHLCV and adjusted close data
                        List<Candle> candleList = new List<Candle>();
                        List<QcPrice> rawClosesFromYfList = new List<QcPrice>();

                        for (int i = 0; i < timestamps.Count; i++)
                        {
                            long unixTime = timestamps[i];
                            DateTime date = DateTimeOffset.FromUnixTimeSeconds(unixTime).UtcDateTime.Date;

                            // Get the OHLCV data and adjusted close price
                            decimal openPrice = openPrices[i];
                            decimal highPrice = highPrices[i];
                            decimal lowPrice = lowPrices[i];
                            decimal closePrice = closePrices[i];
                            long volume = volumes[i];
                            decimal adjustedClose = adjustedClosePrices.Count > i ? adjustedClosePrices[i] : closePrice;

                            // Create Candle object (not used in the final output but kept for future use)
                            Candle candle = new Candle()
                            {
                                DateTime = date,
                                Open = openPrice,
                                High = highPrice,
                                Low = lowPrice,
                                Close = closePrice,
                                Volume = volume,
                                AdjustedClose = adjustedClose
                            };

                            rawClosesFromYfList.Add(new QcPrice(){ ReferenceDate = date, Close = closePrice }); // Store only the Close price in the rawClosesFromYfDict

                            candleList.Add(candle); // Optionally, if you want to keep the Candle data for future use
                        }

                        // Step 4: Adjust prices for splits (if any) and update the Close prices dictionary
                        if (splits.Count > 0)
                            rawClosesFromYfList = AdjustPricesForSplits(rawClosesFromYfList, splits);

                        // Step 5: Convert the list of QcPrice to Dictionary for final output, because that is 6x faster to query
                        Dictionary<DateTime, decimal> rawClosesFromYfDict = new Dictionary<DateTime, decimal>();
                        foreach (QcPrice yfPrice in rawClosesFromYfList)
                            rawClosesFromYfDict[yfPrice.ReferenceDate] = yfPrice.Close;

                        // Store the Close prices for the current ticker
                        p_rawClosesFromYfDicts[ticker] = rawClosesFromYfDict;
                    }
                }
                catch (Exception e)
                {
                    p_algorithm.Log($"Exception: {e.Message}");
                    continue;
                }
            }
        }

        private static List<QcPrice> AdjustPricesForSplits(List<QcPrice> rawClosesFromYfList, List<YfSplit> splits)
        {
            // Sort splits by ReferenceDate in descending order using List<T>.Sort
            splits.Sort((x, y) => y.ReferenceDate.CompareTo(x.ReferenceDate));

            decimal splitMultiplier = 1m;
            int lastSplitIndex = 0;
            DateTime nextSplitDate = splits[lastSplitIndex].ReferenceDate;

            // Iterate backwards through raw closes to adjust them for splits
            for (int i = rawClosesFromYfList.Count - 1; i >= 0; i--)
            {
                DateTime date = rawClosesFromYfList[i].ReferenceDate;
                if (date < nextSplitDate)
                {
                    splitMultiplier *= splits[lastSplitIndex].SplitFactor;
                    lastSplitIndex++;
                    if (lastSplitIndex <= splits.Count - 1)
                        nextSplitDate = splits[lastSplitIndex].ReferenceDate;
                    else
                        nextSplitDate = DateTime.MinValue;
                }

                rawClosesFromYfList[i].Close *= splitMultiplier;
            }

            return rawClosesFromYfList;
        }

        public static void DownloadAndProcessYfDataOld(QCAlgorithm p_algorithm, List<string> p_tickers, DateTime p_startDate, TimeSpan p_warmUp, DateTime p_endDate, out Dictionary<string, Dictionary<DateTime, decimal>> p_rawClosesFromYfDicts)
        {
            p_rawClosesFromYfDicts = new Dictionary<string, Dictionary<DateTime, decimal>>();

            long periodStart = QCAlgorithmUtils.DateTimeUtcToUnixTimeStamp(p_startDate - p_warmUp);
            long periodEnd = QCAlgorithmUtils.DateTimeUtcToUnixTimeStamp(p_endDate.AddDays(1)); // if p_endDate is a fixed date (2023-02-28:00:00), then it has to be increased, otherwise YF doesn't give that day data.

            foreach (string ticker in p_tickers)
            {
                // Step 1. Get Split data
                string splitCsvUrl = $"https://query1.finance.yahoo.com/v7/finance/download/{ticker}?period1={periodStart}&period2={periodEnd}&interval=1d&events=split&includeAdjustedClose=true";
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
                string priceCsvUrl = $"https://query1.finance.yahoo.com/v7/finance/download/{ticker}?period1={periodStart}&period2={periodEnd}&interval=1d&events=history&includeAdjustedClose=true";
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

                // Step 4. Convert List to Dictionary, because that is 6x faster to query
                var rawClosesFromYfDict = new Dictionary<DateTime, decimal>(rawClosesFromYfList.Count);
                for (int i = 0; i < rawClosesFromYfList.Count; i++)
                {
                    var yfPrice = rawClosesFromYfList[i];
                    rawClosesFromYfDict[yfPrice.ReferenceDate] = yfPrice.Close;
                }
                p_rawClosesFromYfDicts[ticker] = rawClosesFromYfDict;
            }
        }
    }
}