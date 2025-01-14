#region imports
using System;
using System.Collections.Generic;
using System.Linq;
using QuantConnect.Parameters;
using QuantConnect.Data;
using QuantConnect.Data.Fundamental;
using QuantConnect.Data.Market;
using QuantConnect.Data.UniverseSelection;
using QuantConnect.Orders;
using QuantConnect.Securities;
using System.Text;
#endregion
namespace QuantConnect.Algorithm.CSharp
{
    // This class is not polished. Only for illustration purposes. To show the ONLY possible way to use historical Fundamental data (e.g. CompanyReference.ShortName) in the pre 2024 QC code base.
    // See our gDoc document 'QuantConnect framework knowledge base', FundamentalData chapter: https://docs.google.com/document/d/1W8CefIRZ-MCwsn_RI2Xb0KdLgGKzjsJKikJ1yuWrnjY/edit#heading=h.83e1r2dxcjlt
    public class SqFundamentalDataFilteredUniv : QCAlgorithm
    {
        bool _isTradeInSqCore = true; // 2 simulation environments. We backtest in Qc cloud or in SqCore frameworks. QcCloud works on per minute resolution (to be able to send MOC orders 20min before MOC), SqCore works on daily resolution only.
        public bool IsTradeInSqCore { get { return _isTradeInSqCore; } }
        public bool IsTradeInQcCloud { get { return !_isTradeInSqCore; } }

        DateTime _startDate = DateTime.MinValue;
        DateTime _endDate = DateTime.MaxValue;
        TimeSpan _warmUp = TimeSpan.Zero;
        string _ticker = "META"; // historical fundamentals.CompanyReference.ShortName: from 20120518: "Facebook", from 20220610: "Meta Platforms, Inc."
        Symbol _symbol;
        List<Symbol> _universe = new List<Symbol>();

        OrderTicket _lastOrderTicket = null;

        List<QcPrice> _rawCloses = new List<QcPrice>(); // comes from QC.OnData(). We use this in SqCore.
        List<QcDividend> _dividends = new List<QcDividend>();
        List<QcPrice> _adjCloses = new List<QcPrice>();

        // keep both List and Dictionary for YF raw price data. List is used for building and adjustment processing. Also, it can be used later if sequential, timely data marching is needed
        List<QcPrice> _rawClosesFromYfList = new List<QcPrice>(); // comes from DownloadAndProcessYfData(). We use this in QcCloud.
        Dictionary<DateTime, decimal> _rawClosesFromYfDict = new Dictionary<DateTime, decimal>(); // Dictionary<DateTime> is about 6x faster to query than List.BinarySearch(). If we know the Key, the DateTime exactly. Which we do know at Rebalancing time.

        DateTime _backtestStartTime;
        StringBuilder _tempLogs = new();
        StringBuilder _priceLogs = new();
        string _tempLastCompanyNameLogged = string.Empty;   // temp variable to prevent Console.Write() on every day. Because that is slow for 10 years daily.

        public override void Initialize()
        {
            _backtestStartTime = DateTime.UtcNow;

            SetStartDate(2013, 03, 25);
            // SetEndDate(2024, 02, 07);

            SetCash(100000);

            _symbol = QuantConnect.Symbol.Create(_ticker, SecurityType.Equity, Market.USA); // If we create it in the CoarseSelectionFunction, then for a 10 year backtest this will be created 2600x
            _universe = new List<Symbol>
                {
                   _symbol
                };
            UniverseSettings.Resolution = Resolution.Daily; // override the default Per Minute.
            AddUniverse(CoarseSelectionFunction, FineSelectionFunction);

        //     Resolution resolutionUsed = IsTradeInSqCore ? Resolution.Daily : Resolution.Minute;
        //     // if Raw is not specified in AddEquity(), all prices come as Adjusted, and then Dividend is not added to Cash, and nShares are not changed at Split
        //     _symbol = AddEquity(_ticker, resolutionUsed, dataNormalizationMode: DataNormalizationMode.Raw).Symbol;   // backtest completed in 4.24 seconds. Processing total of 20,402 data points.
        }

