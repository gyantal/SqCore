#define TradeInSqCore

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
using System.Collections.Specialized;
using System.Web;
#endregion


namespace QuantConnect.Algorithm.CSharp
{
    public class SqPctAllocation : QCAlgorithm
    {
#if TradeInSqCore // 2 simulation environments. We backtest in Qc cloud or in SqCore frameworks. QcCloud works on per minute resolution (to be able to send MOC orders 20min before MOC), SqCore works on daily resolution only.
        bool _isTradeInSqCore = true;
        // AlgorithmParam comes from QCAlgorithm.QCAlgorithm in SqCore framework
#else
        bool _isTradeInSqCore = false;
        public string AlgorithmParam { get; set; } = "startDate=2002-07-24&endDate=now&startDateAutoCalcMode=WhenFirstTickerAlive&assets=SPY,TLT&weights=60,40&rebFreq=Daily,30d"; // in SqCore, this comes from the backtester environment as Portfolio.AlgorithmParam
#endif

        public bool IsTradeInSqCore { get { return _isTradeInSqCore; } }
        public bool IsTradeInQcCloud { get { return !_isTradeInSqCore; } }

        StartDateAutoCalcMode _startDateAutoCalcMode = StartDateAutoCalcMode.Unknown;
        DateTime _forcedStartDate; // user can force a startdate
        DateTime _startDate = DateTime.MinValue; // real startDate. We expect PV chart to start from here. There can be some warmUp days before that, for which data is needed.
        DateTime _earliestUsableDataDay = DateTime.MinValue;
        TimeSpan _warmUp = TimeSpan.Zero;

        DateTime _forcedEndDate;
        DateTime _endDate = DateTime.MaxValue;

        Dictionary<string, decimal> _weights;
        List<string> _tickers;
        private int _rebalancePeriodDays = -1;  // invalid value. Come from parameters
        private int _lookbackTradingDays = 0;
        private Dictionary<string, Symbol> _symbolsDaily = new Dictionary<string, Symbol>();
        private Dictionary<string, Symbol> _symbolsMinute = new Dictionary<string, Symbol>();
        private Dictionary<string, Symbol> _tradedSymbols  = new Dictionary<string, Symbol>(); // pointer to _symbolsDaily in SqCore, and _symbolsMinute in QcCloud
        private Dictionary<string, List<QcPrice>> _rawCloses = new Dictionary<string, List<QcPrice>>();
        private Dictionary<string, List<QcPrice>> _adjCloses = new Dictionary<string, List<QcPrice>>();
        private Dictionary<string, List<QcDividend>> _dividends = new Dictionary<string, List<QcDividend>>();
        private Dictionary<string, List<QcSplit>> _splits = new Dictionary<string, List<QcSplit>>();
        private Dictionary<string, List<QcPrice>> _rawClosesFromYfLists = new Dictionary<string, List<QcPrice>>();
        private Dictionary<string, Dictionary<DateTime, decimal>> _rawClosesFromYfDicts = new Dictionary<string, Dictionary<DateTime, decimal>>();
        DateTime _bnchmarkStartTime;
        private DateTime _lastRebalance = DateTime.MinValue;
        private Dividends _sliceDividends;
        Symbol? _firstOnDataSymbol = null;

