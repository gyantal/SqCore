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
    class QcPrice
    {
        public DateTime QuoteTime;
        public DateTime ReferenceDate;
        public decimal Close;
    }

    class Price
    {
        public DateTime ReferenceDate;
        public decimal Close;
    }

    class SqDividend
    {
        public DateTime QuoteTime;
        public DateTime ReferenceDate;
        public Dividend Dividend;
    }

    class SqSplit
    {
        public DateTime QuoteTime;
        public DateTime ReferenceDate;
        public Split Split;
    }


    public class SQ_BuyAndSellSPYAtMOC : QCAlgorithm
    {
        string _ticker = "SPY";
        Symbol _symbolDaily, _symbolMinute;
        OrderTicket lastOrderTicket = null;
   
        List<QcPrice> rawCloses = new List<QcPrice>();
        List<SqDividend> dividends = new List<SqDividend>();
        List<SqSplit> splits = new List<SqSplit>();
        List<QcPrice> adjCloses = new List<QcPrice>();

        // Trading on Daily resolution has problems. (2023-03: QC started to fix it, but not fully commited to Master branch, and fixed it only for Limit orders, not for MOC). The problem:
        // Monday, 15:40 trades are not executed on Monday 16:00, which is good (but for wrong reason). It is not executed because data is stale, data is from Saturday:00:00
        // Tuesday-Friday 15:40 trades are executed at 16:00, which is wrong, because daily data only comes at next day 00:00
        // We could fix it in local VsCode execution, but not on QC cloud.
        bool _isTradeOnMinuteResolution = true; // until QC fixes the Daily resolution trading problem, use per minute for QC Cloud running. But don't use it in local VsCode execution.
        DateTime _backtestStartTime;

        public override void Initialize()
        {
            _backtestStartTime = DateTime.UtcNow;
            SetStartDate(2020, 01, 03); // means Local time, not UTC
            SetEndDate(2021, 02, 26);
            // SetStartDate(2022, 01, 01); // means Local time, not UTC
            // SetEndDate(DateTime.Now); // means Local time, not UTC. That would be DateTime.UtcNow
            SetWarmUp(TimeSpan.FromDays(200)); // Wind time back 100 calendar days from start
            SetCash(100000);

            Orders.MarketOnCloseOrder.SubmissionTimeBuffer = TimeSpan.FromMinutes(0.5); // change the submission time threshold of MOC orders

            // if Raw is not specified in AddEquity(), all prices come as Adjusted, and then Dividend is not added to Cash, and nShares are not changed at Split
            //_symbol = AddEquity(_ticker, Resolution.Minute).Symbol;    // backtest completed in 17.10 seconds. Processing total of 2,000,849 data points.
            _symbolDaily = AddEquity(_ticker, Resolution.Daily, dataNormalizationMode: DataNormalizationMode.Raw).Symbol;   // backtest completed in 4.24 seconds. Processing total of 20,402 data points.
            Securities[_symbolDaily].FeeModel = new ConstantFeeModel(0);

            if (_isTradeOnMinuteResolution)
            {
                _symbolMinute = AddEquity(_ticker, Resolution.Minute, dataNormalizationMode: DataNormalizationMode.Raw).Symbol;
                Securities[_symbolMinute].FeeModel = new ConstantFeeModel(0);
            }
            

            Schedule.On(DateRules.EveryDay(_ticker), TimeRules.BeforeMarketClose(_ticker, 20), () =>
            {
                if (!IsWarmingUp)
                    AtRebalancePreProcess();
                // this.Time.ToString(): "1/2/2013 3:40:00 PM" // so it is Local time

                // Debug: double check that Dates are aligned
                // Dictionary<Symbol, List<Price>> usedPrices;
                // DateTime? firstSymbolDate = null;
                // foreach (var symbol in Symbols)
                // {
                //     if (firstSymbolDate == null)
                //         firstSymbolDate = usedPrices[symbol][0].ReferenceDate;
                //     else
                //         if (usedPrices[symbol][0].ReferenceDate != firstSymbolDate)
                //             throw new Exception("message");
                // }

                // Using Daily Raw, MarketOnCloseOrder seems to be a disaster, because Buy is OK (uses today's MOC price), but Sell is done on previous day Close
                // Using Daily Raw, Maybe better to use MarketOrder. At least Buy/Sell is consistent: FillDate: 15:40 today on previous day close price
                // If using Daily Raw, and using MarketOrder(). Warning: "Warning: No quote information available at 01/07/2022 00:00:00 America/New_York, order filled using TradeBar data"

                // we might have to read more forum about this Resolution.Daily simulation. It is less common, but I bet some people (like us) try to use Daily simulation.

                Symbol tradedSymbol = _isTradeOnMinuteResolution ? _symbolMinute : _symbolDaily;

                if (!IsWarmingUp && this.Time.DayOfWeek == DayOfWeek.Monday && Portfolio[tradedSymbol].Quantity == 0)
                {
                    var currentMinutePrice = Securities[tradedSymbol].Price; // Daily Raw: price is the Close of the previous day
                    // lastOrderTicket = MarketOnCloseOrder(_symbol, 10);
                    lastOrderTicket = MarketOnCloseOrder(tradedSymbol, Math.Round(Portfolio.Cash / currentMinutePrice)); // Daily Raw: and MOC order uses that day MOC price, Buy: FillDate: 00:00 on next day // Minute Raw: Buy FillDate: 16:00 today
                    // lastOrderTicket = MarketOrder(_symbol, Math.Round(Portfolio.Cash / currentMinutePrice)); // Daily Raw: Buy: FillDate: 15:40 today on previous day close price.
                    // lastOrderTicket = MarketOrder(_symbol, Math.Round(Portfolio.Cash / rawCloses[rawCloses.Count - 1].Close));
                }

                if (this.Time.DayOfWeek == DayOfWeek.Friday && Portfolio[tradedSymbol].Quantity > 0)
                    lastOrderTicket = MarketOnCloseOrder(tradedSymbol, -Portfolio[tradedSymbol].Quantity);  // Daily Raw: Sell: FillDate: 16:00 today (why???) (on previous day close price!!)
                                                                                                  // lastOrderTicket = MarketOrder(_symbol, -Portfolio[_symbol].Quantity); // Daily Raw: Sell: FillDate: 15:40 today on previous day close price.
            });


            Schedule.On(DateRules.EveryDay(_ticker), TimeRules.BeforeMarketClose(_ticker, -1), () =>  // a schedule after close
            {
                if (lastOrderTicket != null && lastOrderTicket.Time.Date == this.Time.Date)
                    Log($"Order filled: {_ticker} {lastOrderTicket.QuantityFilled} shares at {lastOrderTicket.AverageFillPrice} on SubmitTime: {lastOrderTicket.Time:yyyy-MM-dd HH:mm:ss} UTC"); // the order FillTime is in order._order.LastFillTime
            });
        }

        public override void OnOrderEvent(OrderEvent orderEvent)
        {
            if (orderEvent.Status.IsFill())
            {
                // write a code that checkes the filled.Time = what we expect for the MOC order. daily Resolution: 00:00 or per minute resolution : 16:00 the expected)
                // bool isOrderTimeAsExpected = true;
                DateTime fillTimeUtc = orderEvent.UtcTime;
                DateTime fillTimeLoc = fillTimeUtc.ConvertFromUtc(this.TimeZone); // Local time zone of the simulation
                if (lastOrderTicket.OrderType == OrderType.MarketOnClose && !_isTradeOnMinuteResolution && !(fillTimeLoc.Hour == 0 && fillTimeLoc.Minute == 0 && fillTimeLoc.Second == 0))
                    // isOrderTimeAsExpected = false;
                    Debug($"Order has been filled for {orderEvent.Symbol} at wrong time! Details: {lastOrderTicket.QuantityFilled} shares at {lastOrderTicket.AverageFillPrice} on FillTime: {fillTimeLoc:yyyy-MM-dd HH:mm:ss} local(America/New_York) instead of 0:00:00");
                if (lastOrderTicket.OrderType == OrderType.MarketOnClose && _isTradeOnMinuteResolution && !((fillTimeLoc.Hour == 13 || fillTimeLoc.Hour == 16) && fillTimeLoc.Minute == 0 && fillTimeLoc.Second == 0))
                    // isOrderTimeAsExpected = false;
                    Debug($"Order has been filled for {orderEvent.Symbol} at wrong time! Details: {lastOrderTicket.QuantityFilled} shares at {lastOrderTicket.AverageFillPrice} on FillTime: {fillTimeLoc:yyyy-MM-dd HH:mm:ss} local(America/New_York) instead of 16:00:00");
            }
        }

        void AtRebalancePreProcess()
        {
        }

        // public override void OnEndOfDay(string symbol)  // this.Time is 15:50 which is strange, because "Method is called 10 minutes before closing to allow user to close out position."
        // {
        //     if (lastOrderTicket != null && lastOrderTicket.Time.Date == this.Time.Date)
        //         Log($"OnEndOfDay(), Order filled: {_symbol} {lastOrderTicket.QuantityFilled} shares at {lastOrderTicket.AverageFillPrice} on {lastOrderTicket.Time:yyyy-MM-dd hh:mm:ss} UTC"); // the order FillTime is in order._order.LastFillTime
        // }


        // // TradeBars objects are piped into this method. For a given day:  OnData(TradeBars) is called first. Then OnData(Slice data). Then Schedule.On()
        // public void OnData(TradeBars data) // data.ToString(): "1/2/2013 12:00:00 AM" // so it is Local time
        // {
        //     Debug("????????got TradeBars ");
        // }

        // // OnData event is the primary entry point for your algorithm. Each new data point will be pumped in here.
        // // <param name="data">Slice object keyed by symbol containing the stock data</param>
        // // Daily resolution: slice.Time: "1/2/2013 12:00:00 AM" // so it is Local time
        // // Minute resolution: slice.Time: "1/2/2013 9:31:00 AM" // so it is Local time
        // // Minute resolution: this.Time: "1/2/2013 9:31:00 AM" // so it is Local time

        // TSLA case study of Split on 2022-08-24
        // Real life RAW prices. This is what happened (unadjusting YF rounded prices. Can check https://www.marketbeat.com/stocks/NASDAQ/TSLA/chart/ for unadjusted High/Low)
        // 8/23/2022, 16:00: 889.35
        // 8/24/2022, 16:00: 891.3  // + Split occured AFTER market close (QC considers that) or equivalently BEFORE next day open (YF considers that. YF shows the split for 25th Aug)
        // 8/25/2022, 16:00: 296.07
        // For these in OnData(), we collect the following (which is essentially correct. Note: slice time is the next day 00:01)
        // rawCloses: "8/24/2022 12:00:00 AM:889.3600,8/25/2022 12:00:00 AM:891.29,8/26/2022 12:00:00 AM:296.0700"
        // adjCloses: "8/24/2022 12:00:00 AM:296.45330368800,8/25/2022 12:00:00 AM:297.096636957,8/26/2022 12:00:00 AM:296.0700"
        // QC imagines that we have the RAW price occuring at 16:00, and Split or Dividend occured after MOC and it is applied ONTO that raw price. So, we have to apply that AdjMultiplier for the today data too. Not only for the previous history.

        //2021-06-21 Monday (Slice.Time is Monday: 00:00, Dividend is for Sunday, but no price bar): QQQ dividend
        //2022-06-06 Monday (Slice.Time is Monday: 00:00, Split is for Sunday, but no price bar): AMZN split
        public override void OnData(Slice slice) // "slice.Time: 8/23/2022 12:00:00 AM" means early morning on that day, == "slice.Time: 8/23/2022 12:00:01 AM". This gives the day-before data.
        {
            try
            {
                bool isDataPerMinute = !(slice.Time.Hour == 0 && slice.Time.Minute == 0);
                if (_isTradeOnMinuteResolution && isDataPerMinute)
                    return;

                string sliceTime = slice.Time.ToString();
                // Log($"OnData(Slice). Slice.Time: {slice.Time}, this.Time: {sliceTime}");
                // if (slice.Time.Date == new DateTime(2022, 06, 06) || slice.Time.Date == new DateTime(2022, 08, 24) || slice.Time.Date == new DateTime(2022, 08, 25) || slice.Time.Date == new DateTime(2022, 08, 26))
                // {
                //     string msg = $"Place your data here for Debug";
                //     Log($"OnData(Slice). Slice.Time: {sliceTime}, data: {msg}");
                // }

                // Collect Raw prices (and Splits and Dividends) for calculating Adjusted prices
                Split occuredSplit = (slice.Splits.ContainsKey(_symbolDaily) && slice.Splits[_symbolDaily].Type == SplitType.SplitOccurred) ? slice.Splits[_symbolDaily] : null; // split.Type can be Warning and SplitOccured. Ignore 1-day early Split Warnings. Just use the occured

                decimal? rawClose = null;
                if (slice.Bars.TryGetValue(_symbolDaily, out TradeBar bar))
                    rawClose = bar.Close; // Probably bug in QC: if there is a Split for the daily bar, then QC SplitAdjust that bar, even though we asked RAW data. QC only does it for the Split day. We can undo it, because the Split.ReferencePrice has the RAW price.

                if (rawClose != null) // we have a split or dividend on Sunday, but there is no bar, so there is no price, which is fine
                {
                    if (occuredSplit != null)
                        rawClose = occuredSplit.ReferencePrice; // ReferencePrice is RAW, not adjusted. Fixing QC bug of giving SplitAdjusted bar on Split day.
                                                                // clPrice = slice.Splits[_symbol].Price; // Price is an alias to Value. Value is this: For streams of data this is the price now, for OHLC packets this is the closing price.            

                    rawCloses.Add(new QcPrice() { QuoteTime = slice.Time, ReferenceDate = slice.Time.Date.AddDays(-1), Close = (decimal)rawClose });
                    adjCloses.Add(new QcPrice() { QuoteTime = slice.Time, ReferenceDate = slice.Time.Date.AddDays(-1), Close = (decimal)rawClose });
                }

                string lastRaw2 = string.Join(",", rawCloses.TakeLast(4).Select(r => r.QuoteTime.ToString() + ":" + r.Close.ToString())); // "8/24/2022 12:00:00 AM:889.3600,8/25/2022 12:00:00 AM:891.29,8/26/2022 12:00:00 AM:296.0700"
                string lastAdj2 = string.Join(",", adjCloses.TakeLast(4).Select(r => r.QuoteTime.ToString() + ":" + r.Close.ToString())); // "8/24/2022 12:00:00 AM:296.45330368800,8/25/2022 12:00:00 AM:297.096636957,8/26/2022 12:00:00 AM:296.0700"

                if (slice.Dividends.ContainsKey(_symbolDaily))
                {
                    var dividend = slice.Dividends[_symbolDaily];
                    dividends.Add(new SqDividend() { QuoteTime = slice.Time, ReferenceDate = slice.Time.Date.AddDays(-1), Dividend = dividend });
                    decimal divAdjMultiplicator = 1 - dividend.Distribution / dividend.ReferencePrice;
                    for (int i = 0; i < adjCloses.Count; i++)
                    {
                        adjCloses[i].Close *= divAdjMultiplicator;
                    }
                }
                // if (slice.Splits.ContainsKey(_symbol) && slice.Splits[_symbol].Type == SplitType.Warning)  // Split.Warning comes 1 day earlier with slice.Time: 8/24/2022 12:00:00
                // {
                //     var split = slice.Splits[_symbol];  // split.Type can be Warning and SplitOccured
                //     string msg = $"SplitType.Warning. slice.Time: {slice.Time.ToString()}";
                //     Log($"OnData(Slice). Slice.Time: {slice.Time}, msg: {msg}");
                // }

                if (occuredSplit != null)  // Split.SplitOccurred comes on the correct day with slice.Time: 8/25/2022 12:00:00
                {
                    splits.Add(new SqSplit() { QuoteTime = slice.Time, ReferenceDate = slice.Time.Date.AddDays(-1), Split = occuredSplit });
                    decimal refPrice = occuredSplit.ReferencePrice;    // Contains RAW price (before Split adjustment). Not used here.
                    decimal splitAdjMultiplicator = occuredSplit.SplitFactor;
                    for (int i = 0; i < adjCloses.Count; i++)  // Not-chosen option: if we 'have to' use QC bug 'wrongly-adjusted' rawClose, we can skip the last item. In that case we don't apply the split adjustment to the last item, which is the same day as the day of Split.
                    {
                        adjCloses[i].Close *= splitAdjMultiplicator;
                    }
                }
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