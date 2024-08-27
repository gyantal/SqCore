#define TradeInSqCore

#region imports
using System;
using System.Collections.Generic;
using QuantConnect.Parameters;
using QuantConnect.Data;
using QuantConnect.Data.Market;
using QuantConnect.Orders.Fees;
using QuantConnect.Scheduling;
using QuantConnect.Securities;
using System.Web;
using System.Collections.Specialized;
#endregion

// This is a backtesting algorithm of CXO's Value sub-strategy: https://www.cxoadvisory.com/value-strategy/.
namespace QuantConnect.Algorithm.CSharp
{

    public class SqCxoValue : QCAlgorithm
    {
#if TradeInSqCore // 2 simulation environments. We backtest in Qc cloud or in SqCore frameworks. QcCloud works on per minute resolution (to be able to send MOC orders 20min before MOC), SqCore works on daily resolution only.
        bool _isTradeInSqCore = true;
        // AlgorithmParam comes from QCAlgorithm.QCAlgorithm in SqCore framework
#else
        bool _isTradeInSqCore = false;
        public string AlgorithmParam { get; set; } = "startDate=2009-01-05&endDate=now"; // in SqCore, this comes from the backtester environment as Portfolio.AlgorithmParam
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

        List<string> _tickers = new List<string>{"TLT", "LQD", "SPY"};
        Dictionary<DateTime, Dictionary<string, decimal>> _allocationSchedule;
        Dictionary<string, decimal> _lastValidValueWeights;
        private Dictionary<string, Symbol> _symbolsDaily = new Dictionary<string, Symbol>(); // on QcCloud, we need both perMinute and perDaily. perDaily is needed because we collect the past Daily history for momemtum calculation
        private Dictionary<string, Symbol> _symbolsMinute = new Dictionary<string, Symbol>();
        private Dictionary<string, Symbol> _tradedSymbols = new Dictionary<string, Symbol>(); // pointer to _symbolsDaily in SqCore, and _symbolsMinute in QcCloud
        private Dictionary<string, List<QcPrice>> _rawCloses = new Dictionary<string, List<QcPrice>>();
        private Dictionary<string, List<QcPrice>> _adjCloses = new Dictionary<string, List<QcPrice>>();
        private Dictionary<string, List<QcDividend>> _dividends = new Dictionary<string, List<QcDividend>>();
        private Dictionary<string, List<QcSplit>> _splits = new Dictionary<string, List<QcSplit>>();
        private Dictionary<string, Dictionary<DateTime, decimal>>? _rawClosesFromYfDicts = null;
         DateTime _bnchmarkStartTime;
        private Dividends _sliceDividends;
        bool _isEndOfMonth = false; // use Qc Schedule.On() mechanism to calculate the last trading day of the month (because of holidays complications), even using it in SqCore
        Symbol? _firstOnDataSymbol = null;