        public override void Initialize()
        {
            _bnchmarkStartTime = DateTime.UtcNow; // for benchmarking how many msec the backtest takes

            NameValueCollection algorithmParamQuery = HttpUtility.ParseQueryString(AlgorithmParam);
            QCAlgorithmUtils.ProcessAlgorithmParam(algorithmParamQuery, out _forcedStartDate, out _forcedEndDate, out _startDateAutoCalcMode);
            ProcessAlgorithmParam(algorithmParamQuery, out _tickers, out _weights, out _rebalancePeriodDays);
            if (_rebalancePeriodDays == -1) // if invalid value (because e.g. AlgorithmParam str is empty)
                _rebalancePeriodDays = 30; // default value
            // _forcedStartDate = new DateTime(2000, 1, 2);
            // _forcedEndDate = new DateTime(2023, 02, 28); // means Local time, not UTC

            // *** Step 1: general initializations
            SetCash(10000000);

            // AddEquity(_longestHistTicker, Resolution.Daily, dataNormalizationMode: DataNormalizationMode.Raw);
            Orders.MarketOnCloseOrder.SubmissionTimeBuffer = TimeSpan.FromMinutes(0.5); // change the submission time threshold of MOC orders

            foreach (string ticker in _tickers)
            {
                Symbol symbolDaily = AddEquity(ticker, Resolution.Daily, dataNormalizationMode: DataNormalizationMode.Raw).Symbol;
                _symbolsDaily.Add(ticker, symbolDaily);
                Securities[symbolDaily].FeeModel = new ConstantFeeModel(0);
                Securities[symbolDaily].SetBuyingPowerModel(new SecurityMarginModel(30m)); // equivalent to security.SetLeverage(). Allows to go 30x leverage of this stock if all 20 GC stock are in position with 50% overleverage
                _rawCloses.Add(ticker, new List<QcPrice>());
                _adjCloses.Add(ticker, new List<QcPrice>());
                _dividends.Add(ticker, new List<QcDividend>());
                _splits.Add(ticker, new List<QcSplit>());
            }

            // *** Only in QcCloud: YF data download.
            _rawClosesFromYfLists = new Dictionary<string, List<QcPrice>>();
            _rawClosesFromYfDicts = new Dictionary<string, Dictionary<DateTime, decimal>>();
            if (IsTradeInQcCloud) // only in QC cloud: we need not only daily, but perMinute symbols too, because we use perMinute symbols for trading.
            {
                foreach (string ticker in _tickers)
                {
                    Symbol symbolMinute = AddEquity(ticker, Resolution.Minute, dataNormalizationMode: DataNormalizationMode.Raw).Symbol;
                    _symbolsMinute.Add(ticker, symbolMinute);
                    Securities[symbolMinute].FeeModel = new ConstantFeeModel(0);
                    Securities[symbolMinute].SetBuyingPowerModel(new SecurityMarginModel(30m)); // equivalent to security.SetLeverage(). Allows to go 30x leverage of this stock if all 20 GC stock are in position with 50% overleverage
                    // Call the DownloadAndProcessData method to get real life close prices from YF
                    QCAlgorithmUtils.DownloadAndProcessYfData(this, ticker, _earliestUsableDataDay, _warmUp, _endDate, ref _rawClosesFromYfLists, ref _rawClosesFromYfDicts);
                }
            }
            _tradedSymbols = IsTradeInSqCore ? _symbolsDaily : _symbolsMinute;

            // *** Step 2: startDate and warmup determination
            //_warmUp = TimeSpan.FromDays(30); // Wind time back X calendar days from start Before the _startDate. It is calendar day. E.g. If strategy need %chgPrevDay, it needs 2 trading day data. Probably set warmup to 2+2+1 = 5+ (for 2 weekend, 1 holiday).
            SetWarmUp(_warmUp);

            _earliestUsableDataDay = QCAlgorithmUtils.StartDateAutoCalculation(_tradedSymbols, _startDateAutoCalcMode, out Symbol? symbolWithEarliestUsableDataDay);
            if (_forcedStartDate == DateTime.MinValue) // auto calculate if user didn't give a forced startDate. Otherwise, we are obliged to use that user specified forced date.
            {
                if (_earliestUsableDataDay < QCAlgorithmUtils.g_earliestQcDay)
                    _earliestUsableDataDay = QCAlgorithmUtils.g_earliestQcDay; // SetStartDate() exception: "Please select a start date after January 1st, 1900."

                _startDate = _earliestUsableDataDay.Add(_warmUp).AddDays(1); // startdate auto calculation we have to add the warmup days
            }
            else
            {
                _earliestUsableDataDay = _forcedStartDate.Subtract(_warmUp); // if the user forces a startDate, the needed _earliestUsableDataDay is X days before, because of warmUp days.
                _startDate = _forcedStartDate;
            }

            // _startDate = new DateTime(2006, 01, 01); // means Local time, not UTC
            Log($"EarliestUsableDataDay: { _earliestUsableDataDay: yyyy-MM-dd}, PV startDate: { _startDate: yyyy-MM-dd}");
            
            // *** Step 3: endDate determination
            if (_forcedEndDate == DateTime.MaxValue)
                _endDate = DateTime.Now;
            else
                _endDate = _forcedEndDate;

            if (_endDate < _startDate)
            {
                string errMsg = $"StartDate ({_startDate:yyyy-MM-dd}) should be earlier then EndDate  ({_endDate:yyyy-MM-dd}).";
                Log(errMsg);
                throw new ArgumentOutOfRangeException(errMsg);
            }
            SetStartDate(_startDate); // by default it is 1998-01-02. If we don't call SetStartDate(), still, the PV value chart will start from 1998
            SetEndDate(_endDate);
            // SetBenchmark("SPY"); // the default benchmark is SPY, which is OK in the cloud. In SqCore, we removed the default SPY benchmark, because we don't need it.
        }

