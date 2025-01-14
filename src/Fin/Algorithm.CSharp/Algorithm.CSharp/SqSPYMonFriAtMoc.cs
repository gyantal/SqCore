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
    public class SqSPYMonFriAtMoc : QCAlgorithm
    {
        bool _isTradeInSqCore = true; // 2 simulation environments. We backtest in Qc cloud or in SqCore frameworks. QcCloud works on per minute resolution (to be able to send MOC orders 20min before MOC), SqCore works on daily resolution only.
        public bool IsTradeInSqCore { get { return _isTradeInSqCore; } }
        public bool IsTradeInQcCloud { get { return !_isTradeInSqCore; } }

        DateTime _startDate = DateTime.MinValue;
        DateTime _endDate = DateTime.MaxValue;
        TimeSpan _warmUp = TimeSpan.Zero;
        string _ticker = "SPY";
        Symbol _symbol;
        OrderTicket _lastOrderTicket = null;

        List<QcPrice> _rawCloses = new List<QcPrice>(); // comes from QC.OnData(). We use this in SqCore.
        List<QcDividend> _dividends = new List<QcDividend>();
        List<QcPrice> _adjCloses = new List<QcPrice>();

        // keep both List and Dictionary for YF raw price data. List is used for building and adjustment processing. Also, it can be used later if sequential, timely data marching is needed
        List<QcPrice> _rawClosesFromYfList = new List<QcPrice>(); // comes from DownloadAndProcessYfData(). We use this in QcCloud.
        Dictionary<DateTime, decimal> _rawClosesFromYfDict = new Dictionary<DateTime, decimal>(); // Dictionary<DateTime> is about 6x faster to query than List.BinarySearch(). If we know the Key, the DateTime exactly. Which we do know at Rebalancing time.

        // QC Cloud: Trading on Daily resolution has problems. (2023-03: QC started to fix it, but not fully commited to Master branch, and fixed it only for Limit orders, not for MOC). The problem:
        // Daily resolution: Monday, 15:40 trades are not executed on Monday 16:00, which is good (but for wrong reason). It is not executed because data is stale, data is from Saturday:00:00
        // Tuesday-Friday 15:40 trades are executed at 16:00, which is wrong, because daily data only comes at next day 00:00
        DateTime _backtestStartTime;
        // List<string> _tickers = new List<string> { "TSLA", "FB", "AAPL", "NFLX" };
        // private Dictionary<DateTime, List<string>> _requiredEarningsDataDict = new Dictionary<DateTime, List<string>>();

        public override void Initialize()
        {
            _backtestStartTime = DateTime.UtcNow;

            _startDate = new DateTime(2020, 01, 03); // means Local time, not UTC
            _warmUp = TimeSpan.FromDays(200); // Wind time back 200 calendar days from start
            _endDate = DateTime.Now;

            if (!SqBacktestConfig.SqDailyTradingAtMOC)
                _startDate = _startDate.AddDays(1); // Original QC behaviour: first PV will be StartDate() -1, and it uses previous day Close prices on StartDate:00:00 morning. We don't want that. So, increase the date by 1.
            SetStartDate(_startDate);
            // SetEndDate(2021, 02, 26);
            SetEndDate(_endDate); // means Local time, not UTC. That would be DateTime.UtcNow
            SetWarmUp(_warmUp);
            SetCash(100000);

            Orders.MarketOnCloseOrder.SubmissionTimeBuffer = TimeSpan.FromMinutes(0.5); // change the submission time threshold of MOC orders

            Resolution resolutionUsed = IsTradeInSqCore ? Resolution.Daily : Resolution.Minute;
            // if Raw is not specified in AddEquity(), all prices come as Adjusted, and then Dividend is not added to Cash, and nShares are not changed at Split
            _symbol = AddEquity(_ticker, resolutionUsed, dataNormalizationMode: DataNormalizationMode.Raw).Symbol;   // backtest completed in 4.24 seconds. Processing total of 20,402 data points.
            Securities[_symbol].FeeModel = new ConstantFeeModel(0);
           // QCAlgorithmUtils.DownloadAndProcessEarningsData();

            if (IsTradeInQcCloud)
            {
                DownloadAndProcessYfData(_ticker, _startDate, _warmUp, _endDate); // Call the DownloadAndProcessData method to get real life Raw close prices

                Schedule.On(DateRules.EveryDay(_ticker), TimeRules.BeforeMarketClose(_ticker, 20), () => // only create the Schedule timeslices in QC cloud simulation
                {
                    TradeLogic();
                });
            }

            // Schedule.On(DateRules.EveryDay(_ticker), TimeRules.At(TimeSpan.FromMinutes(6 * 60)), () =>  // a schedule at 6:00 am every day
            // {
            //     if (_lastOrderTicket != _lastLoggedOrderTicket)
            //     {
            //         Log($"Order filled: {_ticker} {_lastOrderTicket.QuantityFilled} shares at {_lastOrderTicket.AverageFillPrice} on SubmitTime: {_lastOrderTicket.Time:yyyy-MM-dd HH:mm:ss} UTC"); // the order FillTime is in order._order.LastFillTime
            //         _lastLoggedOrderTicket = _lastOrderTicket;
            //     }
            // });
        }

        private void DownloadAndProcessYfData(string p_ticker, DateTime p_startDate, TimeSpan p_warmUp, DateTime p_endDate)
        {
            long periodStart = QCAlgorithmUtils.DateTimeUtcToUnixTimeStamp(p_startDate - p_warmUp); // e.g. 1647754466
            long periodEnd = QCAlgorithmUtils.DateTimeUtcToUnixTimeStamp(p_endDate.AddDays(1)); // if p_endDate is a fixed date (2023-02-28:00:00), then it has to be increased, otherwise YF doesn't give that day data.

            // Step 1. Get Split data
            string splitCsvUrl = $"https://query1.finance.yahoo.com/v7/finance/download/{p_ticker}?period1={periodStart}&period2={periodEnd}&interval=1d&events=split&includeAdjustedClose=true";
            string splitCsvData = string.Empty;
            try
            {
                splitCsvData = this.Download(splitCsvUrl); // "Date,Stock Splits" or "Date,Stock Splits\n2023-03-07,1:4" or "Date,Stock Splits\n2021-04-23,1:4\n2023-03-07,1:4"
            }
            catch (Exception e)
            {
                Log($"Exception: {e.Message}");
                return;
            }

            List<YfSplit> splits = new List<YfSplit>();
            int rowStartInd = splitCsvData.IndexOf('\n');   // jump over the header Date, Stock Splits
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
                    if (decimal.TryParse(split1Str, out decimal split1) && decimal.TryParse(split2Str, out decimal split2))
                        splits.Add(new YfSplit() { ReferenceDate = date, SplitFactor = decimal.Divide(split1, split2) });
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

            _rawClosesFromYfList = new List<QcPrice>();
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
                    if (decimal.TryParse(closeStr, out decimal close))
                        _rawClosesFromYfList.Add(new QcPrice() { ReferenceDate = date, Close = close });
                }
                rowStartInd = (closeInd != -1) ? priceCsvData.IndexOf('\n', adjCloseInd + 1) : -1;
                rowStartInd = (rowStartInd == -1) ? priceCsvData.Length : rowStartInd + 1; // jump over the '\n'
            }

            // Step 3. Reverse Adjust history data with the splits. Going backwards in time, starting from the EndDay (today)
            if (splits.Count != 0)
            {
                decimal splitMultiplier = 1m;
                int lastSplitIdx = splits.Count - 1;
                DateTime watchedSplitDate = splits[lastSplitIdx].ReferenceDate;

                for (int i = _rawClosesFromYfList.Count - 1; i >= 0; i--)
                {
                    DateTime date = _rawClosesFromYfList[i].ReferenceDate;
                    if (date < watchedSplitDate)
                    {
                        splitMultiplier *= splits[lastSplitIdx].SplitFactor;
                        lastSplitIdx--;
                        watchedSplitDate = (lastSplitIdx == -1) ? DateTime.MinValue : splits[lastSplitIdx].ReferenceDate;
                    }

                    _rawClosesFromYfList[i].Close *= splitMultiplier;
                }
            }

            // Step 4. Convert List to Dictionary, because that is 6x faster to query
            _rawClosesFromYfDict = new Dictionary<DateTime, decimal>(_rawClosesFromYfList.Count);
            for (int i = 0; i < _rawClosesFromYfList.Count; i++)
            {
                var yfPrice = _rawClosesFromYfList[i];
                _rawClosesFromYfDict[yfPrice.ReferenceDate] = yfPrice.Close;
            }
        }

        private void TradeLogic() // Buy&Hold From Monday to Friday. This is called at 15:40 in QC cloud, and 00:00 in SqCore
        {
            if (IsWarmingUp) // Dont' trade in the warming up period.
                return;

            TradePreProcess();

            if (!Securities[_symbol].Exchange.Hours.IsDateOpen(this.Time.Date)) // market holiday can happen on Monday
                return;

            DateTime simulatedTimeLoc = this.Time;  // Local time,  On Daily resolution: Friday daily tradebar comes at 00:00 on Saturday (next day), On Per minute resolution: "1/2/2013 3:40:00 PM"
            if (IsTradeInSqCore)
                if (!SqBacktestConfig.SqDailyTradingAtMOC) // SqDailyTradingAtMOC sends price at 16:00, which is right. No need the change. Without it, price comes 00:00 next morning, so we adjust it back.
                    simulatedTimeLoc = simulatedTimeLoc.AddHours(-8); // from 00:00 approximately go back to 16:00 prior day.

            if (simulatedTimeLoc.DayOfWeek == DayOfWeek.Monday && Portfolio[_symbol].Quantity == 0) // Buy it on Monday if we don't have it already
            {
                decimal priceToUse;
                if (IsTradeInSqCore) // running in SqCore
                {
                    QcPrice qcPrice = _rawCloses[^1];
                    if (qcPrice.ReferenceDate != simulatedTimeLoc.Date)
                        throw new Exception("Historical price for the date is not found.");
                    priceToUse = qcPrice.Close;
                    // priceToUse = 500;
                }
                else // running in QC cloud simulation
                {
                    if (_rawClosesFromYfDict.TryGetValue(simulatedTimeLoc.Date, out decimal peekAheadMocPrice))
                        priceToUse = peekAheadMocPrice;
                    else
                    {
                        Log($"Warning! Date {simulatedTimeLoc.Date} is not found in _rawClosesFromYfDict. Maybe YF download error. We fallback to using perMinute price.");
                        priceToUse = Securities[_symbol].Price; // currentMinutePrice in QC cloud simulation
                    }
                }

                // QC raises Warning if order quantity = 0. So, we don't sent these. "Unable to submit order with id -10 that has zero quantity."
                _lastOrderTicket = MarketOnCloseOrder(_symbol, Math.Round(Portfolio.Cash / priceToUse));
                // _lastOrderTicket = FixPriceOrder(_symbol, Math.Round(Portfolio.Cash / priceToUse), priceToUse);
            }

            if (simulatedTimeLoc.DayOfWeek == DayOfWeek.Friday && Portfolio[_symbol].Quantity > 0) // Sell it on Friday if we have a position
                _lastOrderTicket = MarketOnCloseOrder(_symbol, -Portfolio[_symbol].Quantity);  // Daily Raw: Sell: FillDate: 16:00 today (why???) (on previous day close price!!)
                // _lastOrderTicket = FixPriceOrder(_symbol, -Portfolio[_symbol].Quantity, 550);  // Daily Raw: Sell: FillDate: 16:00 today (why???) (on previous day close price!!)

            // Log($"PV on day {simulatedTimeLoc.Date.ToString()}: ${Portfolio.TotalPortfolioValue}.");
        }

        void TradePreProcess()
        {
        }

        // public override void OnOrderEvent(OrderEvent orderEvent) // called back twice. Once with orderEvent.Status = Sumbitted, later = Filled
        // {
        //     if (orderEvent.Status.IsFill())
        //     {
        //         // write a code that checks the filled.Time = what we expect for the MOC order. daily Resolution: 00:00 or per minute resolution : 16:00 the expected
        //         DateTime fillTimeUtc = orderEvent.UtcTime;
        //         DateTime fillTimeLoc = fillTimeUtc.ConvertFromUtc(this.TimeZone); // Local time zone of the simulation
        //         if (IsTradeInSqCore && !(fillTimeLoc.Hour == 0 && fillTimeLoc.Minute == 0 && fillTimeLoc.Second == 0))
        //             Debug($"Order has been filled for {orderEvent.Symbol} at wrong time! Details: {orderEvent.FillQuantity} shares at {orderEvent.FillPrice} on FillTime: {fillTimeLoc:yyyy-MM-dd HH:mm:ss} local(America/New_York) instead of 0:00:00");
        //         if (IsTradeInQcCloud && !((fillTimeLoc.Hour == 13 || fillTimeLoc.Hour == 16) && fillTimeLoc.Minute == 0 && fillTimeLoc.Second == 0))
        //             Debug($"Order has been filled for {orderEvent.Symbol} at wrong time! Details: {_lastOrderTicket.QuantityFilled} shares at {_lastOrderTicket.AverageFillPrice} on FillTime: {fillTimeLoc:yyyy-MM-dd HH:mm:ss} local(America/New_York) instead of 16:00:00");
        //     }
        // }


        // >OnData event is the primary entry point for your algorithm. Each new data point will be pumped in here.
        // Daily resolution: slice.Time: "1/2/2013 12:00:00 AM" // so it is Local time, it is 00:00 in the morning
        // Minute resolution: slice.Time = this.Time: "1/2/2013 9:31:00 AM" // so it is Local time
        // >TSLA case study of Split on 2022-08-24
        // Real life RAW prices. This is what happened (unadjusting YF rounded prices. Can check https://www.marketbeat.com/stocks/NASDAQ/TSLA/chart/ for unadjusted High/Low)
        // 8/23/2022, 16:00: 889.35
        // 8/24/2022, 16:00: 891.3  // + Split occured AFTER market close (QC considers that) or equivalently BEFORE next day open (YF considers that. YF shows the split for 25th Aug)
        // 8/25/2022, 16:00: 296.07
        // For these in OnData(), we collect the following (which is essentially correct. Note: slice time is the next day 00:01)
        // rawCloses: "8/24/2022 12:00:00 AM:889.3600,8/25/2022 12:00:00 AM:891.29,8/26/2022 12:00:00 AM:296.0700"
        // adjCloses: "8/24/2022 12:00:00 AM:296.45330368800,8/25/2022 12:00:00 AM:297.096636957,8/26/2022 12:00:00 AM:296.0700"
        // QC imagines that we have the RAW price occuring at 16:00, and Split or Dividend occured after MOC and it is applied ONTO that raw price. So, we have to apply that AdjMultiplier for the today data too. Not only for the previous history.
        // >2021-06-21 Monday (Slice.Time is Monday: 00:00, Dividend is for Sunday, but no price bar): QQQ dividend
        // 2022-06-06 Monday (Slice.Time is Monday: 00:00, Split is for Sunday, but no price bar): AMZN split
        public override void OnData(Slice slice) // "slice.Time(Local): 8/23/2022 12:00:00 AM" means early morning on that day, == "slice.Time: 8/23/2022 12:00:01 AM". This gives the day-before data. slice.UtcTime also exists.
        {
            try
            {
                if (IsTradeInQcCloud)
                    return; // in QcCloud, we don't process the incoming data. We try to use peekAhead YF data or perMinute data from symbol.Price

                // Collect Raw prices (and Splits and Dividends) for calculating Adjusted prices
                // Split comes twice. Once: Split.Warning comes 1 day earlier with slice.Time: 8/24/2022 12:00:00, SplitType.SplitOccurred comes on the proper day
                Split occuredSplit = (slice.Splits.ContainsKey(_symbol) && slice.Splits[_symbol].Type == SplitType.SplitOccurred) ? slice.Splits[_symbol] : null; // split.Type can be Warning and SplitOccured. Ignore 1-day early Split Warnings. Just use the occured

                decimal? rawClose = null;
                if (slice.Bars.TryGetValue(_symbol, out TradeBar bar))
                    rawClose = bar.Close; // Probably bug in QC: if there is a Split for the daily bar, then QC SplitAdjust that bar, even though we asked RAW data. QC only does it for the Split day. We can undo it, because the Split.ReferencePrice has the RAW price.

                if (rawClose != null) // we have a split or dividend on Sunday, but there is no bar, so there is no price, which is fine
                {
                    if (SqBacktestConfig.SqDailyTradingAtMOC) // SqDailyTradingAtMOC sends price at 16:00, which is right. No need the change. Without it, price comes 00:00 next morning, so we adjust it back.
                    {
                        _rawCloses.Add(new QcPrice() { ReferenceDate = slice.Time.Date, Close = (decimal)rawClose });
                        _adjCloses.Add(new QcPrice() { ReferenceDate = slice.Time.Date, Close = (decimal)rawClose });
                    }
                    else
                    {
                        _rawCloses.Add(new QcPrice() { ReferenceDate = slice.Time.Date.AddDays(-1), Close = (decimal)rawClose });
                        _adjCloses.Add(new QcPrice() { ReferenceDate = slice.Time.Date.AddDays(-1), Close = (decimal)rawClose });
                    }
                }

                // string lastRaw2 = string.Join(",", _rawCloses.TakeLast(4).Select(r => r.QuoteTime.ToString() + ":" + r.Close.ToString())); // "8/24/2022 12:00:00 AM:889.3600,8/25/2022 12:00:00 AM:891.29,8/26/2022 12:00:00 AM:296.0700"
                // string lastAdj2 = string.Join(",", _adjCloses.TakeLast(4).Select(r => r.QuoteTime.ToString() + ":" + r.Close.ToString())); // "8/24/2022 12:00:00 AM:296.45330368800,8/25/2022 12:00:00 AM:297.096636957,8/26/2022 12:00:00 AM:296.0700"

                if (slice.Dividends.ContainsKey(_symbol))
                {
                    var dividend = slice.Dividends[_symbol];
                    if (SqBacktestConfig.SqDailyTradingAtMOC) // SqDailyTradingAtMOC sends price at 16:00, which is right. No need the change. Without it, price comes 00:00 next morning, so we adjust it back.
                        _dividends.Add(new QcDividend() { ReferenceDate = slice.Time.Date, Dividend = dividend });
                    else
                        _dividends.Add(new QcDividend() { ReferenceDate = slice.Time.Date.AddDays(-1), Dividend = dividend });

                    decimal divAdjMultiplicator = 1 - dividend.Distribution / dividend.ReferencePrice;
                    for (int i = 0; i < _adjCloses.Count; i++)
                    {
                        _adjCloses[i].Close *= divAdjMultiplicator;
                    }
                }

                if (occuredSplit != null)  // Split.SplitOccurred comes on the correct day with slice.Time: 8/25/2022 12:00:00
                {
                    decimal splitAdjMultiplicator = occuredSplit.SplitFactor;
                    for (int i = 0; i < _adjCloses.Count - 1; i++)  // Not-chosen option: if we 'have to' use QC bug 'wrongly-adjusted' rawClose, we can skip the last item. In that case we don't apply the split adjustment to the last item, which is the same day as the day of Split.
                        _adjCloses[i].Close *= splitAdjMultiplicator;
                }

                if (IsTradeInSqCore) // only create the Schedule timeslices in QC cloud simulation
                    TradeLogic();
            }
            catch (System.Exception e)
            {
                Log($"Error. Exception in OnData(Slice). Slice.Time: {slice.Time}, msg: {e.Message}");
            }
        }

        public override void OnEndOfAlgorithm()
        {
            Log($"OnEndOfAlgorithm(): Backtest time: {(DateTime.UtcNow - _backtestStartTime).TotalMilliseconds}ms");
        }
    }
}