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

    class QcDividend
    {
        public DateTime QuoteTime;
        public DateTime ReferenceDate;
        public Dividend Dividend;
    }

    class QcSplit
    {
        public DateTime QuoteTime;
        public DateTime ReferenceDate;
        public Split Split;
    }


    class SqPrice
    {
        public DateTime ReferenceDate;
        public decimal Close;
    }
    class SqSplit
    {
        public DateTime ReferenceDate;
        public decimal SplitFactor;
    }


    public class SQ_BuyAndSellSPYAtMOC : QCAlgorithm
    {
        DateTime _startDate = DateTime.MinValue;
        DateTime _endDate = DateTime.MaxValue;
        TimeSpan _warmUp = TimeSpan.Zero;
        string _ticker = "SPY";
        Symbol _symbolDaily, _symbolMinute;
        OrderTicket _lastOrderTicket = null;
        bool _wasLastOrderTicketLogged = false;
   
        List<QcPrice> _rawCloses = new List<QcPrice>();
        List<QcDividend> _dividends = new List<QcDividend>();
        List<QcSplit> _splits = new List<QcSplit>();
        List<QcPrice> _adjCloses = new List<QcPrice>();

        List<SqPrice> _rawClosesFromYfList = new List<SqPrice>(); // keep both List and Dictionary for YF raw price data. List is used for building and adjustment processing. Also, it can be used later if sequential, timely data marching is needed
        Dictionary<DateTime, decimal> _rawClosesFromYfDict = new Dictionary<DateTime, decimal>(); // Dictionary<DateTime> is about 6x faster to query than List.BinarySearch(). If we know the Key, the DateTime exactly. Which we do know at Rebalancing time.

        // Trading on Daily resolution has problems. (2023-03: QC started to fix it, but not fully commited to Master branch, and fixed it only for Limit orders, not for MOC). The problem:
        // Monday, 15:40 trades are not executed on Monday 16:00, which is good (but for wrong reason). It is not executed because data is stale, data is from Saturday:00:00
        // Tuesday-Friday 15:40 trades are executed at 16:00, which is wrong, because daily data only comes at next day 00:00
        // We could fix it in local VsCode execution, but not on QC cloud.
        bool _isTradeOnMinuteResolution = false; // until QC fixes the Daily resolution trading problem, use per minute for QC Cloud running. But don't use it in local VsCode execution.
        DateTime _backtestStartTime;

        public override void Initialize()
        {
            _backtestStartTime = DateTime.UtcNow;

            _startDate = new DateTime(2020, 01, 03); // means Local time, not UTC
            _warmUp = TimeSpan.FromDays(200); // Wind time back 200 calendar days from start
            _endDate = DateTime.Now;

            SetStartDate(_startDate); 
            // SetEndDate(2021, 02, 26);
            SetEndDate(_endDate); // means Local time, not UTC. That would be DateTime.UtcNow
            SetWarmUp(_warmUp); 
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

            // Call the DownloadAndProcessData method to get real life Raw close prices
            DownloadAndProcessData(_ticker, _startDate, _warmUp, _endDate);
            

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
                    decimal currentMinutePrice = Securities[tradedSymbol].Price; // Daily Raw: price is the Close of the previous day

                    decimal priceToUse = currentMinutePrice;
                    if (_rawClosesFromYfDict.TryGetValue(this.Time.Date, out decimal peekAheadMocPrice))
                        priceToUse = peekAheadMocPrice;
                    else
                        Log($"Warning! Date {this.Time.Date} is not found in _rawClosesFromYfDict");

                    // QC raises Warning if order quantity = 0. So, we don't sent these. "Unable to submit order with id -10 that has zero quantity."

                    // lastOrderTicket = MarketOnCloseOrder(_symbol, 10);
                    _lastOrderTicket = MarketOnCloseOrder(tradedSymbol, Math.Round(Portfolio.Cash / priceToUse)); // Daily Raw: and MOC order uses that day MOC price, Buy: FillDate: 00:00 on next day // Minute Raw: Buy FillDate: 16:00 today
                    _wasLastOrderTicketLogged = false;
                    // lastOrderTicket = MarketOrder(_symbol, Math.Round(Portfolio.Cash / currentMinutePrice)); // Daily Raw: Buy: FillDate: 15:40 today on previous day close price.
                    // lastOrderTicket = MarketOrder(_symbol, Math.Round(Portfolio.Cash / rawCloses[rawCloses.Count - 1].Close));
                }

                if (this.Time.DayOfWeek == DayOfWeek.Friday && Portfolio[tradedSymbol].Quantity > 0)
                {
                    _lastOrderTicket = MarketOnCloseOrder(tradedSymbol, -Portfolio[tradedSymbol].Quantity);  // Daily Raw: Sell: FillDate: 16:00 today (why???) (on previous day close price!!)
                    _wasLastOrderTicketLogged = false;
                }
                // lastOrderTicket = MarketOrder(_symbol, -Portfolio[_symbol].Quantity); // Daily Raw: Sell: FillDate: 15:40 today on previous day close price.
            });


            Schedule.On(DateRules.EveryDay(_ticker), TimeRules.At(TimeSpan.FromMinutes(6 * 60)), () =>  // a schedule at 6:00 am every day
            {
                if (!_wasLastOrderTicketLogged && _lastOrderTicket != null)
                {
                    Log($"Order filled: {_ticker} {_lastOrderTicket.QuantityFilled} shares at {_lastOrderTicket.AverageFillPrice} on SubmitTime: {_lastOrderTicket.Time:yyyy-MM-dd HH:mm:ss} UTC"); // the order FillTime is in order._order.LastFillTime
                    _wasLastOrderTicketLogged = true;
                }

                // if (_lastOrderTicket != null && _lastOrderTicket.Time.Date == this.Time.Date)
                //     Log($"Order filled: {_ticker} {_lastOrderTicket.QuantityFilled} shares at {_lastOrderTicket.AverageFillPrice} on SubmitTime: {_lastOrderTicket.Time:yyyy-MM-dd HH:mm:ss} UTC"); // the order FillTime is in order._order.LastFillTime
            });
        }

        private void DownloadAndProcessData(string p_ticker, DateTime p_startDate, TimeSpan p_warmUp, DateTime p_endDate)
        {
            long periodStart = DateTimeUtcToUnixTimeStamp(p_startDate - p_warmUp); // e.g. 1647754466
            long periodEnd = DateTimeUtcToUnixTimeStamp(p_endDate.AddDays(1)); // if p_endDate is a fixed date (2023-02-28:00:00), then it has to be increased, otherwise YF doesn't give that day data.

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

            // bool isSplitFirstRawProcessed = false;
            // foreach (string row in splitCsvData.Split('\n')) // leaving the String.Split() version here for a while for potential Debugging purposes
            // {
            //     if (!isSplitFirstRawProcessed)
            //     {
            //         isSplitFirstRawProcessed = true;
            //         continue;
            //     }

            //     int splitStartInd = row.IndexOf(',');
            //     int splitMidInd = (splitStartInd != -1) ? row.IndexOf(':', splitStartInd + 1) : -1;

            //     string dateStr = (splitStartInd != -1) ? row.Substring(0, splitStartInd) : string.Empty;
            //     string split1Str = (splitStartInd != -1 && splitMidInd != -1) ? row.Substring(splitStartInd + 1, splitMidInd - splitStartInd - 1) : string.Empty;
            //     string split2Str = (splitMidInd != -1) ? row.Substring(splitMidInd + 1) : string.Empty;

            //     if (DateTime.TryParseExact(dateStr, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out DateTime date))
            //     {
            //         if (Decimal.TryParse(split1Str, out decimal split1) && Decimal.TryParse(split2Str, out decimal split2))
            //             splits.Add(new YfSplit() { ReferenceDate = date, SplitFactor = decimal.Divide(split1, split2) });
            //     }
            // }

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

            _rawClosesFromYfList = new List<SqPrice>();
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
                        _rawClosesFromYfList.Add(new SqPrice() { ReferenceDate = date, Close = close });
                }
                rowStartInd = (closeInd != -1) ? priceCsvData.IndexOf('\n', adjCloseInd + 1) : -1;
                rowStartInd = (rowStartInd == -1) ? priceCsvData.Length : rowStartInd + 1; // jump over the '\n'
            }


            // bool isFirstRawProcessed = false; // leaving the String.Split() version here for a while for potential Debugging purposes
            // foreach (string row in priceCsvData.Split('\n'))    // chronological processing: it goes forward in time. Starting with StartDate
            // {
            //     if (!isFirstRawProcessed)
            //     {
            //         isFirstRawProcessed = true;
            //         continue;
            //     }

            //     // (Raw)Close is non adjusted for dividend, but adjusted for split.
            //     int openInd = row.IndexOf(',');
            //     int highInd = (openInd != -1) ? row.IndexOf(',', openInd + 1) : -1;
            //     int lowInd = (highInd != -1) ? row.IndexOf(',', highInd + 1) : -1;
            //     int closeInd = (lowInd != -1) ? row.IndexOf(',', lowInd + 1) : -1;
            //     int adjCloseInd = (closeInd != -1) ? row.IndexOf(',', closeInd + 1) : -1;

            //     string dateStr = (openInd != -1) ? row.Substring(0, openInd) : string.Empty;
            //     string closeStr = (closeInd != -1 && adjCloseInd != -1) ? row.Substring(closeInd + 1, adjCloseInd - closeInd - 1) : string.Empty;

            //     if (DateTime.TryParseExact(dateStr, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out DateTime date))
            //     {
            //         if (Decimal.TryParse(closeStr, out decimal close))
            //             _rawClosesFromYfList.Add(new YfPrice() { ReferenceDate = date, Close = close });
            //     }                
            // }

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

        public override void OnOrderEvent(OrderEvent orderEvent)
        {
            if (orderEvent.Status.IsFill())
            {
                // write a code that checkes the filled.Time = what we expect for the MOC order. daily Resolution: 00:00 or per minute resolution : 16:00 the expected)
                // bool isOrderTimeAsExpected = true;
                DateTime fillTimeUtc = orderEvent.UtcTime;
                DateTime fillTimeLoc = fillTimeUtc.ConvertFromUtc(this.TimeZone); // Local time zone of the simulation
                if (_lastOrderTicket.OrderType == OrderType.MarketOnClose && !_isTradeOnMinuteResolution && !(fillTimeLoc.Hour == 0 && fillTimeLoc.Minute == 0 && fillTimeLoc.Second == 0))
                    // isOrderTimeAsExpected = false;
                    Debug($"Order has been filled for {orderEvent.Symbol} at wrong time! Details: {_lastOrderTicket.QuantityFilled} shares at {_lastOrderTicket.AverageFillPrice} on FillTime: {fillTimeLoc:yyyy-MM-dd HH:mm:ss} local(America/New_York) instead of 0:00:00");
                if (_lastOrderTicket.OrderType == OrderType.MarketOnClose && _isTradeOnMinuteResolution && !((fillTimeLoc.Hour == 13 || fillTimeLoc.Hour == 16) && fillTimeLoc.Minute == 0 && fillTimeLoc.Second == 0))
                    // isOrderTimeAsExpected = false;
                    Debug($"Order has been filled for {orderEvent.Symbol} at wrong time! Details: {_lastOrderTicket.QuantityFilled} shares at {_lastOrderTicket.AverageFillPrice} on FillTime: {fillTimeLoc:yyyy-MM-dd HH:mm:ss} local(America/New_York) instead of 16:00:00");
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

                    _rawCloses.Add(new QcPrice() { QuoteTime = slice.Time, ReferenceDate = slice.Time.Date.AddDays(-1), Close = (decimal)rawClose });
                    _adjCloses.Add(new QcPrice() { QuoteTime = slice.Time, ReferenceDate = slice.Time.Date.AddDays(-1), Close = (decimal)rawClose });
                }

                string lastRaw2 = string.Join(",", _rawCloses.TakeLast(4).Select(r => r.QuoteTime.ToString() + ":" + r.Close.ToString())); // "8/24/2022 12:00:00 AM:889.3600,8/25/2022 12:00:00 AM:891.29,8/26/2022 12:00:00 AM:296.0700"
                string lastAdj2 = string.Join(",", _adjCloses.TakeLast(4).Select(r => r.QuoteTime.ToString() + ":" + r.Close.ToString())); // "8/24/2022 12:00:00 AM:296.45330368800,8/25/2022 12:00:00 AM:297.096636957,8/26/2022 12:00:00 AM:296.0700"

                if (slice.Dividends.ContainsKey(_symbolDaily))
                {
                    var dividend = slice.Dividends[_symbolDaily];
                    _dividends.Add(new QcDividend() { QuoteTime = slice.Time, ReferenceDate = slice.Time.Date.AddDays(-1), Dividend = dividend });
                    decimal divAdjMultiplicator = 1 - dividend.Distribution / dividend.ReferencePrice;
                    for (int i = 0; i < _adjCloses.Count; i++)
                    {
                        _adjCloses[i].Close *= divAdjMultiplicator;
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
                    _splits.Add(new QcSplit() { QuoteTime = slice.Time, ReferenceDate = slice.Time.Date.AddDays(-1), Split = occuredSplit });
                    decimal refPrice = occuredSplit.ReferencePrice;    // Contains RAW price (before Split adjustment). Not used here.
                    decimal splitAdjMultiplicator = occuredSplit.SplitFactor;
                    for (int i = 0; i < _adjCloses.Count; i++)  // Not-chosen option: if we 'have to' use QC bug 'wrongly-adjusted' rawClose, we can skip the last item. In that case we don't apply the split adjustment to the last item, which is the same day as the day of Split.
                    {
                        _adjCloses[i].Close *= splitAdjMultiplicator;
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

        public static long DateTimeUtcToUnixTimeStamp(DateTime p_utcDate) // Int would roll over to a negative in 2038 (if you are using UNIX timestamp), so long is safer
        {
            // Unix timestamp is seconds past epoch
            System.DateTime dtDateTime = new(1970, 1, 1, 0, 0, 0, 0, System.DateTimeKind.Utc);
            TimeSpan span = p_utcDate - dtDateTime;
            return (long)span.TotalSeconds;
        }
    }
}