        public static void ProcessAlgorithmParam(NameValueCollection p_AlgorithmParamQuery, out List<string> p_tickers, out Dictionary<string, decimal> p_weights, out int p_rebalancePeriodDays)
        {
            // e.g. _AlgorithmParam = "assets=SPY,TLT&weights=60,40&rebFreq=Daily,10d"

            // Step 1: process tickers and weights
            p_tickers = new();
            p_weights = new();
            p_rebalancePeriodDays = -1;  // invalid value. Come from parameters

            string[] tickers = p_AlgorithmParamQuery.Get("assets")?.Split(',', StringSplitOptions.RemoveEmptyEntries) ?? Array.Empty<string>();
            string[] weights = p_AlgorithmParamQuery.Get("weights")?.Split(',', StringSplitOptions.RemoveEmptyEntries) ?? Array.Empty<string>();
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


            // Step 2: process rebFreq
            string[] rebalanceParams = p_AlgorithmParamQuery.Get("rebFreq")?.Split(',', StringSplitOptions.RemoveEmptyEntries) ?? Array.Empty<string>();
            if (rebalanceParams.Length == 2)
            {
                string rebalancePeriodNumStr = rebalanceParams[1]; // second item is "10d"
                if (rebalancePeriodNumStr.Length != 0)
                {
                    char rebalancePeriodNumStrLastChar = rebalancePeriodNumStr[^1]; // 'd' or 'w' or 'm', but it is not required to be present
                    if (Char.IsLetter(rebalancePeriodNumStrLastChar)) // if 'd/w/m' is given, remove it
                        rebalancePeriodNumStr = rebalancePeriodNumStr[..^1];
                    if (!Int32.TryParse(rebalancePeriodNumStr, out p_rebalancePeriodDays))
                        throw new ArgumentException($"The rebFreq's rebalancePeriodNumStr {rebalancePeriodNumStr} cannot be converted to int.");
                }
            }
        }

        private void TradeLogic() // this is called at 15:40 in QC cloud, and 00:00 in SqCore
        {
            if (IsWarmingUp) // Dont' trade in the warming up period.
                return;

            QCAlgorithmUtils.ApplyDividendMOCAfterClose(Portfolio, _sliceDividends, -1); // We use 'daily' (not perMinute) TradeBars in SqCore.  When OnData() callback comes with this TradeBar, the dividends of that day is already added to the Cash (by the framework). We remove this before trading, and add back after trading. See comment at ApplyDividendMOCAfterClose()

            TradePreProcess();

            Dictionary<string, List<QcPrice>> usedAdjustedClosePrices = GetUsedAdjustedClosePriceData();
            decimal currentPV = PvCalculation(usedAdjustedClosePrices);
            // Log($"PV on day {this.Time.Date.ToString()}: ${Portfolio.TotalPortfolioValue}.");

            string logMessage = $"New positions after close of {this.Time.Date:yyyy-MM-dd}: ";
            string logMessage2 = "Close prices are: ";

            foreach (KeyValuePair<string, Symbol> kvp in _tradedSymbols)
            {
                string ticker = kvp.Key;
                // List<QcPrice> tickerUsedAdjustedClosePrices = usedAdjustedClosePrices[ticker];
                decimal newMarketValue = 0;
                decimal newPosition = 0;
                if (usedAdjustedClosePrices.TryGetValue(ticker, out List<QcPrice> tickerUsedAdjustedClosePrices))
                {
                    if (_weights[ticker] != 0 && tickerUsedAdjustedClosePrices.Count != 0)
                    {
                        newMarketValue = currentPV * _weights[ticker];
                        newPosition = Math.Round((decimal)(newMarketValue / tickerUsedAdjustedClosePrices[(Index)(^1)].Close)); // use the last element
                    }
                }
                decimal positionChange = newPosition - Portfolio[ticker].Quantity;
                if (positionChange != 0) // QC raises Warning if order quantity = 0. So, we don't sent these. "Unable to submit order with id -10 that has zero quantity."
                    MarketOnCloseOrder(kvp.Value, positionChange); // when Order() function returns Portfolio[ticker].Quantity is already changed to newPosition

                logMessage += ticker + ": " + newPosition + "; ";
                logMessage2 += ticker + ": " + ((tickerUsedAdjustedClosePrices.Count != 0) ? tickerUsedAdjustedClosePrices[(Index)(^1)].Close.ToString() : "N/A") + "; "; // use the last element
            }

            QCAlgorithmUtils.ApplyDividendMOCAfterClose(Portfolio, _sliceDividends, 1); // We use 'daily' (not perMinute) TradeBars in SqCore.  When OnData() callback comes with this TradeBar, the dividends of that day is already added to the Cash (by the framework). We remove this before trading, and add back after trading. See comment at ApplyDividendMOCAfterClose()

            logMessage = logMessage.Substring(0, logMessage.Length - 2) + ".";
            logMessage2 = logMessage2.Substring(0, logMessage2.Length - 2) + ".";
            string LogMessageToLog = logMessage + " " + logMessage2 + $" Previous cash: {Portfolio.Cash}. Current PV: {currentPV}.";

            // Log(LogMessageToLog);
            _lastRebalance = this.Time;
        }
        void TradePreProcess()
        {
        }

