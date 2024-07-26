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

// This is a backtesting algorithm of CXO's combined strategy: https://www.cxoadvisory.com/strategies/.
namespace QuantConnect.Algorithm.CSharp
{

    public class SqCxoCombined : QCAlgorithm
    {
#if TradeInSqCore // 2 simulation environments. We backtest in Qc cloud or in SqCore frameworks. QcCloud works on per minute resolution (to be able to send MOC orders 20min before MOC), SqCore works on daily resolution only.
        bool _isTradeInSqCore = true;
        // AlgorithmParam comes from QCAlgorithm.QCAlgorithm in SqCore framework
#else
        bool _isTradeInSqCore = false;
        public string AlgorithmParam { get; set; } = "startDate=2009-01-05&endDate=now&assets=SPY,DBC,EMB,EFA,GLD,IWM,TLT,VNQ&lookbackMonth=4&noETFs=2&subStratWeights=50,50"; // in SqCore, this comes from the backtester environment as Portfolio.AlgorithmParam
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

        List<string> _tickersMom;
        List<string> _tickersValue = new List<string>{"TLT", "LQD", "SPY"};
        List<string> _tickersCombined;
        Dictionary<DateTime, Dictionary<string, decimal>> _allocationSchedule;
        private int _lookbackTradingDays;
        private int _numberOfEtfsSelected;
        private double[] _subStratWeights;
        private Dictionary<string, Symbol> _symbolsDaily = new Dictionary<string, Symbol>(); // on QcCloud, we need both perMinute and perDaily. perDaily is needed because we collect the past Daily history for momemtum calculation
        private Dictionary<string, Symbol> _symbolsMinute = new Dictionary<string, Symbol>();
        private Dictionary<string, Symbol> _tradedSymbols = new Dictionary<string, Symbol>(); // pointer to _symbolsDaily in SqCore, and _symbolsMinute in QcCloud
        private Dictionary<string, List<QcPrice>> _rawCloses = new Dictionary<string, List<QcPrice>>();
        private Dictionary<string, List<QcPrice>> _adjCloses = new Dictionary<string, List<QcPrice>>();
        private Dictionary<string, List<QcDividend>> _dividends = new Dictionary<string, List<QcDividend>>();
        private Dictionary<string, List<QcSplit>> _splits = new Dictionary<string, List<QcSplit>>();
        private Dictionary<string, Dictionary<DateTime, decimal>>? _rawClosesFromYfDicts = null;
        private int _lookbackMonths;
        DateTime _bnchmarkStartTime;
        private Dividends _sliceDividends;
        bool _isEndOfMonth = false; // use Qc Schedule.On() mechanism to calculate the last trading day of the month (because of holidays complications), even using it in SqCore
        Symbol? _firstOnDataSymbol = null;

        public override void Initialize()
        {
            _bnchmarkStartTime = DateTime.UtcNow;

            NameValueCollection algorithmParamQuery = HttpUtility.ParseQueryString(AlgorithmParam);
            QCAlgorithmUtils.ProcessAlgorithmParam(algorithmParamQuery, out _forcedStartDate, out _forcedEndDate, out _startDateAutoCalcMode);
            ProcessAlgorithmParam(algorithmParamQuery, out _tickersMom, out _lookbackMonths, out _numberOfEtfsSelected, out _subStratWeights);
            _lookbackTradingDays = _lookbackMonths * 23;

            // *** Step 1: general initializations
            SetCash(10000000);
            Orders.MarketOnCloseOrder.SubmissionTimeBuffer = TimeSpan.FromMinutes(0.5); // change the submission time threshold of MOC orders

            HashSet<string> tickerSet = new HashSet<string>(_tickersMom);
            foreach (string ticker in _tickersValue)
                tickerSet.Add(ticker);

            _tickersCombined = new List<string>(tickerSet);

            foreach (string ticker in _tickersCombined)
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
                foreach (string ticker in _tickersCombined)
                {
                    Symbol symbolMinute = AddEquity(ticker, Resolution.Minute, dataNormalizationMode: DataNormalizationMode.Raw).Symbol;
                    _symbolsMinute.Add(ticker, symbolMinute);
                    Securities[symbolMinute].FeeModel = new ConstantFeeModel(0);
                    Securities[symbolMinute].SetBuyingPowerModel(new SecurityMarginModel(30m)); // equivalent to security.SetLeverage().
                }
            }
            _tradedSymbols = IsTradeInSqCore ? _symbolsDaily : _symbolsMinute;

            // *** Step 2: startDate and warmup determination
            _warmUp = TimeSpan.FromDays(150); // Wind time back X calendar days from start Before the _startDate. It is calendar day. E.g. If strategy need %chgPrevDay, it needs 2 trading day data. Probably set warmup to 2+2+1 = 5+ (for 2 weekend, 1 holiday).
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

            // *** Only in QcCloud: YF data download. Part 2
            if (IsTradeInQcCloud)
                QCAlgorithmUtils.DownloadAndProcessYfData(this, _tickersCombined, _earliestUsableDataDay, _warmUp, _endDate, out _rawClosesFromYfDicts);

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

        public static void ProcessAlgorithmParam(NameValueCollection p_AlgorithmParamQuery, out List<string> p_tickersMom, out int p_lookbackMonths, out int p_numberOfEtfsSelected, out double[] p_subStratWeights)
        {
            // e.g. _AlgorithmParam = "startDate=2009-01-05&endDate=now&assets=SPY,DBC,EMB,EFA,GLD,IWM,TLT,VNQ&lookbackMonth=4&noETFs=2"
            string[] tickersMom = p_AlgorithmParamQuery.Get("assets")?.Split(',', StringSplitOptions.RemoveEmptyEntries) ?? Array.Empty<string>();
            p_tickersMom = new List<string>(tickersMom);

            string lookbackTradingDays = p_AlgorithmParamQuery.Get("lookbackMonth");
            if (!int.TryParse(lookbackTradingDays, out p_lookbackMonths))
                p_lookbackMonths = 4;

            string numberOfEtfsSelected = p_AlgorithmParamQuery.Get("noETFs");
            if (!int.TryParse(numberOfEtfsSelected, out p_numberOfEtfsSelected))
                p_numberOfEtfsSelected = 2;

            // Parse sub-strategy weights. First parameter: Momentum, second parameter: Value
            string[] subStratWeightsStringArray = p_AlgorithmParamQuery.Get("subStratWeights")?.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries) ?? Array.Empty<string>();
            p_subStratWeights = new double[2];

            if (subStratWeightsStringArray.Length == 2 &&
                double.TryParse(subStratWeightsStringArray[0], out double weight1) &&
                double.TryParse(subStratWeightsStringArray[1], out double weight2))
            {
                // Normalize weights
                double total = weight1 + weight2;
                p_subStratWeights[0] = weight1 / total;
                p_subStratWeights[1] = weight2 / total;
            }
            else
            {
                // Default weights
                p_subStratWeights[0] = 0.5;
                p_subStratWeights[1] = 0.5;
            }
        }

