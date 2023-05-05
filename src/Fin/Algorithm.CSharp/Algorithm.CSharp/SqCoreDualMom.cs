#region imports
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;
using System.Drawing;
using QuantConnect;
using QuantConnect.Algorithm.Framework;
using QuantConnect.Algorithm.Framework.Selection;
using QuantConnect.Algorithm.Framework.Alphas;
using QuantConnect.Algorithm.Framework.Portfolio;
using QuantConnect.Algorithm.Framework.Execution;
using QuantConnect.Algorithm.Framework.Risk;
using QuantConnect.Parameters;
using QuantConnect.Benchmarks;
using QuantConnect.Brokerages;
using QuantConnect.Util;
using QuantConnect.Interfaces;
using QuantConnect.Algorithm;
using QuantConnect.Indicators;
using QuantConnect.Data;
using QuantConnect.Data.Consolidators;
using QuantConnect.Data.Custom;
using QuantConnect.Data.Fundamental;
using QuantConnect.Data.Market;
using QuantConnect.Data.UniverseSelection;
using QuantConnect.Notifications;
using QuantConnect.Orders;
using QuantConnect.Orders.Fees;
using QuantConnect.Orders.Fills;
using QuantConnect.Orders.Slippage;
using QuantConnect.Scheduling;
using QuantConnect.Securities;
using QuantConnect.Securities.Equity;
using QuantConnect.Securities.Future;
using QuantConnect.Securities.Option;
using QuantConnect.Securities.Forex;
using QuantConnect.Securities.Crypto;
using QuantConnect.Securities.Interfaces;
using QuantConnect.Storage;
using QuantConnect.Data.Custom.AlphaStreams;
using QCAlgorithmFramework = QuantConnect.Algorithm.QCAlgorithm;
using QCAlgorithmFrameworkBridge = QuantConnect.Algorithm.QCAlgorithm;
#endregion

// >This is the successful validation that the - TaaMeta's sub-strategy - DualMomentum works similarly 
// in QuantConnect's transaction-based portfolio backtest,
// as in the Excel or Python-based daily-return multiplication matrix backtests.
// QuantConnect transactions simulate real life. The trades.
// >There are 3 differences in matrix-multiplication vs. correct buy/sell trade based backtests.
// 1. An Excel backtest buys a fractional number of shares. In real life, we cannot do that. (Think about Berkshire Hathaway A stock)
// 2. An Excel backtest immediately invests dividends (because of daily AdjustedPrices usage). QC Raw price usage adds dividends to the cash pool and invests dividends only at the next monthly rebalance.
// 3. An Excel backtest uses YF 4-digit rounded values for adjusted prices. QC Raw price uses perfectly accurate real-life prices for buy/sell transactions. As it would have happened in real life.
// >What were the differences between Excel and QC backtests?
// For 10 years from 2013 to 2023: Total Return: Excel: 98%, QuantConnect: 102%. The difference is 4% over 10 years. Acceptable considering the previous 3 differences and the fact that small differences at the start are magnified later.
// For 1 year from 2022-01-01 to 2023-02: Total return: Excel: 4.285%, QuantConnect: 4.296%. The difference is marginal.
// As QuantConnect simulation doesn't invest the dividends immediately (only at the next rebalance), we expected the Excel simulation to be a tiny bit better.
// On the contrary, QC simulation is a tiny bit better, but the difference is actually not noticeable.
// See: TheImportance Of RawPrice Based Transaction Simulation Vs Excel AdjustePrice By MichaelHarris.pdf https://www.priceactionlab.com/Blog/2021/05/trend-following-stocks/
namespace QuantConnect.Algorithm.CSharp
{
    // class QcPrice
    // {
    //     public DateTime QuoteTime;
    //     public DateTime ReferenceDate;
    //     public decimal Close;
    // }
    // class QcDividend
    // {
    //     public DateTime QuoteTime;
    //     public DateTime ReferenceDate;
    //     public Dividend Dividend;
    // }
    // class QcSplit
    // {
    //     public DateTime QuoteTime;
    //     public DateTime ReferenceDate;
    //     public Split Split;
    // }
    // class SqPrice
    // {
    //     public DateTime ReferenceDate;
    //     public decimal Close;
    // }
    // class SqSplit
    // {
    //     public DateTime ReferenceDate;
    //     public decimal SplitFactor;
    // }
    public class SQ_DualMomentum : QCAlgorithm
    {
        DateTime _startDate = DateTime.MinValue;
        DateTime _endDate = DateTime.MaxValue;
        TimeSpan _warmUp = TimeSpan.Zero;
        // List<string> _tickers = new List<string> { "AMZN", "TSLA", "VXX", "SPY", "TLT", "SHY" };
        List<string> _tickers = new List<string> { "VNQ", "EEM", "DBC", "SPY", "TLT", "SHY" };
        private Dictionary<string, Symbol> _symbolsDaily = new Dictionary<string, Symbol>();
        private Dictionary<string, Symbol> _symbolsMinute = new Dictionary<string, Symbol>();
        DateTime startTime;
        private int _lookbackTradingDays = 63;
        private int _numberOfEtfsSelected = 3;
        private Dictionary<string, List<QcPrice>> _rawCloses = new Dictionary<string, List<QcPrice>>();
        private Dictionary<string, List<QcPrice>> _adjCloses = new Dictionary<string, List<QcPrice>>();
        private Dictionary<string, List<QcDividend>> _dividends = new Dictionary<string, List<QcDividend>>();
        private Dictionary<string, List<QcSplit>> _splits = new Dictionary<string, List<QcSplit>>();
        bool _isTradeOnMinuteResolution = false; // until QC fixes the Daily resolution trading problem, use per minute for QC Cloud running. But don't use it in local VsCode execution.
        private Dictionary<string, List<SqPrice>> _rawClosesFromYfLists = new Dictionary<string, List<SqPrice>>();
        private Dictionary<string, Dictionary<DateTime, decimal>> _rawClosesFromYfDicts = new Dictionary<string, Dictionary<DateTime, decimal>>();

