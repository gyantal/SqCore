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
using System.Web;
using System.Collections.Specialized;
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

    public class SqDualMomentum : QCAlgorithm
    {
#if TradeInSqCore // 2 simulation environments. We backtest in Qc cloud or in SqCore frameworks. QcCloud works on per minute resolution (to be able to send MOC orders 20min before MOC), SqCore works on daily resolution only.
        bool _isTradeInSqCore = true;
        // AlgorithmParam comes from QCAlgorithm.QCAlgorithm in SqCore framework
#else
        bool _isTradeInSqCore = false;
        public string AlgorithmParam { get; set; } = "startDate=2006-01-01&endDate=now&assets=VNQ,EEM,DBC,SPY,TLT,SHY&lookback=63&noETFs=3"; // in SqCore, this comes from the backtester environment as Portfolio.AlgorithmParam
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

        // List<string> _tickers = new List<string> { "VNQ", "EEM", "DBC", "SPY", "TLT", "SHY" };
        List<string> _tickers;
        private int _lookbackTradingDays;
        private int _numberOfEtfsSelected;
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
            ProcessAlgorithmParam(algorithmParamQuery, out _tickers, out _lookbackTradingDays, out _numberOfEtfsSelected);

            // *** Step 1: general initializations
            SetCash(10000000);
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

            // *** Only in QcCloud: YF data download. Part 1
            if (IsTradeInQcCloud) // only in QC cloud: we need not only daily, but perMinute symbols too, because we use perMinute symbols for trading.
            {
                foreach (string ticker in _tickers)
                {
                    Symbol symbolMinute = AddEquity(ticker, Resolution.Minute, dataNormalizationMode: DataNormalizationMode.Raw).Symbol;
                    _symbolsMinute.Add(ticker, symbolMinute);
                    Securities[symbolMinute].FeeModel = new ConstantFeeModel(0);
                    Securities[symbolMinute].SetBuyingPowerModel(new SecurityMarginModel(30m)); // equivalent to security.SetLeverage(). Allows to go 30x leverage of this stock if all 20 GC stock are in position with 50% overleverage
                }
            }
            _tradedSymbols = IsTradeInSqCore ? _symbolsDaily : _symbolsMinute;

            // *** Step 2: startDate and warmup determination
            _warmUp = TimeSpan.FromDays(200); // Wind time back X calendar days from start Before the _startDate. It is calendar day. E.g. If strategy need %chgPrevDay, it needs 2 trading day data. Probably set warmup to 2+2+1 = 5+ (for 2 weekend, 1 holiday).
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
            SetStartDate(_startDate); // by default it is 1998-01-02. If we don't call SetStartDate(), still, the PV value chart will start from 1998
            SetEndDate(_endDate);
            // SetBenchmark("SPY"); // the default benchmark is SPY, which is OK in the cloud. In SqCore, we removed the default SPY benchmark, because we don't need it.")

            // *** Only in QcCloud: YF data download. Part 2
            if (IsTradeInQcCloud) // only in QC cloud: we need not only daily, but perMinute symbols too, because we use perMinute symbols for trading.
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

        public static void ProcessAlgorithmParam(NameValueCollection p_AlgorithmParamQuery, out List<string> p_tickers, out int p_lookbackTradingDays, out int p_numberOfEtfsSelected)
        {
            // e.g. _AlgorithmParam = "assets=VNQ,EEM,DBC,SPY,TLT,SHY&lookback=63&noETFs=3"
            string[] tickers = p_AlgorithmParamQuery.Get("assets")?.Split(',', StringSplitOptions.RemoveEmptyEntries) ?? Array.Empty<string>();
            p_tickers = new List<string>(tickers);

            string lookbackTradingDays = p_AlgorithmParamQuery.Get("lookback");
            if (!int.TryParse(lookbackTradingDays, out p_lookbackTradingDays))
                p_lookbackTradingDays = 63;

            string numberOfEtfsSelected = p_AlgorithmParamQuery.Get("noETFs");
            if (!int.TryParse(numberOfEtfsSelected, out p_numberOfEtfsSelected))
                p_numberOfEtfsSelected = 3;
        }

        private void TradeLogic() // this is called at 15:40 in QC cloud, and 00:00 in SqCore
        {
            if (IsWarmingUp) // Dont' trade in the warming up period.
                return;

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

                if (IsTradeInSqCore && _isEndOfMonth)
                {
                    _sliceDividends = slice.Dividends;
                    TradeLogic();
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

        private Dictionary<string, decimal> HistPerfCalc(Dictionary<string, List<QcPrice>> p_usedAdjustedClosePrices)
        {
            Dictionary<string, decimal> relativeMomentums = new Dictionary<string, decimal>();

            foreach (string key in p_usedAdjustedClosePrices.Keys)
            {
                decimal relMom = -99;
                List<QcPrice> usedAdjustedClosePrice = p_usedAdjustedClosePrices[key];
                if (usedAdjustedClosePrice.Count == _lookbackTradingDays + 1)
                    relMom = usedAdjustedClosePrice[^1].Close / usedAdjustedClosePrice[0].Close - 1; // last element divided by the first
                relativeMomentums.Add(key, relMom);
            }
            // Convert the relativeMomentum dictionary to a list of key-value pairs
            List<KeyValuePair<string, decimal>> list = relativeMomentums.ToList();

            // Sort the list based on the values
            list.Sort((x, y) => y.Value.CompareTo(x.Value));

            // Assign a rank to each key and store the result in a new dictionary. It can handle if there is tie in ranks.
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
            // Calculate the appropriate weights for next month. It will be used during trading process.
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