        public override void Initialize()
        {
            _bnchmarkStartTime = DateTime.UtcNow;

            NameValueCollection algorithmParamQuery = HttpUtility.ParseQueryString(AlgorithmParam);
            QCAlgorithmUtils.ProcessAlgorithmParam(algorithmParamQuery, out _forcedStartDate, out _forcedEndDate, out _startDateAutoCalcMode);

            // *** Step 1: general initializations
            SetCash(10000000);
            Orders.MarketOnCloseOrder.SubmissionTimeBuffer = TimeSpan.FromMinutes(0.5); // change the submission time threshold of MOC orders

            foreach (string ticker in _tickers)
            {
                Symbol symbolDaily = AddEquity(ticker, Resolution.Daily, dataNormalizationMode: DataNormalizationMode.Raw).Symbol;
                _symbolsDaily.Add(ticker, symbolDaily);
                Securities[symbolDaily].FeeModel = new ConstantFeeModel(0);
                Securities[symbolDaily].SetBuyingPowerModel(new SecurityMarginModel(30m)); // equivalent to security.SetLeverage().
                _rawCloses.Add(ticker, new List<QcPrice>());
                _adjCloses.Add(ticker, new List<QcPrice>());
                _dividends.Add(ticker, new List<QcDividend>());
                _splits.Add(ticker, new List<QcSplit>());
            }

            // *** Only in QcCloud: YF data download. Part 1
            if (IsTradeInQcCloud) // only in QC cloud: we need not only daily, but perMinute symbols too, because we use perMinute symbols for trading.
            {
                foreach (string ticker in _tickers)
                {
                    Symbol symbolMinute = AddEquity(ticker, Resolution.Minute, dataNormalizationMode: DataNormalizationMode.Raw).Symbol;
                    _symbolsMinute.Add(ticker, symbolMinute);
                    Securities[symbolMinute].FeeModel = new ConstantFeeModel(0);
                    Securities[symbolMinute].SetBuyingPowerModel(new SecurityMarginModel(30m)); // equivalent to security.SetLeverage().
                }
            }
            _tradedSymbols = IsTradeInSqCore ? _symbolsDaily : _symbolsMinute;

            // *** Step 2: startDate and warmup determination
            _warmUp = TimeSpan.FromDays(0); // Wind time back X calendar days from start Before the _startDate. It is calendar day. E.g. If strategy need %chgPrevDay, it needs 2 trading day data. Probably set warmup to 2+2+1 = 5+ (for 2 weekend, 1 holiday).
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

            Log($"EarliestUsableDataDay: {_earliestUsableDataDay: yyyy-MM-dd}, PV startDate: {_startDate: yyyy-MM-dd}");

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
            if (!SqBacktestConfig.SqDailyTradingAtMOC)
                _startDate = _startDate.AddDays(1); // Original QC behaviour: first PV will be StartDate() -1, and it uses previous day Close prices on StartDate:00:00 morning. We don't want that. So, increase the date by 1.
            SetStartDate(_startDate); // by default it is 1998-01-02. If we don't call SetStartDate(), still, the PV value chart will start from 1998
            SetEndDate(_endDate);

            _allocationSchedule = SqCxoCommon.AllocationByMonth();

            // *** Only in QcCloud: YF data download. Part 2
            if (IsTradeInQcCloud)
                QCAlgorithmUtils.DownloadAndProcessYfData(this, _tickers, _earliestUsableDataDay, _warmUp, _endDate, out _rawClosesFromYfDicts);

            Symbol tradingScheduleSymbol = symbolWithEarliestUsableDataDay; // "SPY" is the best candidate for trading schedule, but if "SPY" is not in the asset universe, we use the one with the longest history
            if (tradingScheduleSymbol != null) // if AlgorithmParam is empty, then there is no symbol at all. It is OK to not schedule trading.
                Schedule.On(DateRules.MonthEnd(tradingScheduleSymbol), TimeRules.BeforeMarketClose(tradingScheduleSymbol, 20), () => // The last trading day of each month has to be determined. The rebalance will take place on this day.
                {
                    if (IsWarmingUp) // Dont' trade in the warming up period.
                        return;
                    _isEndOfMonth = true;
                    if (IsTradeInQcCloud)
                        TradeLogic();
                });
        }