        public override void Initialize()
        {
            startTime = DateTime.UtcNow;
            _startDate = new DateTime(2006, 01, 01); // means Local time, not UTC
            _warmUp = TimeSpan.FromDays(200); // Wind time back 200 calendar days from start
            _endDate = DateTime.Now;
            // _endDate = new DateTime(2023, 02, 28); // means Local time, not UTC

            SetStartDate(_startDate);
            SetEndDate(_endDate);
            SetWarmUp(_warmUp);
            SetCash(10000000);

            Orders.MarketOnCloseOrder.SubmissionTimeBuffer = TimeSpan.FromMinutes(0.5); // change the submission time threshold of MOC orders


            foreach (string ticker in _tickers)
            {
                Symbol symbolDaily = AddEquity(ticker, Resolution.Daily, dataNormalizationMode: DataNormalizationMode.Raw).Symbol;
                _symbolsDaily.Add(ticker, symbolDaily);
                Securities[symbolDaily].FeeModel = new ConstantFeeModel(0);
                _rawCloses.Add(ticker, new List<QcPrice>());
                _adjCloses.Add(ticker, new List<QcPrice>());
                _dividends.Add(ticker, new List<QcDividend>());
                _splits.Add(ticker, new List<QcSplit>());

                if (_isTradeOnMinuteResolution)
                {
                    Symbol symbolMinute = AddEquity(ticker, Resolution.Minute, dataNormalizationMode: DataNormalizationMode.Raw).Symbol;
                    _symbolsMinute.Add(ticker, symbolMinute);
                    Securities[symbolMinute].FeeModel = new ConstantFeeModel(0);
                }
            }

            // Call the DownloadAndProcessData method to get real life close prices from YF
            _rawClosesFromYfLists = new Dictionary<string, List<SqPrice>>();
            _rawClosesFromYfDicts = new Dictionary<string, Dictionary<DateTime, decimal>>();
            foreach (string ticker in _tickers)
            {
                DownloadAndProcessData(ticker, _startDate, _warmUp, _endDate);
            }

            Schedule.On(DateRules.MonthEnd("SPY"), TimeRules.BeforeMarketClose("SPY", 30), () =>
            {
                if (IsWarmingUp)
                    return;
                Dictionary<string, List<SqPrice>> usedAdjustedClosePrices = GetUsedAdjustedClosePriceData();
                Dictionary<string, decimal> nextMonthWeights = HistPerfCalc(usedAdjustedClosePrices);
                decimal currentPV = PvCalculation(usedAdjustedClosePrices);

                Dictionary<string, Symbol> tradedSymbols = _isTradeOnMinuteResolution ? _symbolsMinute : _symbolsDaily;

                string logMessage = $"New positions after close of {this.Time.Date:yyyy-MM-dd}: ";
                string logMessage2 = "Close prices are: ";

                foreach (KeyValuePair<string, Symbol> kvp in tradedSymbols)
                {
                    string ticker = kvp.Key;
                    List<SqPrice> tickerUsedAdjustedClosePrices = usedAdjustedClosePrices[ticker];
                    decimal newMarketValue = 0;
                    decimal newPosition = 0;
                    if (nextMonthWeights[ticker] != 0)
                    {
                        newMarketValue = currentPV * nextMonthWeights[ticker];
                        newPosition = Math.Round(newMarketValue / tickerUsedAdjustedClosePrices[^1].Close); // use the last element
                    }
                    decimal positionChange = newPosition - Portfolio[ticker].Quantity;
                    if (positionChange != 0)
                        MarketOnCloseOrder(kvp.Value, positionChange); // QC raises Warning if order quantity = 0. So, we don't sent these. "Unable to submit order with id -10 that has zero quantity."
                    logMessage += ticker + ": " + newPosition + "; ";
                    logMessage2 += ticker + ": " + ((tickerUsedAdjustedClosePrices.Count != 0) ? tickerUsedAdjustedClosePrices[^1].Close.ToString() : "N/A")  + "; "; // use the last element
                }
                logMessage = logMessage.Substring(0, logMessage.Length - 2) + ".";
                logMessage2 = logMessage2.Substring(0, logMessage2.Length - 2) + ".";
                string LogMessageToLog = logMessage + " " + logMessage2 + $" Previous cash: {Portfolio.Cash}. Current PV: {currentPV}.";

                Log(LogMessageToLog);
            });
        }