        public IEnumerable<Symbol> CoarseSelectionFunction(IEnumerable<CoarseFundamental> p_coarse) // return a list of fixed Symbol objects
        {
            // These 2 SelectionFunction callback functions are called for every day.
            // The input p_coarse would give us a List of Symbols found in the data folder: Fin\Data\equity\usa\fundamental\coarse\. E.g. file "20130322.csv"
            // As we don't keep daily Coarse fundamental data in SqCore for every day (or at all. To save disk space as it is not needed for our backtests), p_coarse input is an empty list. That is not a problem per se.
            // The problem is that a System.IO.DirectoryNotFoundException exception is raised for every day by the QC framework. For a 10 year backtest that is 3,000 exceptions. That takes 20 seconds.
            // This code is illustration purposes only. If we want to really use Fundamental data based universe selection, we will have to eliminate those Exceptions in the QC framework.
            // To convince QC that if that daily Coarse file is not found, it should continue without exceptions.
             return _universe; // return our fixed universe, instead of filtering based on coarse fundamental data (e.g. closePrice or $Volume)
        }

        public IEnumerable<Symbol> FineSelectionFunction(IEnumerable<FineFundamental> p_fine) // e.g. application of FineSelection: sort the data by market capitalization and take the top
        {
            return p_fine.Select(x => x.Symbol); // select all
        }

        private void TradeLogic() // Buy&Hold From Monday to Friday. This is called at 15:40 in QC cloud, and 00:00 in SqCore
        {
            if (IsWarmingUp) // Dont' trade in the warming up period.
                return;

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
            }

            if (simulatedTimeLoc.DayOfWeek == DayOfWeek.Friday && Portfolio[_symbol].Quantity > 0) // Sell it on Friday if we have a position
                _lastOrderTicket = MarketOnCloseOrder(_symbol, -Portfolio[_symbol].Quantity);  // Daily Raw: Sell: FillDate: 16:00 today (why???) (on previous day close price!!)

            // Log($"PV on day {simulatedTimeLoc.Date.ToString()}: ${Portfolio.TotalPortfolioValue}.");
        }

        public override void OnData(Slice slice) // "slice.Time(Local): 8/23/2022 12:00:00 AM" means early morning on that day, == "slice.Time: 8/23/2022 12:00:01 AM". This gives the day-before data. slice.UtcTime also exists.
        {
            try
            {
                if (IsTradeInQcCloud)
                    return; // in QcCloud, we don't process the incoming data. We try to use peekAhead YF data or perMinute data from symbol.Price

                var fundamentals = Securities[_symbol].Fundamentals;
                if (fundamentals != null)
                {
                    if (fundamentals.CompanyReference.ShortName != _tempLastCompanyNameLogged)
                    {
                        Console.WriteLine($"**** {slice.Time.Date}: Company name: {fundamentals.CompanyReference.ShortName}");
                        _tempLogs.AppendLine($"**** {slice.Time.Date}: Company name: {fundamentals.CompanyReference.ShortName}");
                    }
                    _tempLastCompanyNameLogged = fundamentals.CompanyReference.ShortName;
                }

                // Collect Raw prices (and Splits and Dividends) for calculating Adjusted prices
                // Split comes twice. Once: Split.Warning comes 1 day earlier with slice.Time: 8/24/2022 12:00:00, SplitType.SplitOccurred comes on the proper day
                Split occuredSplit = (slice.Splits.ContainsKey(_symbol) && slice.Splits[_symbol].Type == SplitType.SplitOccurred) ? slice.Splits[_symbol] : null; // split.Type can be Warning and SplitOccured. Ignore 1-day early Split Warnings. Just use the occured

                decimal? rawClose = null;
                if (slice.Bars.TryGetValue(_symbol, out TradeBar bar))
                    rawClose = bar.Close; // Probably bug in QC: if there is a Split for the daily bar, then QC SplitAdjust that bar, even though we asked RAW data. QC only does it for the Split day. We can undo it, because the Split.ReferencePrice has the RAW price.

                _priceLogs.AppendLine($"{slice.Time.Date}: {rawClose ?? 0m}");

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

                if (slice.Dividends.ContainsKey(_symbol))
                {
                    var dividend = slice.Dividends[_symbol];
                    if (SqBacktestConfig.SqDailyTradingAtMOC) // SqDailyTradingAtMOC sends price at 16:00, which is right. No need the change. Without it, price comes 00:00 next morning, so we adjust it back.
                        _dividends.Add(new QcDividend() { ReferenceDate = slice.Time.Date, Dividend = dividend });
                    else
                        _dividends.Add(new QcDividend() { ReferenceDate = slice.Time.Date.AddDays(-1), Dividend = dividend });

                    decimal divAdjMultiplicator = 1 - dividend.Distribution / dividend.ReferencePrice;
                    for (int i = 0; i < _adjCloses.Count; i++)
                        _adjCloses[i].Close *= divAdjMultiplicator;
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