        /// OnData event is the primary entry point for your algorithm. Each new data point will be pumped in here.
        /// Slice object keyed by symbol containing the stock data
        public override void OnData(Slice slice)
        {
            try
            {
                if (_firstOnDataSymbol == null)
                    _firstOnDataSymbol = slice.Keys[0];

                bool qcCloudRebalanceTime = (slice.Time.Hour == 15 && slice.Time.Minute == 0);
                if (IsTradeInQcCloud && qcCloudRebalanceTime && (slice.Time - _lastRebalance).TotalDays >= _rebalancePeriodDays)
                {
                    TradeLogic();
                    return;
                }

                bool qcCloudDailyDataTime = !(slice.Time.Hour == 0 && slice.Time.Minute == 0);
                if (IsTradeInQcCloud && qcCloudDailyDataTime)
                    return;

                string sliceTime = slice.Time.ToString();
                // Log($"Cash in Portfolio on day {sliceTime}: ${Portfolio.Cash}.");
                // Log($"PV on day {sliceTime}: ${Portfolio.TotalPortfolioValue}.");
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

                        _rawCloses[ticker].Add(new QcPrice() { ReferenceDate = slice.Time.Date.AddDays(-1), Close = (decimal)rawClose });
                        _adjCloses[ticker].Add(new QcPrice() { ReferenceDate = slice.Time.Date.AddDays(-1), Close = (decimal)rawClose });
                    }
                    if (occuredSplit != null)  // Split.SplitOccurred comes on the correct day with slice.Time: 8/25/2022 12:00:00
                    {
                        _splits[ticker].Add(new QcSplit() { ReferenceDate = slice.Time.Date.AddDays(-1), Split = occuredSplit });
                        decimal refPrice = occuredSplit.ReferencePrice;    // Contains RAW price (before Split adjustment). Not used here.
                        decimal splitAdjMultiplicator = occuredSplit.SplitFactor;
                        for (int i = 0; i < _adjCloses[ticker].Count; i++)  // Not-chosen option: if we 'have to' use QC bug 'wrongly-adjusted' rawClose, we can skip the last item. In that case we don't apply the split adjustment to the last item, which is the same day as the day of Split.
                        {
                            _adjCloses[ticker][i].Close *= splitAdjMultiplicator;
                        }
                    }
                }

                bool isPrevDayOpen = Securities[_firstOnDataSymbol].Exchange.Hours.IsDateOpen(this.Time.Date.AddDays(-1)); // dividends for the previous day comes at next day 00:00
                // bool isPrevDayOpen = true; // TEMP. or find a better way to determine this
                if (IsTradeInSqCore && (slice.Time - _lastRebalance).TotalDays >= _rebalancePeriodDays && isPrevDayOpen)
                {
                    _sliceDividends = slice.Dividends;
                    TradeLogic();  // Call rebalance if we reached the rebalance period
                }