        /// OnData event is the primary entry point for your algorithm. Each new data point will be pumped in here.
        /// Slice object keyed by symbol containing the stock data
        public override void OnData(Slice slice)
        {
            try
            {
                bool isDataPerMinute = !(slice.Time.Hour == 0 && slice.Time.Minute == 0);
                if (_isTradeOnMinuteResolution && isDataPerMinute)
                    return;

                string sliceTime = slice.Time.ToString();
                Log($"Cash in Portfolio on day {sliceTime}: ${Portfolio.Cash}.");
                // Log($"OnData(Slice). Slice.Time: {slice.Time}, this.Time: {sliceTime}");
                foreach (string ticker in _tickers)
                {
                    var symbol = _symbolsDaily[ticker];
                    Split occuredSplit = (slice.Splits.ContainsKey(symbol) && slice.Splits[symbol].Type == SplitType.SplitOccurred) ? slice.Splits[symbol] : null; // split.Type can be Warning and SplitOccured. Ignore 1-day early Split Warnings. Just use the occured

                    decimal? rawClose = null;
                    if (slice.Bars.TryGetValue(symbol, out TradeBar bar))
                        rawClose = bar.Close; // Probably bug in QC: if there is a Split for the daily bar, then QC SplitAdjust that bar, even though we asked RAW data. QC only does it for the Split day. We can undo it, because the Split.ReferencePrice has the RAW price.
                    if (rawClose != null) // we have a split or dividend on Sunday, but there is no bar, so there is no price, which is fine
                    {
                        if (occuredSplit != null)
                            rawClose = occuredSplit.ReferencePrice; // ReferencePrice is RAW, not adjusted. Fixing QC bug of giving SplitAdjusted bar on Split day.
                                                                    // clPrice = slice.Splits[_symbol].Price; // Price is an alias to Value. Value is this: For streams of data this is the price now, for OHLC packets this is the closing price.            

                        _rawCloses[ticker].Add(new QcPrice() { QuoteTime = slice.Time, ReferenceDate = slice.Time.Date.AddDays(-1), Close = (decimal)rawClose });
                        _adjCloses[ticker].Add(new QcPrice() { QuoteTime = slice.Time, ReferenceDate = slice.Time.Date.AddDays(-1), Close = (decimal)rawClose });
                    }
                    if (slice.Dividends.ContainsKey(symbol))
                    {
                        var dividend = slice.Dividends[symbol];
                        _dividends[ticker].Add(new QcDividend() { QuoteTime = slice.Time, ReferenceDate = slice.Time.Date.AddDays(-1), Dividend = dividend });
                        decimal divAdjMultiplicator = 1 - dividend.Distribution / dividend.ReferencePrice;
                        for (int i = 0; i < _adjCloses[ticker].Count; i++)
                        {
                            _adjCloses[ticker][i].Close *= divAdjMultiplicator;
                        }
                        Log($"Dividend on {slice.Time.ToString()}: {ticker} ${dividend.Distribution} when ReferencePrice was {dividend.ReferencePrice}.");
                    }
                    if (occuredSplit != null)  // Split.SplitOccurred comes on the correct day with slice.Time: 8/25/2022 12:00:00
                    {
                        _splits[ticker].Add(new QcSplit() { QuoteTime = slice.Time, ReferenceDate = slice.Time.Date.AddDays(-1), Split = occuredSplit });
                        decimal refPrice = occuredSplit.ReferencePrice;    // Contains RAW price (before Split adjustment). Not used here.
                        decimal splitAdjMultiplicator = occuredSplit.SplitFactor;
                        for (int i = 0; i < _adjCloses[ticker].Count; i++)  // Not-chosen option: if we 'have to' use QC bug 'wrongly-adjusted' rawClose, we can skip the last item. In that case we don't apply the split adjustment to the last item, which is the same day as the day of Split.
                        {
                            _adjCloses[ticker][i].Close *= splitAdjMultiplicator;
                        }
                    }
                }
            }
            catch (System.Exception e)
            {
                Log($"Error. Exception in OnData(Slice). Slice.Time: {slice.Time}, msg: {e.Message}");
            }
        }
        private void DownloadAndProcessData(string p_ticker, DateTime p_startDate, TimeSpan p_warmUp, DateTime p_endDate)
        {
            long periodStart = DateTimeUtcToUnixTimeStamp(p_startDate - p_warmUp);
            long periodEnd = DateTimeUtcToUnixTimeStamp(p_endDate.AddDays(1)); // if p_endDate is a fixed date (2023-02-28:00:00), then it has to be increased, otherwise YF doesn't give that day data.

            // Step 1. Get Split data
            string splitCsvUrl = $"https://query1.finance.yahoo.com/v7/finance/download/{p_ticker}?period1={periodStart}&period2={periodEnd}&interval=1d&events=split&includeAdjustedClose=true";
            string splitCsvData = string.Empty;
            try
            {
                splitCsvData = this.Download(splitCsvUrl); // "Date,Stock Splits\n2023-03-07,1:4"
            }
            catch (Exception e)
            {
                Log($"Exception: {e.Message}");
                return;
            }

            List<SqSplit> splits = new List<SqSplit>();
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
                        splits.Add(new SqSplit() { ReferenceDate = date, SplitFactor = decimal.Divide(split1, split2) });
                }
                rowStartInd = splitEndIndExcl + 1; // jump over the '\n'
            }

