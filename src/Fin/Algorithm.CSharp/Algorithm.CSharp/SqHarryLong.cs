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

namespace QuantConnect.Algorithm.CSharp
{

    public class SqHarryLong : QCAlgorithm
    {
        bool _isTradeInSqCore = true; // 2 simulation environments. We backtest in Qc cloud or in SqCore frameworks. QcCloud works on per minute resolution (to be able to send MOC orders 20min before MOC), SqCore works on daily resolution only.
        public bool IsTradeInSqCore { get { return _isTradeInSqCore; } }
        public bool IsTradeInQcCloud { get { return !_isTradeInSqCore; } }
        DateTime _startDate = DateTime.MinValue;
        DateTime _endDate = DateTime.MaxValue;
        TimeSpan _warmUp = TimeSpan.Zero;
        Dictionary<string, decimal> _weights = new Dictionary<string, decimal> { { "SPY", 0.6m }, { "TLT", 0.4m } };
        List<string> _tickers = new List<string>();
        string _longestHistTicker = "SPY";
        private int _rebalancePeriodDays = 30;
        private int _lookbackTradingDays = 0;
        private Dictionary<string, Symbol> _symbolsDaily = new Dictionary<string, Symbol>();
        private Dictionary<string, Symbol> _symbolsMinute = new Dictionary<string, Symbol>();
        private Dictionary<string, List<QcPrice>> _rawCloses = new Dictionary<string, List<QcPrice>>();
        private Dictionary<string, List<QcPrice>> _adjCloses = new Dictionary<string, List<QcPrice>>();
        private Dictionary<string, List<QcDividend>> _dividends = new Dictionary<string, List<QcDividend>>();
        private Dictionary<string, List<QcSplit>> _splits = new Dictionary<string, List<QcSplit>>();
        private Dictionary<string, List<QcPrice>> _rawClosesFromYfLists = new Dictionary<string, List<QcPrice>>();
        private Dictionary<string, Dictionary<DateTime, decimal>> _rawClosesFromYfDicts = new Dictionary<string, Dictionary<DateTime, decimal>>();
        DateTime _backtestStartTime;
        private DateTime _lastRebalance = DateTime.MinValue;
        private Dividends _sliceDividends;

        // public void Initialize(string p_param)
        // {
        // }

        public override void Initialize()
        {
            _backtestStartTime = DateTime.UtcNow;
            _startDate = new DateTime(2006, 01, 01); // means Local time, not UTC
            _warmUp = TimeSpan.FromDays(30); // Wind time back 200 calendar days from start
            _endDate = DateTime.Now;
            // _endDate = new DateTime(2023, 02, 28); // means Local time, not UTC

            _tickers = new List<string>(_weights.Keys);

            SetStartDate(_startDate);
            SetEndDate(_endDate);
            SetWarmUp(_warmUp);
            SetCash(10000000);

            AddEquity(_longestHistTicker, Resolution.Daily, dataNormalizationMode: DataNormalizationMode.Raw);
            Orders.MarketOnCloseOrder.SubmissionTimeBuffer = TimeSpan.FromMinutes(0.5); // change the submission time threshold of MOC orders

            _rawClosesFromYfLists = new Dictionary<string, List<QcPrice>>();
            _rawClosesFromYfDicts = new Dictionary<string, Dictionary<DateTime, decimal>>();
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

                if (IsTradeInQcCloud)
                {
                    Symbol symbolMinute = AddEquity(ticker, Resolution.Minute, dataNormalizationMode: DataNormalizationMode.Raw).Symbol;
                    _symbolsMinute.Add(ticker, symbolMinute);
                    Securities[symbolMinute].FeeModel = new ConstantFeeModel(0);
                    Securities[symbolMinute].SetBuyingPowerModel(new SecurityMarginModel(30m)); // equivalent to security.SetLeverage(). Allows to go 30x leverage of this stock if all 20 GC stock are in position with 50% overleverage
                    // Call the DownloadAndProcessData method to get real life close prices from YF
                    QCAlgorithmUtils.DownloadAndProcessYfData(this, ticker, _startDate, _warmUp, _endDate, ref _rawClosesFromYfLists, ref _rawClosesFromYfDicts);
                }
            }
        }
        private void TradeLogic() // this is called at 15:40 in QC cloud, and 00:00 in SqCore
        {
            if (IsWarmingUp) // Dont' trade in the warming up period.
                return;

            TradePreProcess();

            Dictionary<string, List<QcPrice>> usedAdjustedClosePrices = GetUsedAdjustedClosePriceData();
            decimal currentPV = PvCalculation(usedAdjustedClosePrices);
            // Log($"PV on day {this.Time.Date.ToString()}: ${Portfolio.TotalPortfolioValue}.");

            Dictionary<string, Symbol> tradedSymbols = IsTradeInSqCore ? _symbolsDaily : _symbolsMinute;

            string logMessage = $"New positions after close of {this.Time.Date:yyyy-MM-dd}: ";
            string logMessage2 = "Close prices are: ";

            foreach (KeyValuePair<string, Symbol> kvp in tradedSymbols)
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
                if (positionChange != 0)
                {
                    MarketOnCloseOrder(kvp.Value, positionChange); // QC raises Warning if order quantity = 0. So, we don't sent these. "Unable to submit order with id -10 that has zero quantity."
                    if (IsTradeInSqCore && _sliceDividends.ContainsKey(ticker)) // If we use our hacked after market close MOC trading, the dividend credit precedes the trade (cash already contains them). This results incorrect PV and therefore incorrect new positions if the same time slice includes the dividend as the prices. For this reason, before trading, these dividends (which are in the given daily slice) have to be written back. Then after the trade has been executed, they have to be credited again based on the new positions that are already correct.
                        Portfolio.ApplyDividendMOCAfterClose(_sliceDividends[ticker], Portfolio[ticker].Quantity);
                }
                logMessage += ticker + ": " + newPosition + "; ";
                logMessage2 += ticker + ": " + ((tickerUsedAdjustedClosePrices.Count != 0) ? tickerUsedAdjustedClosePrices[(Index)(^1)].Close.ToString() : "N/A") + "; "; // use the last element
            }
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

                if (IsTradeInSqCore && (slice.Time - _lastRebalance).TotalDays >= _rebalancePeriodDays && Securities[_longestHistTicker].Exchange.Hours.IsDateOpen(this.Time.Date.AddDays(-1)))
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
            if (IsTradeInSqCore) // If we use our hacked after market close MOC trading, the dividend credit precedes the trade (cash already contains them). This results incorrect PV and therefore incorrect new positions if the same time slice includes the dividend as the prices. For this reason, before trading, these dividends (which are in the given daily slice) have to be written back. Then after the trade has been executed, they have to be credited again based on the new positions that are already correct.
            {
                foreach (KeyValuePair<string, Symbol> kvp in _symbolsDaily)
                {
                    string ticker = kvp.Key;
                    if (_sliceDividends.ContainsKey(ticker))
                        Portfolio.ApplyDividendMOCAfterClose(_sliceDividends[ticker], -Portfolio[ticker].Quantity);
                }
                return Portfolio.TotalPortfolioValue;
            }

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
            Log($"OnEndOfAlgorithm(): Backtest time: {(DateTime.UtcNow - _backtestStartTime).TotalMilliseconds}ms");
        }

    }
}