                foreach (string ticker in _tickers)
                {
                    var symbol = _symbolsDaily[ticker];
                    Split occuredSplit = (slice.Splits.ContainsKey(symbol) && slice.Splits[symbol].Type == SplitType.SplitOccurred) ? slice.Splits[symbol] : null; // split.Type can be Warning and SplitOccured. Ignore 1-day early Split Warnings. Just use the occured
                    if (slice.Dividends.ContainsKey(symbol))
                    {
                        var dividend = slice.Dividends[symbol];
                        _dividends[ticker].Add(new QcDividend() { ReferenceDate = slice.Time.Date.AddDays(-1), Dividend = dividend });
                        decimal divAdjMultiplicator = 1 - dividend.Distribution / dividend.ReferencePrice;
                        for (int i = 0; i < _adjCloses[ticker].Count; i++)
                        {
                            _adjCloses[ticker][i].Close *= divAdjMultiplicator;
                        }
                        // Log($"Dividend on {slice.Time.ToString()}: {ticker} ${dividend.Distribution} when ReferencePrice was {dividend.ReferencePrice}.");
                    }
                }
            }
            catch (System.Exception e)
            {
                Log($"Error. Exception in OnData(Slice). Slice.Time: {slice.Time}, msg: {e.Message}");
            }
        }

        private Dictionary<string, List<QcPrice>> GetUsedAdjustedClosePriceData()
        {
            Dictionary<string, List<QcPrice>> usedAdjCloses = new Dictionary<string, List<QcPrice>>();
            // iterate over each key-value pair in the dictionary
            foreach (KeyValuePair<string, List<QcPrice>> kvp in _adjCloses) // loop for each tickers
            {
                // get the last lookbackTradingDays items for this key + current raw data from YF if QC cloud is used.
                string ticker = kvp.Key;
                List<QcPrice> qcAdjCloses = kvp.Value;
                List<QcPrice> lastLbTdPrices = new List<QcPrice>();
                int startIndex = IsTradeInSqCore ? Math.Max(0, qcAdjCloses.Count - _lookbackTradingDays - 1) : Math.Max(0, qcAdjCloses.Count - _lookbackTradingDays);
                for (int i = startIndex; i < qcAdjCloses.Count; i++)
                {
                    lastLbTdPrices.Add(new QcPrice() { ReferenceDate = qcAdjCloses[i].ReferenceDate, Close = qcAdjCloses[i].Close });
                }
                if (IsTradeInQcCloud)
                {
                    // add the last lookbackTradingDays items to the new dictionary from YF
                    if (_rawClosesFromYfDicts[ticker].TryGetValue(this.Time.Date, out decimal lastRawClose))
                        lastLbTdPrices.Add(new QcPrice() { ReferenceDate = this.Time.Date, Close = lastRawClose });
                    else // if lastLbTdPrices is empty, then not finding this date is fine. If lastLbTdPrices has at least 1 items, we expect that we find this date.
                        if (qcAdjCloses.Count > 0)
                        throw new Exception($"Cannot find date {this.Time.Date.ToString()} in the YF.");
                }

                usedAdjCloses[ticker] = lastLbTdPrices;
            }

            return usedAdjCloses;
        }

        private decimal PvCalculation(Dictionary<string, List<QcPrice>> p_usedAdjustedClosePrices)
        {
            if (IsTradeInSqCore)
                return Portfolio.TotalPortfolioValue;

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

                        List<QcPrice> usedAdjustedClosePrices = p_usedAdjustedClosePrices[ticker];
                        decimal currentPrice = (usedAdjustedClosePrices.Count != 0) ? usedAdjustedClosePrices[^1].Close : 0;    // get the last element
                        currentPV += quantity * currentPrice;
                    }
                }
            }
            return currentPV;
        }
        public override void OnEndOfAlgorithm()
        {
            Log($"OnEndOfAlgorithm(): Backtest time: {(DateTime.UtcNow - _bnchmarkStartTime).TotalMilliseconds}ms");
        }

    }
}