        private void TradeLogic() // this is called at 15:40 in QC cloud, and 00:00 in SqCore
        {
            if (IsWarmingUp) // Dont' trade in the warming up period.
                return;

            if(IsTradeInSqCore && !SqBacktestConfig.SqDailyTradingAtMOC)
                QCAlgorithmUtils.ApplyDividendMOCAfterClose(Portfolio, _sliceDividends, -1); // We use 'daily' (not perMinute) TradeBars in SqCore.  When OnData() callback comes with this TradeBar, the dividends of that day is already added to the Cash (by the framework). We remove this before trading, and add back after trading. See comment at ApplyDividendMOCAfterClose()

            TradePreProcess();

            Dictionary<string, List<QcPrice>> usedAdjustedClosePrices = GetUsedAdjustedClosePriceData();
            Dictionary<string, decimal> nextMonthWeights = HistPerfCalc(usedAdjustedClosePrices);
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
                foreach (string ticker in _tickersCombined)
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

                foreach (string ticker in _tickersCombined)
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
                int startIndex = IsTradeInSqCore ? Math.Max(0, qcAdjCloses.Count - _lookbackTradingDays - 1) : Math.Max(0, qcAdjCloses.Count - _lookbackTradingDays);
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
        public Dictionary<string, decimal> HistPerfCalc(Dictionary<string, List<QcPrice>> p_usedAdjustedClosePrices)
        {
            // Calculate weights for the Momentum sub-strategy
            Dictionary<string, decimal> momentumWeights = HistPerfCalcMomentum(p_usedAdjustedClosePrices);

            // Calculate weights for the Value sub-strategy
            Dictionary<string, decimal> valueWeights = HistPerfCalcValue();

            // Initialize the combined weights dictionary
            Dictionary<string, decimal> combinedWeights = new Dictionary<string, decimal>();

            // Combine the weights based on _subStratWeights
            foreach (string ticker in _tickersCombined)
            {
                decimal momentumWeight = momentumWeights.ContainsKey(ticker) ? momentumWeights[ticker] : 0;
                decimal valueWeight = valueWeights.ContainsKey(ticker) ? valueWeights[ticker] : 0;

                combinedWeights[ticker] = momentumWeight * (decimal)_subStratWeights[0] + valueWeight * (decimal)_subStratWeights[1];
            }

            return combinedWeights;
        }
        private Dictionary<string, decimal> HistPerfCalcMomentum(Dictionary<string, List<QcPrice>> p_usedAdjustedClosePrices)
        {
            Dictionary<string, decimal> relativeMomentums = new Dictionary<string, decimal>();

            foreach (KeyValuePair<string, List<QcPrice>> kvp in p_usedAdjustedClosePrices)
            {
                string key = kvp.Key;

                // Only process tickers that are in _tickersMom
                if (!_tickersMom.Contains(key))
                    continue;

                List<QcPrice> usedAdjustedClosePrice = kvp.Value;
                decimal relMom = -99;

                if (usedAdjustedClosePrice.Count > 0)
                {
                    // Get the date "lookback" months ago
                    DateTime lookbackMonthsAgo = Time.Date.AddMonths(-_lookbackMonths);
                    // Adjust to the last day of that month
                    lookbackMonthsAgo = new DateTime(lookbackMonthsAgo.Year, lookbackMonthsAgo.Month, DateTime.DaysInMonth(lookbackMonthsAgo.Year, lookbackMonthsAgo.Month));

                    // Find the closest date in the list to four months ago
                    QcPrice closestPrice = BinarySearchClosestDate(usedAdjustedClosePrice, lookbackMonthsAgo);

                    if (closestPrice != null)
                        relMom = usedAdjustedClosePrice[usedAdjustedClosePrice.Count - 1].Close / closestPrice.Close - 1; // Calculate the relative momentum
                }

                relativeMomentums.Add(key, relMom);
            }

            // Sorting and ranking logic
            List<KeyValuePair<string, decimal>> sortedMomentums = new List<KeyValuePair<string, decimal>>(relativeMomentums);
            sortedMomentums.Sort((x, y) => y.Value.CompareTo(x.Value));
            Dictionary<string, int> rankDict = new Dictionary<string, int>();

            for (int i = 0; i < sortedMomentums.Count; i++)
                rankDict[sortedMomentums[i].Key] = i + 1;

            // Determine ETF selection
            Dictionary<string, bool> resultDict = new Dictionary<string, bool>();
            int posRelMomNum = 0;
            foreach (decimal v in relativeMomentums.Values)
            {
                if (v > 0)
                    posRelMomNum++;
            }
            int cashSelector = (posRelMomNum < _numberOfEtfsSelected) ? 1 : 0;

            foreach (KeyValuePair<string, int> kvp in rankDict)
                resultDict[kvp.Key] = kvp.Value <= _numberOfEtfsSelected - cashSelector;

            // Calculate weights for next month
            Dictionary<string, decimal> nextMonthWeightsMom = new Dictionary<string, decimal>();
            decimal playedWeight = 1m / _numberOfEtfsSelected;

            foreach (string key in p_usedAdjustedClosePrices.Keys)
                if (_tickersMom.Contains(key))
                    nextMonthWeightsMom[key] = resultDict[key] ? playedWeight : 0;

            return nextMonthWeightsMom;
        }

        private Dictionary<string, decimal> HistPerfCalcValue()
        {
            AllocationByMonth();

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
                    break;
                }
            }

            // Initialize the dictionary to store next month's weights without "CASH"
            Dictionary<string, decimal> nextMonthWeightsValue = new Dictionary<string, decimal>();

            // If a matching allocation was found, remove the "CASH" key and populate nextMonthWeights
            if (matchedAllocation != null)
            {
                foreach (KeyValuePair<string, decimal> kvp in matchedAllocation)
                {
                    if (kvp.Key != "CASH")
                        nextMonthWeightsValue.Add(kvp.Key, kvp.Value);
                }
            }