            // Step 2. Get Price history data
            string priceCsvUrl = $"https://query1.finance.yahoo.com/v7/finance/download/{p_ticker}?period1={periodStart}&period2={periodEnd}&interval=1d&events=history&includeAdjustedClose=true";
            string priceCsvData = string.Empty;
            try
            {
                priceCsvData = this.Download(priceCsvUrl);  // ""Date,Open,High,Low,Close,Adj Close,Volume\n2022-03-21,131.279999,131.669998,129.750000,130.350006,127.057739,26122000\n" 
            }
            catch (Exception e)
            {
                Log($"Exception: {e.Message}");
                return;
            }

            List<SqPrice> rawClosesFromYfList = new List<SqPrice>();
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
                        rawClosesFromYfList.Add(new SqPrice() { ReferenceDate = date, Close = close });
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
            _rawClosesFromYfLists[p_ticker] = rawClosesFromYfList;
            
            // Step 4. Convert List to Dictionary, because that is 6x faster to query
            var rawClosesFromYfDict = new Dictionary<DateTime, decimal>(rawClosesFromYfList.Count);
            for (int i = 0; i < rawClosesFromYfList.Count; i++)
            {
                var yfPrice = rawClosesFromYfList[i];
                rawClosesFromYfDict[yfPrice.ReferenceDate] = yfPrice.Close;
            }
            _rawClosesFromYfDicts[p_ticker] = rawClosesFromYfDict;
        }
        private Dictionary<string, List<SqPrice>> GetUsedAdjustedClosePriceData()
        {
            Dictionary<string, List<SqPrice>> usedAdjCloses = new Dictionary<string, List<SqPrice>>();
            // iterate over each key-value pair in the dictionary
            foreach (KeyValuePair<string, List<QcPrice>> kvp in _adjCloses) // loop for each tickers
            {
                // get the last lookbackTradingDays items for this key + current raw data from YF
                string ticker = kvp.Key;
                List<QcPrice> qcAdjCloses = kvp.Value;
                List<SqPrice> lastLbTdPrices = new List<SqPrice>();
                int startIndex = Math.Max(0, qcAdjCloses.Count - _lookbackTradingDays);
                for (int i = startIndex; i < qcAdjCloses.Count; i++)
                {
                    lastLbTdPrices.Add(new SqPrice() { ReferenceDate = qcAdjCloses[i].ReferenceDate, Close = qcAdjCloses[i].Close });
                }

                // add the last lookbackTradingDays items to the new dictionary
                if (_rawClosesFromYfDicts[ticker].TryGetValue(this.Time.Date, out decimal lastRawClose))
                    lastLbTdPrices.Add(new SqPrice() { ReferenceDate = this.Time.Date, Close = lastRawClose});
                else // if lastLbTdPrices is empty, then not finding this date is fine. If lastLbTdPrices has at least 1 items, we expect that we find this date.
                    if (qcAdjCloses.Count > 0)
                        throw new Exception($"Cannot find date {this.Time.Date.ToString()} in the YF.");

                usedAdjCloses[ticker] = lastLbTdPrices;
            }

            return usedAdjCloses;
        }

        private Dictionary<string, decimal> HistPerfCalc(Dictionary<string, List<SqPrice>> p_usedAdjustedClosePrices)
        {
            // tie in ranks!!!
            Dictionary<string, decimal> relativeMomentums = new Dictionary<string, decimal>();

            foreach (string key in p_usedAdjustedClosePrices.Keys)
            {
                decimal relMom = -99;
                List<SqPrice> usedAdjustedClosePrice = p_usedAdjustedClosePrices[key];
                if (usedAdjustedClosePrice.Count == _lookbackTradingDays + 1)
                    relMom = usedAdjustedClosePrice[^1].Close / usedAdjustedClosePrice[0].Close - 1; // last element divided by the first
                relativeMomentums.Add(key, relMom);
            }
            // Convert the relativeMomentum dictionary to a list of key-value pairs
            List<KeyValuePair<string, decimal>> list = relativeMomentums.ToList();

            // Sort the list based on the values
            list.Sort((x, y) => y.Value.CompareTo(x.Value));

            // Assign a rank to each key and store the result in a new dictionary
            int rank = 1;
            decimal lastValue = 9999;
            int numTies = 0;
            Dictionary<string, int> rankDict = new Dictionary<string, int>();
            foreach (KeyValuePair<string, decimal> kvp in list)
            {
                if (kvp.Value < lastValue)
                {
                    rank += numTies;
                    numTies = 0;
                    lastValue = kvp.Value;
                }
                numTies++;
                rankDict.Add(kvp.Key, rank);
            }
            // Create a new dictionary of bools indicating whether the rankDict value is <=_numberOfEtfsSelected and the corresponding value in relativeMomentums is positive
            Dictionary<string, bool> resultDict = new Dictionary<string, bool>();
            int trueCount = 0;
            foreach (KeyValuePair<string, int> kvp in rankDict)
            {
                string ticker = kvp.Key;
                if (kvp.Value <= _numberOfEtfsSelected && relativeMomentums[ticker] > 0)
                {
                    resultDict.Add(ticker, true);
                    trueCount++;
                }
                else
                {
                    resultDict.Add(ticker, false);
                }
            }
            Dictionary<string, decimal> nextMonthWeights = new Dictionary<string, decimal>();
            int noPlayedEtf = Math.Max(trueCount, _numberOfEtfsSelected);
            decimal playedWeight = decimal.Divide(1, noPlayedEtf);
            foreach (string key in p_usedAdjustedClosePrices.Keys)
            {
                if (resultDict[key] == true)
                    nextMonthWeights.Add(key, playedWeight);
                else
                    nextMonthWeights.Add(key, 0);
            }
            return nextMonthWeights;
        }
        private decimal PvCalculation(Dictionary<string, List<SqPrice>> p_usedAdjustedClosePrices)
        {
            decimal currentPV = 0m;

            decimal cashValue = Portfolio.Cash;

            currentPV = cashValue;

            foreach (string ticker in _tickers)
            {
                if (Securities.ContainsKey(ticker))
                {
                    SecurityHolding position = Portfolio[ticker];
                    if (position != null)
                    {
                        // Get the current position and price for this ticker
                        decimal quantity = position.Quantity;
                        
                        List<SqPrice> usedAdjustedClosePrices = p_usedAdjustedClosePrices[ticker];
                        decimal currentPrice = (usedAdjustedClosePrices.Count != 0) ? usedAdjustedClosePrices[^1].Close : 0;    // get the last element
                        currentPV += quantity * currentPrice;
                    }
                }
            }

            return currentPV;
        }
        public static long DateTimeUtcToUnixTimeStamp(DateTime p_utcDate) // Int would roll over to a negative in 2038 (if you are using UNIX timestamp), so long is safer
        {
            // Unix timestamp is seconds past epoch
            System.DateTime dtDateTime = new(1970, 1, 1, 0, 0, 0, 0, System.DateTimeKind.Utc);
            TimeSpan span = p_utcDate - dtDateTime;
            return (long)span.TotalSeconds;
        }

    }
}