        private void TradeLogic() // this is called at 15:40 in QC cloud, and 00:00 in SqCore
        {
            if (IsWarmingUp) // Dont' trade in the warming up period.
                return;

            if(IsTradeInSqCore && !SqBacktestConfig.SqDailyTradingAtMOC)
                QCAlgorithmUtils.ApplyDividendMOCAfterClose(Portfolio, _sliceDividends, -1); // We use 'daily' (not perMinute) TradeBars in SqCore.  When OnData() callback comes with this TradeBar, the dividends of that day is already added to the Cash (by the framework). We remove this before trading, and add back after trading. See comment at ApplyDividendMOCAfterClose()

            TradePreProcess();

            Dictionary<string, List<QcPrice>> usedAdjustedClosePrices = GetUsedAdjustedClosePriceData();
            Dictionary<string, decimal> nextMonthWeights = HistPerfCalc();
            decimal currentPV = PvCalculation(usedAdjustedClosePrices);

            string logMessage = $"New positions after close of {this.Time.Date:yyyy-MM-dd}: ";
            string logMessage2 = "Close prices are: ";

            foreach (KeyValuePair<string, Symbol> kvp in _tradedSymbols)
            {
                string ticker = kvp.Key;
                decimal newMarketValue = 0;
                decimal newPosition = 0;
                decimal closePrice = 0;
                if (usedAdjustedClosePrices.TryGetValue(ticker, out List<QcPrice> tickerUsedAdjustedClosePrices) && tickerUsedAdjustedClosePrices?.Count > 0)
                    closePrice = tickerUsedAdjustedClosePrices[^1].Close;

                if (nextMonthWeights[ticker] != 0 && closePrice != 0)
                {
                    newMarketValue = currentPV * nextMonthWeights[ticker];
                    newPosition = Math.Round((decimal)(newMarketValue / closePrice)); // use the last element
                }
                decimal positionChange = newPosition - Portfolio[ticker].Quantity;
                if (positionChange != 0) // QC raises Warning if order quantity = 0. So, we don't sent these. "Unable to submit order with id -10 that has zero quantity."
                    MarketOnCloseOrder(kvp.Value, positionChange); // when Order() function returns Portfolio[ticker].Quantity is already changed to newPosition

                logMessage += ticker + ": " + newPosition + "; ";
                logMessage2 += ticker + ": " + ((tickerUsedAdjustedClosePrices.Count != 0) ? tickerUsedAdjustedClosePrices[(Index)(^1)].Close.ToString() : "N/A") + "; "; // use the last element
            }

            if(IsTradeInSqCore && !SqBacktestConfig.SqDailyTradingAtMOC)
                QCAlgorithmUtils.ApplyDividendMOCAfterClose(Portfolio, _sliceDividends, 1); // We use 'daily' (not perMinute) TradeBars in SqCore.  When OnData() callback comes with this TradeBar, the dividends of that day is already added to the Cash (by the framework). We remove this before trading, and add back after trading. See comment at ApplyDividendMOCAfterClose()

            logMessage = logMessage.Substring(0, logMessage.Length - 2) + ".";
            logMessage2 = logMessage2.Substring(0, logMessage2.Length - 2) + ".";
            string LogMessageToLog = logMessage + " " + logMessage2 + $" Previous cash: {Portfolio.Cash}. Current PV: {currentPV}.";

            // Log(LogMessageToLog);
            _isEndOfMonth = false;
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

                if (IsTradeInQcCloud)
                {
                    bool isDataPerMinute = !(slice.Time.Hour == 0 && slice.Time.Minute == 0);
                    if (isDataPerMinute)
                        return; // if in the SqCloud and we receive perMinute data, return, because we don't need it.
                }
                // string sliceTime = slice.Time.ToString();
                // Log($"Cash in Portfolio on day {sliceTime}: ${Portfolio.Cash}.");
                // Log($"PV on day {sliceTime}: ${Portfolio.TotalPortfolioValue}.");
                // Log($"OnData(Slice). Slice.Time: {slice.Time}, this.Time: {sliceTime}");
                foreach (string ticker in _tickers)
                {
                    Symbol symbol = _symbolsDaily[ticker];
                    Split occuredSplit = (slice.Splits.ContainsKey(symbol) && slice.Splits[symbol].Type == SplitType.SplitOccurred) ? slice.Splits[symbol] : null; // split.Type can be Warning and SplitOccured. Ignore 1-day early Split Warnings. Just use the occured

                    decimal? rawClose = null;
                    if (slice.Bars.TryGetValue(symbol, out TradeBar bar))
                        rawClose = bar.Close; // Probably bug in QC: if there is a Split for the daily bar, then QC SplitAdjust that bar, even though we asked RAW data. QC only does it for the Split day. We can undo it, because the Split.ReferencePrice has the RAW price.
                    if (rawClose != null) // we have a split or dividend on Sunday, but there is no bar, so there is no price, which is fine
                    {
                        if (occuredSplit != null)
                            rawClose = occuredSplit.ReferencePrice; // ReferencePrice is RAW, not adjusted. Fixing QC bug of giving SplitAdjusted bar on Split day.
                                                                    // clPrice = slice.Splits[_symbol].Price; // Price is an alias to Value. Value is this: For streams of data this is the price now, for OHLC packets this is the closing price.            
                        if (SqBacktestConfig.SqDailyTradingAtMOC) // SqDailyTradingAtMOC sends price at 16:00, which is right. No need the change. Without it, price comes 00:00 next morning, so we adjust it back.
                        {
                            _rawCloses[ticker].Add(new QcPrice() { ReferenceDate = slice.Time.Date, Close = (decimal)rawClose });
                            _adjCloses[ticker].Add(new QcPrice() { ReferenceDate = slice.Time.Date, Close = (decimal)rawClose });
                        }
                        else
                        {
                            _rawCloses[ticker].Add(new QcPrice() { ReferenceDate = slice.Time.Date.AddDays(-1), Close = (decimal)rawClose });
                            _adjCloses[ticker].Add(new QcPrice() { ReferenceDate = slice.Time.Date.AddDays(-1), Close = (decimal)rawClose });
                        }
                    }
                    if (occuredSplit != null)  // Split.SplitOccurred comes on the correct day with slice.Time: 8/25/2022 12:00:00
                    {
                        if (SqBacktestConfig.SqDailyTradingAtMOC) // SqDailyTradingAtMOC sends price at 16:00, which is right. No need the change. Without it, price comes 00:00 next morning, so we adjust it back.
                            _splits[ticker].Add(new QcSplit() { ReferenceDate = slice.Time.Date, Split = occuredSplit });
                        else
                            _splits[ticker].Add(new QcSplit() { ReferenceDate = slice.Time.Date.AddDays(-1), Split = occuredSplit });
                        decimal refPrice = occuredSplit.ReferencePrice;    // Contains RAW price (before Split adjustment). Not used here.
                        decimal splitAdjMultiplicator = occuredSplit.SplitFactor;
                        for (int i = 0; i < _adjCloses[ticker].Count; i++)  // Not-chosen option: if we 'have to' use QC bug 'wrongly-adjusted' rawClose, we can skip the last item. In that case we don't apply the split adjustment to the last item, which is the same day as the day of Split.
                            _adjCloses[ticker][i].Close *= splitAdjMultiplicator;
                    }
                }

                if (IsTradeInSqCore && _isEndOfMonth)
                {
                    _sliceDividends = slice.Dividends;
                    TradeLogic();
                }

                foreach (string ticker in _tickers)
                {
                    Symbol symbol = _symbolsDaily[ticker];
                    Split occuredSplit = (slice.Splits.ContainsKey(symbol) && slice.Splits[symbol].Type == SplitType.SplitOccurred) ? slice.Splits[symbol] : null; // split.Type can be Warning and SplitOccured. Ignore 1-day early Split Warnings. Just use the occured
                    if (slice.Dividends.ContainsKey(symbol))
                    {
                        Dividend dividend = slice.Dividends[symbol];
                        if (SqBacktestConfig.SqDailyTradingAtMOC) // SqDailyTradingAtMOC sends price at 16:00, which is right. No need the change. Without it, price comes 00:00 next morning, so we adjust it back.
                            _dividends[ticker].Add(new QcDividend() { ReferenceDate = slice.Time.Date, Dividend = dividend });
                        else
                            _dividends[ticker].Add(new QcDividend() { ReferenceDate = slice.Time.Date.AddDays(-1), Dividend = dividend });
                        decimal divAdjMultiplicator = 1 - dividend.Distribution / dividend.ReferencePrice;
                        for (int i = 0; i < _adjCloses[ticker].Count; i++)
                            _adjCloses[ticker][i].Close *= divAdjMultiplicator;

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
                int startIndex = IsTradeInSqCore ? Math.Max(0, qcAdjCloses.Count - 1) : Math.Max(0, qcAdjCloses.Count);
                for (int i = startIndex; i < qcAdjCloses.Count; i++)
                    lastLbTdPrices.Add(new QcPrice() { ReferenceDate = qcAdjCloses[i].ReferenceDate, Close = qcAdjCloses[i].Close });
                if (IsTradeInQcCloud)
                {
                    // add the last lookbackTradingDays items to the new dictionary from YF
                    if (_rawClosesFromYfDicts != null && _rawClosesFromYfDicts[ticker].TryGetValue(this.Time.Date, out decimal lastRawClose))
                        lastLbTdPrices.Add(new QcPrice() { ReferenceDate = this.Time.Date, Close = lastRawClose });
                    else // if lastLbTdPrices is empty, then not finding this date is fine. If lastLbTdPrices has at least 1 items, we expect that we find this date.
                        if (qcAdjCloses.Count > 0)
                        throw new Exception($"Cannot find date {this.Time.Date.ToString()} in the YF.");
                }

                usedAdjCloses[ticker] = lastLbTdPrices;
            }

            return usedAdjCloses;
        }

        private Dictionary<string, decimal> HistPerfCalc()
        {
            // Get the last trading day
            DateTime lastTradingDay = this.Time.Date;

            // Initialize variable to store the matched allocation
            Dictionary<string, decimal> matchedAllocation = null;

            // Search for the allocation that matches the last trading day's month and year
            foreach (KeyValuePair<DateTime, Dictionary<string, decimal>> allocation in _allocationSchedule)
            {
                if (allocation.Key.Month == lastTradingDay.Month && allocation.Key.Year == lastTradingDay.Year)
                {
                    matchedAllocation = allocation.Value;
                    _lastValidValueWeights = matchedAllocation;
                    break;
                }
            }

            // Initialize the dictionary to store next month's weights without "CASH"
            Dictionary<string, decimal> nextMonthWeights = new Dictionary<string, decimal>();

            // If a matching allocation was found, remove the "CASH" key and populate nextMonthWeights
            if (matchedAllocation != null)
            {
                foreach (KeyValuePair<string, decimal> kvp in matchedAllocation)
                {
                    if (kvp.Key != "CASH")
                        nextMonthWeights.Add(kvp.Key, kvp.Value);
                }
            }
            else
                nextMonthWeights = _lastValidValueWeights;

            return nextMonthWeights;
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