            return nextMonthWeightsValue;
        }

        private QcPrice BinarySearchClosestDate(List<QcPrice> p_prices, DateTime p_targetDate)
        {
            int left = 0;
            int right = p_prices.Count - 1;

            while (left <= right)
            {
                int mid = left + (right - left) / 2;

                if (p_prices[mid].ReferenceDate == p_targetDate)
                    return p_prices[mid];

                if (p_prices[mid].ReferenceDate < p_targetDate)
                    left = mid + 1;
                else
                    right = mid - 1;
            }

            // If not found exactly, return the closest price before the target date
            return right >= 0 ? p_prices[right] : null;
        }

        private decimal PvCalculation(Dictionary<string, List<QcPrice>> p_usedAdjustedClosePrices)
        {
            if (IsTradeInSqCore)
                return Portfolio.TotalPortfolioValue;

            decimal currentPV = 0m;
            decimal cashValue = Portfolio.Cash;
            currentPV = cashValue;

            foreach (string ticker in _tickersCombined)
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
        private void AllocationByMonth()
        {
            // Set up the allocation schedule
            _allocationSchedule = new Dictionary<DateTime, Dictionary<string, decimal>>
            {
                { new DateTime(2024, 6, 30 ), new Dictionary<string, decimal> { { "TLT", 0.0m }, { "LQD", 0.0m }, { "SPY", 0.0m }, { "CASH", 1.0m } } },
                { new DateTime(2024, 5, 31 ), new Dictionary<string, decimal> { { "TLT", 0.0m }, { "LQD", 0.0m }, { "SPY", 0.0m }, { "CASH", 1.0m } } },
                { new DateTime(2024, 4, 30 ), new Dictionary<string, decimal> { { "TLT", 0.0m }, { "LQD", 0.0m }, { "SPY", 0.0m }, { "CASH", 1.0m } } },
                { new DateTime(2024, 3, 31 ), new Dictionary<string, decimal> { { "TLT", 0.0m }, { "LQD", 0.0m }, { "SPY", 0.0m }, { "CASH", 1.0m } } },
                { new DateTime(2024, 2, 29 ), new Dictionary<string, decimal> { { "TLT", 0.0m }, { "LQD", 0.0m }, { "SPY", 0.0m }, { "CASH", 1.0m } } },
                { new DateTime(2024, 1, 31 ), new Dictionary<string, decimal> { { "TLT", 0.0m }, { "LQD", 0.0m }, { "SPY", 0.0m }, { "CASH", 1.0m } } },
                { new DateTime(2023, 12, 31 ), new Dictionary<string, decimal> { { "TLT", 0.0m }, { "LQD", 0.0m }, { "SPY", 0.0m }, { "CASH", 1.0m } } },
                { new DateTime(2023, 11, 30 ), new Dictionary<string, decimal> { { "TLT", 0.0m }, { "LQD", 0.0m }, { "SPY", 0.0m }, { "CASH", 1.0m } } },
                { new DateTime(2023, 10, 31 ), new Dictionary<string, decimal> { { "TLT", 0.0m }, { "LQD", 0.0m }, { "SPY", 0.0m }, { "CASH", 1.0m } } },
                { new DateTime(2023, 9, 30 ), new Dictionary<string, decimal> { { "TLT", 0.0m }, { "LQD", 0.0m }, { "SPY", 0.0m }, { "CASH", 1.0m } } },
                { new DateTime(2023, 8, 31 ), new Dictionary<string, decimal> { { "TLT", 0.0m }, { "LQD", 0.0m }, { "SPY", 0.0m }, { "CASH", 1.0m } } },
                { new DateTime(2023, 7, 31 ), new Dictionary<string, decimal> { { "TLT", 0.0m }, { "LQD", 0.0m }, { "SPY", 0.0m }, { "CASH", 1.0m } } },
                { new DateTime(2023, 6, 30 ), new Dictionary<string, decimal> { { "TLT", 0.0m }, { "LQD", 0.0m }, { "SPY", 0.0m }, { "CASH", 1.0m } } },
                { new DateTime(2023, 5, 31 ), new Dictionary<string, decimal> { { "TLT", 0.0m }, { "LQD", 0.0m }, { "SPY", 1.0m }, { "CASH", 0.0m } } },
                { new DateTime(2023, 4, 30 ), new Dictionary<string, decimal> { { "TLT", 0.0m }, { "LQD", 0.0m }, { "SPY", 1.0m }, { "CASH", 0.0m } } },
                { new DateTime(2023, 3, 31 ), new Dictionary<string, decimal> { { "TLT", 0.0m }, { "LQD", 0.0m }, { "SPY", 1.0m }, { "CASH", 0.0m } } },
                { new DateTime(2023, 2, 28 ), new Dictionary<string, decimal> { { "TLT", 0.0m }, { "LQD", 0.0m }, { "SPY", 1.0m }, { "CASH", 0.0m } } },
                { new DateTime(2023, 1, 31 ), new Dictionary<string, decimal> { { "TLT", 0.0m }, { "LQD", 0.0m }, { "SPY", 1.0m }, { "CASH", 0.0m } } },
                { new DateTime(2022, 12, 31 ), new Dictionary<string, decimal> { { "TLT", 0.0m }, { "LQD", 0.0m }, { "SPY", 1.0m }, { "CASH", 0.0m } } },
                { new DateTime(2022, 11, 30 ), new Dictionary<string, decimal> { { "TLT", 0.0m }, { "LQD", 0.0m }, { "SPY", 1.0m }, { "CASH", 0.0m } } },
                { new DateTime(2022, 10, 31 ), new Dictionary<string, decimal> { { "TLT", 0.0m }, { "LQD", 0.0m }, { "SPY", 1.0m }, { "CASH", 0.0m } } },
                { new DateTime(2022, 9, 30 ), new Dictionary<string, decimal> { { "TLT", 0.0m }, { "LQD", 0.0m }, { "SPY", 1.0m }, { "CASH", 0.0m } } },
                { new DateTime(2022, 8, 31 ), new Dictionary<string, decimal> { { "TLT", 0.0m }, { "LQD", 0.0m }, { "SPY", 1.0m }, { "CASH", 0.0m } } },
                { new DateTime(2022, 7, 31 ), new Dictionary<string, decimal> { { "TLT", 0.0m }, { "LQD", 0.15m }, { "SPY", 0.85m }, { "CASH", 0.0m } } },
                { new DateTime(2022, 6, 30 ), new Dictionary<string, decimal> { { "TLT", 0.0m }, { "LQD", 0.03m }, { "SPY", 0.97m }, { "CASH", 0.0m } } },
                { new DateTime(2022, 5, 31 ), new Dictionary<string, decimal> { { "TLT", 0.09m }, { "LQD", 0.0m }, { "SPY", 0.91m }, { "CASH", 0.0m } } },
                { new DateTime(2022, 4, 30 ), new Dictionary<string, decimal> { { "TLT", 0.4m }, { "LQD", 0.0m }, { "SPY", 0.6m }, { "CASH", 0.0m } } },
                { new DateTime(2022, 3, 31 ), new Dictionary<string, decimal> { { "TLT", 0.2m }, { "LQD", 0.0m }, { "SPY", 0.8m }, { "CASH", 0.0m } } },
                { new DateTime(2022, 2, 28 ), new Dictionary<string, decimal> { { "TLT", 0.0m }, { "LQD", 0.0m }, { "SPY", 1.0m }, { "CASH", 0.0m } } },
                { new DateTime(2022, 1, 31 ), new Dictionary<string, decimal> { { "TLT", 0.0m }, { "LQD", 0.0m }, { "SPY", 1.0m }, { "CASH", 0.0m } } },
                { new DateTime(2021, 12, 31 ), new Dictionary<string, decimal> { { "TLT", 0.0m }, { "LQD", 0.0m }, { "SPY", 1.0m }, { "CASH", 0.0m } } },
                { new DateTime(2021, 11, 30 ), new Dictionary<string, decimal> { { "TLT", 0.0m }, { "LQD", 0.0m }, { "SPY", 1.0m }, { "CASH", 0.0m } } },
                { new DateTime(2021, 10, 31 ), new Dictionary<string, decimal> { { "TLT", 0.0m }, { "LQD", 0.0m }, { "SPY", 1.0m }, { "CASH", 0.0m } } },
                { new DateTime(2021, 9, 30 ), new Dictionary<string, decimal> { { "TLT", 0.0m }, { "LQD", 0.0m }, { "SPY", 1.0m }, { "CASH", 0.0m } } },
                { new DateTime(2021, 8, 31 ), new Dictionary<string, decimal> { { "TLT", 0.0m }, { "LQD", 0.0m }, { "SPY", 1.0m }, { "CASH", 0.0m } } },
                { new DateTime(2021, 7, 31 ), new Dictionary<string, decimal> { { "TLT", 0.0m }, { "LQD", 0.0m }, { "SPY", 1.0m }, { "CASH", 0.0m } } },
                { new DateTime(2021, 6, 30 ), new Dictionary<string, decimal> { { "TLT", 0.0m }, { "LQD", 0.0m }, { "SPY", 1.0m }, { "CASH", 0.0m } } },
                { new DateTime(2021, 5, 31 ), new Dictionary<string, decimal> { { "TLT", 0.0m }, { "LQD", 0.0m }, { "SPY", 1.0m }, { "CASH", 0.0m } } },
                { new DateTime(2021, 4, 30 ), new Dictionary<string, decimal> { { "TLT", 0.03m }, { "LQD", 0.0m }, { "SPY", 0.97m }, { "CASH", 0.0m } } },
                { new DateTime(2021, 3, 31 ), new Dictionary<string, decimal> { { "TLT", 0.25m }, { "LQD", 0.0m }, { "SPY", 0.75m }, { "CASH", 0.0m } } },
                { new DateTime(2021, 2, 28 ), new Dictionary<string, decimal> { { "TLT", 0.0m }, { "LQD", 0.0m }, { "SPY", 1.0m }, { "CASH", 0.0m } } },
                { new DateTime(2021, 1, 31 ), new Dictionary<string, decimal> { { "TLT", 0.0m }, { "LQD", 0.0m }, { "SPY", 1.0m }, { "CASH", 0.0m } } },
                { new DateTime(2020, 12, 31 ), new Dictionary<string, decimal> { { "TLT", 0.0m }, { "LQD", 0.0m }, { "SPY", 1.0m }, { "CASH", 0.0m } } },
                { new DateTime(2020, 11, 30 ), new Dictionary<string, decimal> { { "TLT", 0.0m }, { "LQD", 0.0m }, { "SPY", 1.0m }, { "CASH", 0.0m } } },
                { new DateTime(2020, 10, 31 ), new Dictionary<string, decimal> { { "TLT", 0.0m }, { "LQD", 0.24m }, { "SPY", 0.76m }, { "CASH", 0.0m } } },
                { new DateTime(2020, 9, 30 ), new Dictionary<string, decimal> { { "TLT", 0.0m }, { "LQD", 0.33m }, { "SPY", 0.67m }, { "CASH", 0.0m } } },
                { new DateTime(2020, 8, 31 ), new Dictionary<string, decimal> { { "TLT", 0.0m }, { "LQD", 0.32m }, { "SPY", 0.68m }, { "CASH", 0.0m } } },
                { new DateTime(2020, 7, 31 ), new Dictionary<string, decimal> { { "TLT", 0.0m }, { "LQD", 0.21m }, { "SPY", 0.79m }, { "CASH", 0.0m } } },
                { new DateTime(2020, 6, 30 ), new Dictionary<string, decimal> { { "TLT", 0.0m }, { "LQD", 0.36m }, { "SPY", 0.64m }, { "CASH", 0.0m } } },
                { new DateTime(2020, 5, 31 ), new Dictionary<string, decimal> { { "TLT", 0.0m }, { "LQD", 0.39m }, { "SPY", 0.61m }, { "CASH", 0.0m } } },
                { new DateTime(2020, 4, 30 ), new Dictionary<string, decimal> { { "TLT", 0.0m }, { "LQD", 0.4m }, { "SPY", 0.6m }, { "CASH", 0.0m } } },
                { new DateTime(2020, 3, 31 ), new Dictionary<string, decimal> { { "TLT", 0.0m }, { "LQD", 0.5m }, { "SPY", 0.5m }, { "CASH", 0.0m } } },
                { new DateTime(2020, 2, 29 ), new Dictionary<string, decimal> { { "TLT", 0.0m }, { "LQD", 0.08m }, { "SPY", 0.92m }, { "CASH", 0.0m } } },
                { new DateTime(2020, 1, 31 ), new Dictionary<string, decimal> { { "TLT", 0.0m }, { "LQD", 0.0m }, { "SPY", 1.0m }, { "CASH", 0.0m } } },
                { new DateTime(2019, 12, 31 ), new Dictionary<string, decimal> { { "TLT", 0.0m }, { "LQD", 0.0m }, { "SPY", 1.0m }, { "CASH", 0.0m } } },
                { new DateTime(2019, 11, 30 ), new Dictionary<string, decimal> { { "TLT", 0.0m }, { "LQD", 0.0m }, { "SPY", 1.0m }, { "CASH", 0.0m } } },
                { new DateTime(2019, 10, 31 ), new Dictionary<string, decimal> { { "TLT", 0.0m }, { "LQD", 0.0m }, { "SPY", 1.0m }, { "CASH", 0.0m } } },
                { new DateTime(2019, 9, 30 ), new Dictionary<string, decimal> { { "TLT", 0.0m }, { "LQD", 0.0m }, { "SPY", 1.0m }, { "CASH", 0.0m } } },
                { new DateTime(2019, 8, 31 ), new Dictionary<string, decimal> { { "TLT", 0.0m }, { "LQD", 0.0m }, { "SPY", 1.0m }, { "CASH", 0.0m } } },
                { new DateTime(2019, 7, 31 ), new Dictionary<string, decimal> { { "TLT", 0.0m }, { "LQD", 0.0m }, { "SPY", 1.0m }, { "CASH", 0.0m } } },
                { new DateTime(2019, 6, 30 ), new Dictionary<string, decimal> { { "TLT", 0.0m }, { "LQD", 0.0m }, { "SPY", 1.0m }, { "CASH", 0.0m } } },
                { new DateTime(2019, 5, 31 ), new Dictionary<string, decimal> { { "TLT", 0.0m }, { "LQD", 0.0m }, { "SPY", 1.0m }, { "CASH", 0.0m } } },
                { new DateTime(2019, 4, 30 ), new Dictionary<string, decimal> { { "TLT", 0.0m }, { "LQD", 0.0m }, { "SPY", 1.0m }, { "CASH", 0.0m } } },
                { new DateTime(2019, 3, 31 ), new Dictionary<string, decimal> { { "TLT", 0.0m }, { "LQD", 0.0m }, { "SPY", 1.0m }, { "CASH", 0.0m } } },
                { new DateTime(2019, 2, 28 ), new Dictionary<string, decimal> { { "TLT", 0.0m }, { "LQD", 0.0m }, { "SPY", 1.0m }, { "CASH", 0.0m } } },
                { new DateTime(2019, 1, 31 ), new Dictionary<string, decimal> { { "TLT", 0.0m }, { "LQD", 0.1m }, { "SPY", 0.9m }, { "CASH", 0.0m } } },
                { new DateTime(2018, 12, 31 ), new Dictionary<string, decimal> { { "TLT", 0.0m }, { "LQD", 0.12m }, { "SPY", 0.88m }, { "CASH", 0.0m } } },
                { new DateTime(2018, 11, 30 ), new Dictionary<string, decimal> { { "TLT", 0.0m }, { "LQD", 0.0m }, { "SPY", 1.0m }, { "CASH", 0.0m } } },
                { new DateTime(2018, 10, 31 ), new Dictionary<string, decimal> { { "TLT", 0.0m }, { "LQD", 0.0m }, { "SPY", 1.0m }, { "CASH", 0.0m } } },
                { new DateTime(2018, 9, 30 ), new Dictionary<string, decimal> { { "TLT", 0.0m }, { "LQD", 0.0m }, { "SPY", 1.0m }, { "CASH", 0.0m } } },
                { new DateTime(2018, 8, 31 ), new Dictionary<string, decimal> { { "TLT", 0.0m }, { "LQD", 0.0m }, { "SPY", 1.0m }, { "CASH", 0.0m } } },
                { new DateTime(2018, 7, 31 ), new Dictionary<string, decimal> { { "TLT", 0.0m }, { "LQD", 0.0m }, { "SPY", 1.0m }, { "CASH", 0.0m } } },
                { new DateTime(2018, 6, 30 ), new Dictionary<string, decimal> { { "TLT", 0.0m }, { "LQD", 0.0m }, { "SPY", 1.0m }, { "CASH", 0.0m } } },
                { new DateTime(2018, 5, 31 ), new Dictionary<string, decimal> { { "TLT", 0.0m }, { "LQD", 0.0m }, { "SPY", 1.0m }, { "CASH", 0.0m } } },
                { new DateTime(2018, 4, 30 ), new Dictionary<string, decimal> { { "TLT", 0.0m }, { "LQD", 0.0m }, { "SPY", 1.0m }, { "CASH", 0.0m } } },
                { new DateTime(2018, 3, 31 ), new Dictionary<string, decimal> { { "TLT", 0.0m }, { "LQD", 0.0m }, { "SPY", 1.0m }, { "CASH", 0.0m } } },
                { new DateTime(2018, 2, 28 ), new Dictionary<string, decimal> { { "TLT", 0.0m }, { "LQD", 0.0m }, { "SPY", 1.0m }, { "CASH", 0.0m } } },
                { new DateTime(2018, 1, 31 ), new Dictionary<string, decimal> { { "TLT", 0.0m }, { "LQD", 0.0m }, { "SPY", 1.0m }, { "CASH", 0.0m } } },
                { new DateTime(2017, 12, 31 ), new Dictionary<string, decimal> { { "TLT", 0.0m }, { "LQD", 0.0m }, { "SPY", 1.0m }, { "CASH", 0.0m } } },
                { new DateTime(2017, 11, 30 ), new Dictionary<string, decimal> { { "TLT", 0.0m }, { "LQD", 0.0m }, { "SPY", 1.0m }, { "CASH", 0.0m } } },
                { new DateTime(2017, 10, 31 ), new Dictionary<string, decimal> { { "TLT", 0.0m }, { "LQD", 0.0m }, { "SPY", 1.0m }, { "CASH", 0.0m } } },
                { new DateTime(2017, 9, 30 ), new Dictionary<string, decimal> { { "TLT", 0.0m }, { "LQD", 0.0m }, { "SPY", 1.0m }, { "CASH", 0.0m } } },
                { new DateTime(2017, 8, 31 ), new Dictionary<string, decimal> { { "TLT", 0.0m }, { "LQD", 0.0m }, { "SPY", 1.0m }, { "CASH", 0.0m } } },
                { new DateTime(2017, 7, 31 ), new Dictionary<string, decimal> { { "TLT", 0.0m }, { "LQD", 0.0m }, { "SPY", 1.0m }, { "CASH", 0.0m } } },
                { new DateTime(2017, 6, 30 ), new Dictionary<string, decimal> { { "TLT", 0.0m }, { "LQD", 0.0m }, { "SPY", 1.0m }, { "CASH", 0.0m } } },
                { new DateTime(2017, 5, 31 ), new Dictionary<string, decimal> { { "TLT", 0.0m }, { "LQD", 0.0m }, { "SPY", 1.0m }, { "CASH", 0.0m } } },
                { new DateTime(2017, 4, 30 ), new Dictionary<string, decimal> { { "TLT", 0.0m }, { "LQD", 0.0m }, { "SPY", 1.0m }, { "CASH", 0.0m } } },
                { new DateTime(2017, 3, 31 ), new Dictionary<string, decimal> { { "TLT", 0.0m }, { "LQD", 0.0m }, { "SPY", 1.0m }, { "CASH", 0.0m } } },
                { new DateTime(2017, 2, 28 ), new Dictionary<string, decimal> { { "TLT", 0.08m }, { "LQD", 0.0m }, { "SPY", 0.92m }, { "CASH", 0.0m } } },
                { new DateTime(2017, 1, 31 ), new Dictionary<string, decimal> { { "TLT", 0.18m }, { "LQD", 0.0m }, { "SPY", 0.82m }, { "CASH", 0.0m } } },
                { new DateTime(2016, 12, 31 ), new Dictionary<string, decimal> { { "TLT", 0.18m }, { "LQD", 0.0m }, { "SPY", 0.82m }, { "CASH", 0.0m } } },
                { new DateTime(2016, 11, 30 ), new Dictionary<string, decimal> { { "TLT", 0.13m }, { "LQD", 0.02m }, { "SPY", 0.84m }, { "CASH", 0.0m } } },
                { new DateTime(2016, 10, 31 ), new Dictionary<string, decimal> { { "TLT", 0.0m }, { "LQD", 0.25m }, { "SPY", 0.75m }, { "CASH", 0.0m } } },
                { new DateTime(2016, 9, 30 ), new Dictionary<string, decimal> { { "TLT", 0.0m }, { "LQD", 0.24m }, { "SPY", 0.76m }, { "CASH", 0.0m } } },
                { new DateTime(2016, 8, 31 ), new Dictionary<string, decimal> { { "TLT", 0.0m }, { "LQD", 0.22m }, { "SPY", 0.78m }, { "CASH", 0.0m } } },
                { new DateTime(2016, 7, 31 ), new Dictionary<string, decimal> { { "TLT", 0.0m }, { "LQD", 0.3m }, { "SPY", 0.7m }, { "CASH", 0.0m } } },
                { new DateTime(2016, 6, 30 ), new Dictionary<string, decimal> { { "TLT", 0.0m }, { "LQD", 0.37m }, { "SPY", 0.63m }, { "CASH", 0.0m } } },
                { new DateTime(2016, 5, 31 ), new Dictionary<string, decimal> { { "TLT", 0.0m }, { "LQD", 0.37m }, { "SPY", 0.63m }, { "CASH", 0.0m } } },
                { new DateTime(2016, 4, 30 ), new Dictionary<string, decimal> { { "TLT", 0.0m }, { "LQD", 0.39m }, { "SPY", 0.61m }, { "CASH", 0.0m } } },
                { new DateTime(2016, 3, 31 ), new Dictionary<string, decimal> { { "TLT", 0.0m }, { "LQD", 0.47m }, { "SPY", 0.53m }, { "CASH", 0.0m } } },
                { new DateTime(2016, 2, 29 ), new Dictionary<string, decimal> { { "TLT", 0.0m }, { "LQD", 0.53m }, { "SPY", 0.47m }, { "CASH", 0.0m } } },
                { new DateTime(2016, 1, 31 ), new Dictionary<string, decimal> { { "TLT", 0.0m }, { "LQD", 0.54m }, { "SPY", 0.46m }, { "CASH", 0.0m } } },
                { new DateTime(2015, 12, 31 ), new Dictionary<string, decimal> { { "TLT", 0.11m }, { "LQD", 0.48m }, { "SPY", 0.41m }, { "CASH", 0.0m } } },
                { new DateTime(2015, 11, 30 ), new Dictionary<string, decimal> { { "TLT", 0.07m }, { "LQD", 0.47m }, { "SPY", 0.46m }, { "CASH", 0.0m } } },
                { new DateTime(2015, 10, 31 ), new Dictionary<string, decimal> { { "TLT", 0.1m }, { "LQD", 0.45m }, { "SPY", 0.45m }, { "CASH", 0.0m } } },
                { new DateTime(2015, 9, 30 ), new Dictionary<string, decimal> { { "TLT", 0.09m }, { "LQD", 0.42m }, { "SPY", 0.49m }, { "CASH", 0.0m } } },
                { new DateTime(2015, 8, 31 ), new Dictionary<string, decimal> { { "TLT", 0.11m }, { "LQD", 0.38m }, { "SPY", 0.51m }, { "CASH", 0.0m } } },
                { new DateTime(2015, 7, 31 ), new Dictionary<string, decimal> { { "TLT", 0.13m }, { "LQD", 0.35m }, { "SPY", 0.52m }, { "CASH", 0.0m } } },
                { new DateTime(2015, 6, 30 ), new Dictionary<string, decimal> { { "TLT", 0.21m }, { "LQD", 0.27m }, { "SPY", 0.51m }, { "CASH", 0.0m } } },
                { new DateTime(2015, 5, 31 ), new Dictionary<string, decimal> { { "TLT", 0.14m }, { "LQD", 0.26m }, { "SPY", 0.6m }, { "CASH", 0.0m } } },
                { new DateTime(2015, 4, 30 ), new Dictionary<string, decimal> { { "TLT", 0.12m }, { "LQD", 0.17m }, { "SPY", 0.7m }, { "CASH", 0.0m } } },
                { new DateTime(2015, 3, 31 ), new Dictionary<string, decimal> { { "TLT", 0.07m }, { "LQD", 0.17m }, { "SPY", 0.77m }, { "CASH", 0.0m } } },
                { new DateTime(2015, 2, 28 ), new Dictionary<string, decimal> { { "TLT", 0.1m }, { "LQD", 0.1m }, { "SPY", 0.8m }, { "CASH", 0.0m } } },
                { new DateTime(2015, 1, 31 ), new Dictionary<string, decimal> { { "TLT", 0.0m }, { "LQD", 0.21m }, { "SPY", 0.79m }, { "CASH", 0.0m } } },
                { new DateTime(2014, 12, 31 ), new Dictionary<string, decimal> { { "TLT", 0.16m }, { "LQD", 0.12m }, { "SPY", 0.71m }, { "CASH", 0.0m } } },
                { new DateTime(2014, 11, 30 ), new Dictionary<string, decimal> { { "TLT", 0.18m }, { "LQD", 0.13m }, { "SPY", 0.69m }, { "CASH", 0.0m } } },
                { new DateTime(2014, 10, 31 ), new Dictionary<string, decimal> { { "TLT", 0.27m }, { "LQD", 0.02m }, { "SPY", 0.71m }, { "CASH", 0.0m } } },
                { new DateTime(2014, 9, 30 ), new Dictionary<string, decimal> { { "TLT", 0.33m }, { "LQD", 0.0m }, { "SPY", 0.67m }, { "CASH", 0.0m } } },
                { new DateTime(2014, 8, 31 ), new Dictionary<string, decimal> { { "TLT", 0.28m }, { "LQD", 0.0m }, { "SPY", 0.72m }, { "CASH", 0.0m } } },
                { new DateTime(2014, 7, 31 ), new Dictionary<string, decimal> { { "TLT", 0.35m }, { "LQD", 0.0m }, { "SPY", 0.65m }, { "CASH", 0.0m } } },
                { new DateTime(2014, 6, 30 ), new Dictionary<string, decimal> { { "TLT", 0.34m }, { "LQD", 0.0m }, { "SPY", 0.66m }, { "CASH", 0.0m } } },
                { new DateTime(2014, 5, 31 ), new Dictionary<string, decimal> { { "TLT", 0.31m }, { "LQD", 0.0m }, { "SPY", 0.69m }, { "CASH", 0.0m } } },
                { new DateTime(2014, 4, 30 ), new Dictionary<string, decimal> { { "TLT", 0.38m }, { "LQD", 0.0m }, { "SPY", 0.62m }, { "CASH", 0.0m } } },
                { new DateTime(2014, 3, 31 ), new Dictionary<string, decimal> { { "TLT", 0.39m }, { "LQD", 0.0m }, { "SPY", 0.61m }, { "CASH", 0.0m } } },
                { new DateTime(2014, 2, 28 ), new Dictionary<string, decimal> { { "TLT", 0.38m }, { "LQD", 0.01m }, { "SPY", 0.61m }, { "CASH", 0.0m } } },
                { new DateTime(2014, 1, 31 ), new Dictionary<string, decimal> { { "TLT", 0.36m }, { "LQD", 0.04m }, { "SPY", 0.6m }, { "CASH", 0.0m } } },
                { new DateTime(2013, 12, 31 ), new Dictionary<string, decimal> { { "TLT", 0.51m }, { "LQD", 0.0m }, { "SPY", 0.49m }, { "CASH", 0.0m } } },
                { new DateTime(2013, 11, 30 ), new Dictionary<string, decimal> { { "TLT", 0.35m }, { "LQD", 0.16m }, { "SPY", 0.49m }, { "CASH", 0.0m } } },
                { new DateTime(2013, 10, 31 ), new Dictionary<string, decimal> { { "TLT", 0.28m }, { "LQD", 0.17m }, { "SPY", 0.54m }, { "CASH", 0.0m } } },
                { new DateTime(2013, 9, 30 ), new Dictionary<string, decimal> { { "TLT", 0.28m }, { "LQD", 0.19m }, { "SPY", 0.53m }, { "CASH", 0.0m } } },
                { new DateTime(2013, 8, 31 ), new Dictionary<string, decimal> { { "TLT", 0.34m }, { "LQD", 0.13m }, { "SPY", 0.54m }, { "CASH", 0.0m } } },
                { new DateTime(2013, 7, 31 ), new Dictionary<string, decimal> { { "TLT", 0.27m }, { "LQD", 0.19m }, { "SPY", 0.54m }, { "CASH", 0.0m } } },
                { new DateTime(2013, 6, 30 ), new Dictionary<string, decimal> { { "TLT", 0.22m }, { "LQD", 0.24m }, { "SPY", 0.54m }, { "CASH", 0.0m } } },
                { new DateTime(2013, 5, 31 ), new Dictionary<string, decimal> { { "TLT", 0.13m }, { "LQD", 0.21m }, { "SPY", 0.66m }, { "CASH", 0.0m } } },
                { new DateTime(2013, 4, 30 ), new Dictionary<string, decimal> { { "TLT", 0.0m }, { "LQD", 0.24m }, { "SPY", 0.76m }, { "CASH", 0.0m } } },
                { new DateTime(2013, 3, 31 ), new Dictionary<string, decimal> { { "TLT", 0.02m }, { "LQD", 0.28m }, { "SPY", 0.7m }, { "CASH", 0.0m } } },
                { new DateTime(2013, 2, 28 ), new Dictionary<string, decimal> { { "TLT", 0.02m }, { "LQD", 0.26m }, { "SPY", 0.72m }, { "CASH", 0.0m } } },
                { new DateTime(2013, 1, 31 ), new Dictionary<string, decimal> { { "TLT", 0.07m }, { "LQD", 0.23m }, { "SPY", 0.7m }, { "CASH", 0.0m } } },
                { new DateTime(2012, 12, 31 ), new Dictionary<string, decimal> { { "TLT", 0.0m }, { "LQD", 0.2m }, { "SPY", 0.79m }, { "CASH", 0.0m } } },
                { new DateTime(2012, 11, 30 ), new Dictionary<string, decimal> { { "TLT", 0.0m }, { "LQD", 0.24m }, { "SPY", 0.76m }, { "CASH", 0.0m } } },
                { new DateTime(2012, 10, 31 ), new Dictionary<string, decimal> { { "TLT", 0.0m }, { "LQD", 0.18m }, { "SPY", 0.82m }, { "CASH", 0.0m } } },
                { new DateTime(2012, 9, 30 ), new Dictionary<string, decimal> { { "TLT", 0.0m }, { "LQD", 0.27m }, { "SPY", 0.73m }, { "CASH", 0.0m } } },
                { new DateTime(2012, 8, 31 ), new Dictionary<string, decimal> { { "TLT", 0.0m }, { "LQD", 0.31m }, { "SPY", 0.69m }, { "CASH", 0.0m } } },
                { new DateTime(2012, 7, 31 ), new Dictionary<string, decimal> { { "TLT", 0.0m }, { "LQD", 0.31m }, { "SPY", 0.69m }, { "CASH", 0.0m } } },
                { new DateTime(2012, 6, 30 ), new Dictionary<string, decimal> { { "TLT", 0.0m }, { "LQD", 0.31m }, { "SPY", 0.69m }, { "CASH", 0.0m } } },
                { new DateTime(2012, 5, 31 ), new Dictionary<string, decimal> { { "TLT", 0.0m }, { "LQD", 0.33m }, { "SPY", 0.67m }, { "CASH", 0.0m } } },
                { new DateTime(2012, 4, 30 ), new Dictionary<string, decimal> { { "TLT", 0.03m }, { "LQD", 0.3m }, { "SPY", 0.67m }, { "CASH", 0.0m } } },
                { new DateTime(2012, 3, 31 ), new Dictionary<string, decimal> { { "TLT", 0.1m }, { "LQD", 0.24m }, { "SPY", 0.66m }, { "CASH", 0.0m } } },
                { new DateTime(2012, 2, 29 ), new Dictionary<string, decimal> { { "TLT", 0.04m }, { "LQD", 0.26m }, { "SPY", 0.7m }, { "CASH", 0.0m } } },
                { new DateTime(2012, 1, 31 ), new Dictionary<string, decimal> { { "TLT", 0.01m }, { "LQD", 0.3m }, { "SPY", 0.69m }, { "CASH", 0.0m } } },
                { new DateTime(2011, 12, 31 ), new Dictionary<string, decimal> { { "TLT", 0.03m }, { "LQD", 0.28m }, { "SPY", 0.69m }, { "CASH", 0.0m } } },
                { new DateTime(2011, 11, 30 ), new Dictionary<string, decimal> { { "TLT", 0.07m }, { "LQD", 0.25m }, { "SPY", 0.68m }, { "CASH", 0.0m } } },
                { new DateTime(2011, 10, 31 ), new Dictionary<string, decimal> { { "TLT", 0.08m }, { "LQD", 0.25m }, { "SPY", 0.66m }, { "CASH", 0.0m } } },
                { new DateTime(2011, 9, 30 ), new Dictionary<string, decimal> { { "TLT", 0.03m }, { "LQD", 0.27m }, { "SPY", 0.7m }, { "CASH", 0.0m } } },
                { new DateTime(2011, 8, 31 ), new Dictionary<string, decimal> { { "TLT", 0.09m }, { "LQD", 0.26m }, { "SPY", 0.65m }, { "CASH", 0.0m } } },
                { new DateTime(2011, 7, 31 ), new Dictionary<string, decimal> { { "TLT", 0.2m }, { "LQD", 0.2m }, { "SPY", 0.6m }, { "CASH", 0.0m } } },
                { new DateTime(2011, 6, 30 ), new Dictionary<string, decimal> { { "TLT", 0.3m }, { "LQD", 0.15m }, { "SPY", 0.55m }, { "CASH", 0.0m } } },
                { new DateTime(2011, 5, 31 ), new Dictionary<string, decimal> { { "TLT", 0.29m }, { "LQD", 0.14m }, { "SPY", 0.56m }, { "CASH", 0.0m } } },
                { new DateTime(2011, 4, 30 ), new Dictionary<string, decimal> { { "TLT", 0.36m }, { "LQD", 0.12m }, { "SPY", 0.51m }, { "CASH", 0.0m } } },
                { new DateTime(2011, 3, 31 ), new Dictionary<string, decimal> { { "TLT", 0.38m }, { "LQD", 0.11m }, { "SPY", 0.51m }, { "CASH", 0.0m } } },
                { new DateTime(2011, 2, 28 ), new Dictionary<string, decimal> { { "TLT", 0.38m }, { "LQD", 0.13m }, { "SPY", 0.49m }, { "CASH", 0.0m } } },
                { new DateTime(2011, 1, 31 ), new Dictionary<string, decimal> { { "TLT", 0.36m }, { "LQD", 0.14m }, { "SPY", 0.5m }, { "CASH", 0.0m } } },
                { new DateTime(2010, 12, 31 ), new Dictionary<string, decimal> { { "TLT", 0.32m }, { "LQD", 0.17m }, { "SPY", 0.51m }, { "CASH", 0.0m } } },
                { new DateTime(2010, 11, 30 ), new Dictionary<string, decimal> { { "TLT", 0.2m }, { "LQD", 0.25m }, { "SPY", 0.55m }, { "CASH", 0.0m } } },
                { new DateTime(2010, 10, 31 ), new Dictionary<string, decimal> { { "TLT", 0.16m }, { "LQD", 0.29m }, { "SPY", 0.55m }, { "CASH", 0.0m } } },
                { new DateTime(2010, 9, 30 ), new Dictionary<string, decimal> { { "TLT", 0.14m }, { "LQD", 0.24m }, { "SPY", 0.62m }, { "CASH", 0.0m } } },
                { new DateTime(2010, 8, 31 ), new Dictionary<string, decimal> { { "TLT", 0.13m }, { "LQD", 0.25m }, { "SPY", 0.62m }, { "CASH", 0.0m } } },
                { new DateTime(2010, 7, 31 ), new Dictionary<string, decimal> { { "TLT", 0.23m }, { "LQD", 0.24m }, { "SPY", 0.53m }, { "CASH", 0.0m } } },
                { new DateTime(2010, 6, 30 ), new Dictionary<string, decimal> { { "TLT", 0.21m }, { "LQD", 0.25m }, { "SPY", 0.54m }, { "CASH", 0.0m } } },
                { new DateTime(2010, 5, 31 ), new Dictionary<string, decimal> { { "TLT", 0.35m }, { "LQD", 0.24m }, { "SPY", 0.41m }, { "CASH", 0.0m } } },
                { new DateTime(2010, 4, 30 ), new Dictionary<string, decimal> { { "TLT", 0.57m }, { "LQD", 0.1m }, { "SPY", 0.34m }, { "CASH", 0.0m } } },
                { new DateTime(2010, 3, 31 ), new Dictionary<string, decimal> { { "TLT", 0.58m }, { "LQD", 0.12m }, { "SPY", 0.3m }, { "CASH", 0.0m } } },
                { new DateTime(2010, 2, 28 ), new Dictionary<string, decimal> { { "TLT", 0.67m }, { "LQD", 0.24m }, { "SPY", 0.09m }, { "CASH", 0.0m } } },
                { new DateTime(2010, 1, 31 ), new Dictionary<string, decimal> { { "TLT", 0.68m }, { "LQD", 0.21m }, { "SPY", 0.11m }, { "CASH", 0.0m } } },
                { new DateTime(2009, 12, 31 ), new Dictionary<string, decimal> { { "TLT", 0.82m }, { "LQD", 0.16m }, { "SPY", 0.02m }, { "CASH", 0.0m } } },
                { new DateTime(2009, 11, 30 ), new Dictionary<string, decimal> { { "TLT", 0.47m }, { "LQD", 0.35m }, { "SPY", 0.18m }, { "CASH", 0.0m } } },
                { new DateTime(2009, 10, 31 ), new Dictionary<string, decimal> { { "TLT", 0.5m }, { "LQD", 0.33m }, { "SPY", 0.17m }, { "CASH", 0.0m } } },
                { new DateTime(2009, 9, 30 ), new Dictionary<string, decimal> { { "TLT", 0.51m }, { "LQD", 0.29m }, { "SPY", 0.2m }, { "CASH", 0.0m } } },
                { new DateTime(2009, 8, 31 ), new Dictionary<string, decimal> { { "TLT", 0.44m }, { "LQD", 0.32m }, { "SPY", 0.24m }, { "CASH", 0.0m } } },
                { new DateTime(2009, 7, 31 ), new Dictionary<string, decimal> { { "TLT", 0.41m }, { "LQD", 0.37m }, { "SPY", 0.21m }, { "CASH", 0.0m } } },
                { new DateTime(2009, 6, 30 ), new Dictionary<string, decimal> { { "TLT", 0.35m }, { "LQD", 0.43m }, { "SPY", 0.22m }, { "CASH", 0.0m } } },
                { new DateTime(2009, 5, 31 ), new Dictionary<string, decimal> { { "TLT", 0.25m }, { "LQD", 0.51m }, { "SPY", 0.24m }, { "CASH", 0.0m } } },
                { new DateTime(2009, 4, 30 ), new Dictionary<string, decimal> { { "TLT", 0.18m }, { "LQD", 0.56m }, { "SPY", 0.27m }, { "CASH", 0.0m } } },
                { new DateTime(2009, 3, 31 ), new Dictionary<string, decimal> { { "TLT", 0.1m }, { "LQD", 0.6m }, { "SPY", 0.3m }, { "CASH", 0.0m } } },
                { new DateTime(2009, 2, 28 ), new Dictionary<string, decimal> { { "TLT", 0.11m }, { "LQD", 0.46m }, { "SPY", 0.44m }, { "CASH", 0.0m } } },
                { new DateTime(2009, 1, 31 ), new Dictionary<string, decimal> { { "TLT", 0.1m }, { "LQD", 0.51m }, { "SPY", 0.39m }, { "CASH", 0.0m } } },
                { new DateTime(2008, 12, 31 ), new Dictionary<string, decimal> { { "TLT", 0.05m }, { "LQD", 0.57m }, { "SPY", 0.38m }, { "CASH", 0.0m } } },
                { new DateTime(2008, 11, 30 ), new Dictionary<string, decimal> { { "TLT", 0.1m }, { "LQD", 0.58m }, { "SPY", 0.32m }, { "CASH", 0.0m } } },
                { new DateTime(2008, 10, 31 ), new Dictionary<string, decimal> { { "TLT", 0.16m }, { "LQD", 0.59m }, { "SPY", 0.25m }, { "CASH", 0.0m } } },
                { new DateTime(2008, 9, 30 ), new Dictionary<string, decimal> { { "TLT", 0.19m }, { "LQD", 0.5m }, { "SPY", 0.31m }, { "CASH", 0.0m } } },
                { new DateTime(2008, 8, 31 ), new Dictionary<string, decimal> { { "TLT", 0.11m }, { "LQD", 0.47m }, { "SPY", 0.42m }, { "CASH", 0.0m } } },
                { new DateTime(2008, 7, 31 ), new Dictionary<string, decimal> { { "TLT", 0.14m }, { "LQD", 0.46m }, { "SPY", 0.4m }, { "CASH", 0.0m } } },
                { new DateTime(2008, 6, 30 ), new Dictionary<string, decimal> { { "TLT", 0.12m }, { "LQD", 0.43m }, { "SPY", 0.45m }, { "CASH", 0.0m } } },
                { new DateTime(2008, 5, 31 ), new Dictionary<string, decimal> { { "TLT", 0.13m }, { "LQD", 0.44m }, { "SPY", 0.42m }, { "CASH", 0.0m } } },
                { new DateTime(2008, 4, 30 ), new Dictionary<string, decimal> { { "TLT", 0.14m }, { "LQD", 0.44m }, { "SPY", 0.42m }, { "CASH", 0.0m } } },
                { new DateTime(2008, 3, 31 ), new Dictionary<string, decimal> { { "TLT", 0.08m }, { "LQD", 0.48m }, { "SPY", 0.44m }, { "CASH", 0.0m } } },
                { new DateTime(2008, 2, 29 ), new Dictionary<string, decimal> { { "TLT", 0.03m }, { "LQD", 0.45m }, { "SPY", 0.52m }, { "CASH", 0.0m } } },
                { new DateTime(2008, 1, 31 ), new Dictionary<string, decimal> { { "TLT", 0.04m }, { "LQD", 0.41m }, { "SPY", 0.55m }, { "CASH", 0.0m } } },
                { new DateTime(2007, 12, 31 ), new Dictionary<string, decimal> { { "TLT", 0.0m }, { "LQD", 0.33m }, { "SPY", 0.67m }, { "CASH", 0.0m } } },
                { new DateTime(2007, 11, 30 ), new Dictionary<string, decimal> { { "TLT", 0.0m }, { "LQD", 0.25m }, { "SPY", 0.75m }, { "CASH", 0.0m } } },
                { new DateTime(2007, 10, 31 ), new Dictionary<string, decimal> { { "TLT", 0.0m }, { "LQD", 0.0m }, { "SPY", 1.0m }, { "CASH", 0.0m } } },
                { new DateTime(2007, 9, 30 ), new Dictionary<string, decimal> { { "TLT", 0.0m }, { "LQD", 0.0m }, { "SPY", 1.0m }, { "CASH", 0.0m } } },
                { new DateTime(2007, 8, 31 ), new Dictionary<string, decimal> { { "TLT", 0.0m }, { "LQD", 0.0m }, { "SPY", 1.0m }, { "CASH", 0.0m } } },
                { new DateTime(2007, 7, 31 ), new Dictionary<string, decimal> { { "TLT", 0.0m }, { "LQD", 0.0m }, { "SPY", 1.0m }, { "CASH", 0.0m } } },
                { new DateTime(2007, 6, 30 ), new Dictionary<string, decimal> { { "TLT", 0.0m }, { "LQD", 0.0m }, { "SPY", 1.0m }, { "CASH", 0.0m } } },
                { new DateTime(2007, 5, 31 ), new Dictionary<string, decimal> { { "TLT", 0.0m }, { "LQD", 0.0m }, { "SPY", 1.0m }, { "CASH", 0.0m } } },
                { new DateTime(2007, 4, 30 ), new Dictionary<string, decimal> { { "TLT", 0.0m }, { "LQD", 0.0m }, { "SPY", 1.0m }, { "CASH", 0.0m } } },
                { new DateTime(2007, 3, 31 ), new Dictionary<string, decimal> { { "TLT", 0.0m }, { "LQD", 0.0m }, { "SPY", 1.0m }, { "CASH", 0.0m } } },
                { new DateTime(2007, 2, 28 ), new Dictionary<string, decimal> { { "TLT", 0.0m }, { "LQD", 0.0m }, { "SPY", 1.0m }, { "CASH", 0.0m } } },
                { new DateTime(2007, 1, 31 ), new Dictionary<string, decimal> { { "TLT", 0.0m }, { "LQD", 0.0m }, { "SPY", 1.0m }, { "CASH", 0.0m } } },
                { new DateTime(2006, 12, 31 ), new Dictionary<string, decimal> { { "TLT", 0.0m }, { "LQD", 0.0m }, { "SPY", 1.0m }, { "CASH", 0.0m } } },
                { new DateTime(2006, 11, 30 ), new Dictionary<string, decimal> { { "TLT", 0.0m }, { "LQD", 0.0m }, { "SPY", 1.0m }, { "CASH", 0.0m } } },
                { new DateTime(2006, 10, 31 ), new Dictionary<string, decimal> { { "TLT", 0.0m }, { "LQD", 0.0m }, { "SPY", 1.0m }, { "CASH", 0.0m } } },
                { new DateTime(2006, 9, 30 ), new Dictionary<string, decimal> { { "TLT", 0.0m }, { "LQD", 0.0m }, { "SPY", 1.0m }, { "CASH", 0.0m } } },
                { new DateTime(2006, 8, 31 ), new Dictionary<string, decimal> { { "TLT", 0.0m }, { "LQD", 0.0m }, { "SPY", 1.0m }, { "CASH", 0.0m } } },
                { new DateTime(2006, 7, 31 ), new Dictionary<string, decimal> { { "TLT", 0.0m }, { "LQD", 0.0m }, { "SPY", 1.0m }, { "CASH", 0.0m } } },
                { new DateTime(2006, 6, 30 ), new Dictionary<string, decimal> { { "TLT", 0.0m }, { "LQD", 0.0m }, { "SPY", 1.0m }, { "CASH", 0.0m } } },
                { new DateTime(2006, 5, 31 ), new Dictionary<string, decimal> { { "TLT", 0.0m }, { "LQD", 0.0m }, { "SPY", 1.0m }, { "CASH", 0.0m } } },
                { new DateTime(2006, 4, 30 ), new Dictionary<string, decimal> { { "TLT", 0.0m }, { "LQD", 0.0m }, { "SPY", 1.0m }, { "CASH", 0.0m } } },
                { new DateTime(2006, 3, 31 ), new Dictionary<string, decimal> { { "TLT", 0.0m }, { "LQD", 0.0m }, { "SPY", 1.0m }, { "CASH", 0.0m } } },
                { new DateTime(2006, 2, 28 ), new Dictionary<string, decimal> { { "TLT", 0.0m }, { "LQD", 0.0m }, { "SPY", 1.0m }, { "CASH", 0.0m } } },
                { new DateTime(2006, 1, 31 ), new Dictionary<string, decimal> { { "TLT", 0.0m }, { "LQD", 0.0m }, { "SPY", 1.0m }, { "CASH", 0.0m } } },
                { new DateTime(2005, 12, 31 ), new Dictionary<string, decimal> { { "TLT", 0.0m }, { "LQD", 0.0m }, { "SPY", 1.0m }, { "CASH", 0.0m } } },
                { new DateTime(2005, 11, 30 ), new Dictionary<string, decimal> { { "TLT", 0.0m }, { "LQD", 0.0m }, { "SPY", 1.0m }, { "CASH", 0.0m } } },
                { new DateTime(2005, 10, 31 ), new Dictionary<string, decimal> { { "TLT", 0.0m }, { "LQD", 0.0m }, { "SPY", 1.0m }, { "CASH", 0.0m } } },
                { new DateTime(2005, 9, 30 ), new Dictionary<string, decimal> { { "TLT", 0.0m }, { "LQD", 0.0m }, { "SPY", 1.0m }, { "CASH", 0.0m } } },
                { new DateTime(2005, 8, 31 ), new Dictionary<string, decimal> { { "TLT", 0.0m }, { "LQD", 0.0m }, { "SPY", 1.0m }, { "CASH", 0.0m } } },
                { new DateTime(2005, 7, 31 ), new Dictionary<string, decimal> { { "TLT", 0.0m }, { "LQD", 0.0m }, { "SPY", 1.0m }, { "CASH", 0.0m } } },
                { new DateTime(2005, 6, 30 ), new Dictionary<string, decimal> { { "TLT", 0.0m }, { "LQD", 0.0m }, { "SPY", 1.0m }, { "CASH", 0.0m } } },
                { new DateTime(2005, 5, 31 ), new Dictionary<string, decimal> { { "TLT", 0.0m }, { "LQD", 0.0m }, { "SPY", 1.0m }, { "CASH", 0.0m } } },
                { new DateTime(2005, 4, 30 ), new Dictionary<string, decimal> { { "TLT", 0.0m }, { "LQD", 0.0m }, { "SPY", 1.0m }, { "CASH", 0.0m } } },
                { new DateTime(2005, 3, 31 ), new Dictionary<string, decimal> { { "TLT", 0.0m }, { "LQD", 0.0m }, { "SPY", 1.0m }, { "CASH", 0.0m } } },
                { new DateTime(2005, 2, 28 ), new Dictionary<string, decimal> { { "TLT", 0.0m }, { "LQD", 0.0m }, { "SPY", 1.0m }, { "CASH", 0.0m } } },
                { new DateTime(2005, 1, 31 ), new Dictionary<string, decimal> { { "TLT", 0.0m }, { "LQD", 0.0m }, { "SPY", 1.0m }, { "CASH", 0.0m } } },
                { new DateTime(2004, 12, 31 ), new Dictionary<string, decimal> { { "TLT", 0.11m }, { "LQD", 0.0m }, { "SPY", 0.89m }, { "CASH", 0.0m } } },
                { new DateTime(2004, 11, 30 ), new Dictionary<string, decimal> { { "TLT", 0.16m }, { "LQD", 0.0m }, { "SPY", 0.84m }, { "CASH", 0.0m } } },
                { new DateTime(2004, 10, 31 ), new Dictionary<string, decimal> { { "TLT", 0.13m }, { "LQD", 0.03m }, { "SPY", 0.85m }, { "CASH", 0.0m } } },
                { new DateTime(2004, 9, 30 ), new Dictionary<string, decimal> { { "TLT", 0.2m }, { "LQD", 0.0m }, { "SPY", 0.8m }, { "CASH", 0.0m } } },
                { new DateTime(2004, 8, 31 ), new Dictionary<string, decimal> { { "TLT", 0.22m }, { "LQD", 0.09m }, { "SPY", 0.69m }, { "CASH", 0.0m } } },
                { new DateTime(2004, 7, 31 ), new Dictionary<string, decimal> { { "TLT", 0.36m }, { "LQD", 0.05m }, { "SPY", 0.59m }, { "CASH", 0.0m } } },
                { new DateTime(2004, 6, 30 ), new Dictionary<string, decimal> { { "TLT", 0.45m }, { "LQD", 0.03m }, { "SPY", 0.53m }, { "CASH", 0.0m } } },
                { new DateTime(2004, 5, 31 ), new Dictionary<string, decimal> { { "TLT", 0.54m }, { "LQD", 0.0m }, { "SPY", 0.46m }, { "CASH", 0.0m } } },
                { new DateTime(2004, 4, 30 ), new Dictionary<string, decimal> { { "TLT", 0.51m }, { "LQD", 0.0m }, { "SPY", 0.49m }, { "CASH", 0.0m } } },
                { new DateTime(2004, 3, 31 ), new Dictionary<string, decimal> { { "TLT", 0.29m }, { "LQD", 0.12m }, { "SPY", 0.59m }, { "CASH", 0.0m } } },
                { new DateTime(2004, 2, 29 ), new Dictionary<string, decimal> { { "TLT", 0.37m }, { "LQD", 0.09m }, { "SPY", 0.54m }, { "CASH", 0.0m } } },
                { new DateTime(2004, 1, 31 ), new Dictionary<string, decimal> { { "TLT", 0.42m }, { "LQD", 0.09m }, { "SPY", 0.5m }, { "CASH", 0.0m } } },
                { new DateTime(2003, 12, 31 ), new Dictionary<string, decimal> { { "TLT", 0.41m }, { "LQD", 0.13m }, { "SPY", 0.46m }, { "CASH", 0.0m } } },
                { new DateTime(2003, 11, 30 ), new Dictionary<string, decimal> { { "TLT", 0.46m }, { "LQD", 0.09m }, { "SPY", 0.45m }, { "CASH", 0.0m } } },
                { new DateTime(2003, 10, 31 ), new Dictionary<string, decimal> { { "TLT", 0.42m }, { "LQD", 0.15m }, { "SPY", 0.43m }, { "CASH", 0.0m } } },
                { new DateTime(2003, 9, 30 ), new Dictionary<string, decimal> { { "TLT", 0.27m }, { "LQD", 0.24m }, { "SPY", 0.5m }, { "CASH", 0.0m } } },
                { new DateTime(2003, 8, 31 ), new Dictionary<string, decimal> { { "TLT", 0.42m }, { "LQD", 0.17m }, { "SPY", 0.41m }, { "CASH", 0.0m } } },
                { new DateTime(2003, 7, 31 ), new Dictionary<string, decimal> { { "TLT", 0.43m }, { "LQD", 0.15m }, { "SPY", 0.42m }, { "CASH", 0.0m } } },
                { new DateTime(2003, 6, 30 ), new Dictionary<string, decimal> { { "TLT", 0.18m }, { "LQD", 0.27m }, { "SPY", 0.55m }, { "CASH", 0.0m } } },
                { new DateTime(2003, 5, 31 ), new Dictionary<string, decimal> { { "TLT", 0.12m }, { "LQD", 0.28m }, { "SPY", 0.6m }, { "CASH", 0.0m } } },
                { new DateTime(2003, 4, 30 ), new Dictionary<string, decimal> { { "TLT", 0.2m }, { "LQD", 0.27m }, { "SPY", 0.53m }, { "CASH", 0.0m } } },
                { new DateTime(2003, 3, 31 ), new Dictionary<string, decimal> { { "TLT", 0.16m }, { "LQD", 0.3m }, { "SPY", 0.54m }, { "CASH", 0.0m } } },
                { new DateTime(2003, 2, 28 ), new Dictionary<string, decimal> { { "TLT", 0.13m }, { "LQD", 0.34m }, { "SPY", 0.53m }, { "CASH", 0.0m } } },
                { new DateTime(2003, 1, 31 ), new Dictionary<string, decimal> { { "TLT", 0.18m }, { "LQD", 0.34m }, { "SPY", 0.48m }, { "CASH", 0.0m } } },
                { new DateTime(2002, 12, 31 ), new Dictionary<string, decimal> { { "TLT", 0.14m }, { "LQD", 0.4m }, { "SPY", 0.46m }, { "CASH", 0.0m } } },
                { new DateTime(2002, 11, 30 ), new Dictionary<string, decimal> { { "TLT", 0.22m }, { "LQD", 0.46m }, { "SPY", 0.33m }, { "CASH", 0.0m } } },
                { new DateTime(2002, 10, 31 ), new Dictionary<string, decimal> { { "TLT", 0.12m }, { "LQD", 0.51m }, { "SPY", 0.37m }, { "CASH", 0.0m } } },
                { new DateTime(2002, 9, 30 ), new Dictionary<string, decimal> { { "TLT", 0.06m }, { "LQD", 0.46m }, { "SPY", 0.48m }, { "CASH", 0.0m } } },
                { new DateTime(2002, 8, 31 ), new Dictionary<string, decimal> { { "TLT", 0.14m }, { "LQD", 0.51m }, { "SPY", 0.35m }, { "CASH", 0.0m } } },
                { new DateTime(2002, 7, 31 ), new Dictionary<string, decimal> { { "TLT", 0.2m }, { "LQD", 0.53m }, { "SPY", 0.27m }, { "CASH", 0.0m } } },
            };

            return;
        }
        public override void OnEndOfAlgorithm()
        {
            Log($"OnEndOfAlgorithm(): Backtest time: {(DateTime.UtcNow - _bnchmarkStartTime).TotalMilliseconds}ms");
        }

    }
}