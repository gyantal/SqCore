using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using SqCommon;
using YahooFinanceApi;

namespace FinTechCommon
{

    [DebuggerDisplay("Ticker = {Ticker}, SecID({SecID})")]
    public class Security
    {
        public uint SecID { get; set; } = 0; // invalid value is best to be 0. If it is Uint32.MaxValue is the invalid, then problems if extending to Uint64
        public String Ticker { get; set; } = String.Empty;

        public String ExpectedHistorySpan { get; set; } = String.Empty;
        public float LastPriceIex { get; set; } = -100.0f;     // real-time last price
        public float LastPriceYF { get; set; } = -100.0f;     // real-time last price
    }

    public partial class MemDb
    {

        public static MemDb gMemDb = new MemDb();

        // RAM requirement: 1Year = 260*(2+4) = 1560B = 1.5KB,  5y data is: 5*260*(2+4) = 7.8K
        // Max RAM requirement if need only AdjClose: 20years for 5K stocks: 5000*20*260*(2+4) = 160MB (only one data per day: DivSplitAdjClose.)
        // Max RAM requirement if need O/H/L/C/AdjClose/Volume: 6x of previous = 960MB = 1GB
        // 2020-01 FinTimeSeries SumMem: 2+10+10+4*5 = 42 years. 42*260*(2+4)= 66KB.                                With 5000 stocks, 30years: 5000*260*30*(2+4)= 235MB
        // 2020-05 CompactFinTimeSeries SumMem: 2+10+10+4*5 = 42 years. Date + data: 2*260*10+42*260*(4)= 48KB.     With 5000 stocks, 30years: 2*260*30+5000*260*30*(4)= 156MB (a saving of 90MB)
        // CompactFinTimeSeries also gives better cache usage, because the shared Date field page is always in the cache, because it is used frequently

        public List<Security> Securities { get; } = new List<Security>() { // to minimize mem footprint, only load the necessary dates (not all history).
            new Security() { SecID = 1, Ticker = "GLD", ExpectedHistorySpan="5y"},                  // history starts on 2004-11-18
            new Security() { SecID = 2, Ticker = "QQQ", ExpectedHistorySpan="Date: 2009-12-31"},    // history starts on 1999-03-10. Full history would be: 32KB.       // 2010-01-01 Friday is NewYear holiday, first trading day is 2010-01-04
            new Security() { SecID = 3, Ticker = "SPY", ExpectedHistorySpan="Date: 2009-12-31"},    // history starts on 1993-01-29. Full history would be: 44KB, 
            new Security() { SecID = 4, Ticker = "TLT", ExpectedHistorySpan="5y"},                  // history starts on 2002-07-30
            new Security() { SecID = 6, Ticker = "UNG", ExpectedHistorySpan="5y"},                  // history starts on 2007-04-18
            new Security() { SecID = 7, Ticker = "USO", ExpectedHistorySpan="5y"},                  // history starts on 2006-04-10
             new Security() { SecID = 5, Ticker = "VXX", ExpectedHistorySpan="Date: 2018-01-25"}};  // history starts on 2018-01-25 on YF, because VXX was restarted. The previously existed VXX.B shares are not on YF.

        // alphabetical order for faster search is not realistic without Index tables. MemDb should mirror persistent data in RedisDb. For Trades in Portfolios. The SecID in MemDb should be the same SecID as in Redis.
        // There are ticker renames every other day, and we will not reorganize the whole Securities table in Redis just because there were ticker renames in real life.
        // In Redis, SecID will be permanent. Starting from 1...increasing by 1. Redis 'tables' will be ordered by SecID, because of faster JOIN operations. And SecID will be permanent, so no reorganizing is needed.
        // The top bits of SecID is the SecType, so there can be gaps in the SecID ordered list. But at least, we can aim to order this array by SecID. (as in Redis)
        // TODO: if we want to have fast BinarySearch access by ticker, we need a Table of Indexes based on Ticker's alphabetical order. An Index table. And the GetFirstMatchingSecurity(string p_ticker) should use it. 
        int[] m_idxByTicker = new int[7];    // TODO: implement and use Index Table based on Ticker for faster BinarySearch

        public CompactFinTimeSeries<DateOnly, uint, float, uint> DailyHist = new CompactFinTimeSeries<DateOnly, uint, float, uint>();

        public bool IsInitialized { get; set; } = false;

        public delegate void MemDbEventHandler();
        public event MemDbEventHandler? EvInitialized = null;
        


        Timer m_historicalDataReloadTimer;
        DateTime m_lastHistoricalDataReload = DateTime.MinValue; // UTC
        public event MemDbEventHandler? EvHistoricalDataReloaded = null;

        

        public MemDb()
        {
            m_historicalDataReloadTimer = new System.Threading.Timer(new TimerCallback(ReloadHistoricalDataTimer_Elapsed), this, TimeSpan.FromMilliseconds(-1.0), TimeSpan.FromMilliseconds(-1.0));
        }

        public void Init()
        {
            ThreadPool.QueueUserWorkItem(Init_WT);
        }

        void Init_WT(object? p_state)    // WT : WorkThread
        {
            Thread.CurrentThread.Name = "MemDb.Init_WT Thread";

            HistoricalDataReloadAndSetTimer();

            IsInitialized = true;
            EvInitialized?.Invoke();
        }

        public void ServerDiagnostic(StringBuilder p_sb)
        {
            int memUsedKb = DailyHist.GetDataDirect().MemUsed() / 1024;
            p_sb.Append("<H2>MemDb</H2>");
            p_sb.Append($"Historical: #Securities: {Securities.Count}. Used RAM: {memUsedKb:N0}KB<br>");
            ServerDiagnosticRealtime(p_sb);
        }

        public Security GetSecurity(uint p_secID)
        {
            // TODO: <after dates are compacted> MemDb. If fast access is important order Securities by SecID as well, and then use BinarySearch
            foreach (var sec in Securities)
            {
                if (sec.SecID == p_secID)
                    return sec;
            }
            throw new Exception($"SecID '{p_secID}' is missing from MemDb.Securities.");
        }

        public Security GetFirstMatchingSecurity(string p_ticker)
        {
            // although Tickers are not unique (only SecID), most of the time clients access data by Ticker.

            // TODO: <after dates are compacted> MemDb. implement and use Index Table based on Ticker for faster BinarySearch. int[] m_idxByTicker. 
            // Both GetFirstMatchingSecurity(string p_ticker) and GetSecurity(uint p_secID) should use BinarySearch.
            foreach (var sec in Securities)
            {
                if (sec.Ticker == p_ticker)
                    return sec;
            }
            throw new Exception($"Ticker '{p_ticker}' is missing from MemDb.Securities.");
        }

        public Security[] GetAllMatchingSecurities(string p_ticker)
        {
            throw new NotImplementedException();
        }

        public static void ReloadHistoricalDataTimer_Elapsed(object state)    // Timer is coming on a ThreadPool thread
        {
            ((MemDb)state).HistoricalDataReloadAndSetTimer();
        }

        // https://github.com/lppkarl/YahooFinanceApi
        void HistoricalDataReloadAndSetTimer()
        {
            Utils.Logger.Info("ReloadHistoricalDataAndSetTimer() START");
            try
            {
                // The startTime & endTime here defaults to EST timezone
                var secDates = new Dictionary<uint, DateOnly[]>();
                var secAdjustedClose = new Dictionary<uint, float[]>();

                // YF sends this weird Texts, which are converted to Decimals, so we don't lose TEXT conversion info.
                // AAPL:    DateTime: 2016-01-04 00:00:00, Open: 102.610001, High: 105.370003, Low: 102.000000, Close: 105.349998, Volume: 67649400, AdjustedClose: 98.213585  (original)
                //          DateTime: 2016-01-04 00:00:00, Open: 102.61, High: 105.37, Low: 102, Close: 105.35, Volume: 67649400, AdjustedClose: 98.2136
                // we have to round values of 102.610001 to 2 decimals (in normal stocks), but some stocks price is 0.00452, then that should be left without conversion.
                // AdjustedClose 98.213585 is exactly how YF sends it, which is correct. Just in YF HTML UI, it is converted to 98.21. However, for calculations, we may need better precision.
                // In general, round these price data Decimals to 4 decimal precision.
                foreach (var sec in Securities)
                {
                    DateTime startDateET = new DateTime(2018, 02, 01, 0, 0, 0);
                    if (sec.ExpectedHistorySpan.StartsWith("Date: ")) {
                        if (!DateTime.TryParseExact(sec.ExpectedHistorySpan.Substring("Date: ".Length), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out startDateET))
                            throw new Exception($"ReloadHistoricalDataAndSetTimer(): wrong ExpectedHistorySpan for ticker {sec.Ticker}");
                    } else if (sec.ExpectedHistorySpan.EndsWith("y")) {
                        if (!Int32.TryParse(sec.ExpectedHistorySpan.Substring(0, sec.ExpectedHistorySpan.Length - 1), out int nYears))
                            throw new Exception($"ReloadHistoricalDataAndSetTimer(): wrong ExpectedHistorySpan for ticker {sec.Ticker}");
                        startDateET = DateTime.UtcNow.FromUtcToEt().AddYears(-1*nYears);
                    }

                    var history = Yahoo.GetHistoricalAsync(sec.Ticker, startDateET, DateTime.Now, Period.Daily).Result; // if asked 2010-01-01 (Friday), the first data returned is 2010-01-04, which is next Monday. So, ask YF 1 day before the intended
                    // for penny stocks, IB and YF considers them for max. 4 digits. UWT price (both in IB ask-bid, YF history) 2020-03-19: 0.3160, 2020-03-23: 2302
                    // sec.AdjCloseHistory = history.Select(r => (double)Math.Round(r.AdjustedClose, 4)).ToList();
                    var dates = history.Select(r => new DateOnly(r!.DateTime)).ToArray();
                    secDates[sec.SecID] = dates;
                    var adjCloses = history.Select(r => RowExtension.IsEmptyRow(r!) ? float.NaN : (float)Math.Round(r!.AdjustedClose, 4)).ToArray();
                    secAdjustedClose[sec.SecID] = adjCloses;

                    Debug.WriteLine($"{sec.Ticker}, first: DateTime: {dates.First()}, Close: {adjCloses.First()}, last: DateTime: {dates.Last()}, Close: {adjCloses.Last()}");  // only writes to Console in Debug mode in vscode 'Debug Console'
                }

                // Merge all dates into a big date array
                // We don't know which days are holidays, so we have to walk all the dates simultaneously, and put into merged date array all the existing dates.
                // walk backward, because the first item will be the latest, the yesterday.
                var idx = new Dictionary<uint, int>();
                DateOnly minDate = DateOnly.MaxValue, maxDate = DateOnly.MinValue;
                foreach (var dates in secDates)  // assume first date is the oldest, last day is yesterday
                {
                    if (minDate > dates.Value.First())
                        minDate = dates.Value.First();
                    if (maxDate < dates.Value.Last())
                        maxDate = dates.Value.Last();
                    idx.Add(dates.Key, dates.Value.Length - 1);
                }

                List<DateOnly> mergedDates = new List<DateOnly>();
                DateOnly currDate = maxDate;
                while (true) 
                {
                    mergedDates.Add(currDate);

                    DateOnly nextDate = DateOnly.MaxValue;
                    foreach (var dates in secDates)
                    {
                        if (idx[dates.Key] >= 0 && dates.Value[idx[dates.Key]] == currDate)
                        {
                            idx[dates.Key] = idx[dates.Key] - 1;    // decrease index
                        }

                        if (idx[dates.Key] >= 0 && dates.Value[idx[dates.Key]] < nextDate)
                            nextDate = dates.Value[idx[dates.Key]];
                    }
                    if (nextDate == DateOnly.MaxValue)  // it was not updated, so all idx reached 0
                        break;
                    currDate = nextDate;
                }
                Debug.WriteLine($"first: DateTime: {mergedDates.First()}, last: DateTime: {mergedDates.Last()}");

                var mergedDatesArr = mergedDates.ToArray();

                var values = new Dictionary<uint, Tuple<Dictionary<TickType, float[]>, Dictionary<TickType, uint[]>>>();
                foreach (var closes in secAdjustedClose)
                {
                    var secID = closes.Key;
                    var secDatesArr = secDates[secID];
                    var iSecDates = secDatesArr.Length - 1; // set index to last item

                    var closesArr = new List<float>(mergedDates.Count); // allocate more, even though it is not necessarily filled
                    for (int i = 0; i < mergedDates.Count; i++)
                    {
                        if (secDatesArr[iSecDates] == mergedDatesArr[i])
                        {
                            closesArr.Add(closes.Value[iSecDates]);
                            if (iSecDates <= 0) // if first (oldest) item is already used, thatis the last item. 
                                break;
                            iSecDates--;
                        }
                        else
                        { 
                            closesArr[i] = float.NaN;   // in a very rare cases prices might be missing from the front, or from the middle (if stock didn't trade on that day)
                        }
                    }   // for mergedDates

                    var dict1 = new Dictionary<TickType, float[]>() { { TickType.SplitDivAdjClose, closesArr.ToArray() } };
                    var dict2 = new Dictionary<TickType, uint[]>(); // we don't store volume now, but maybe in the future
                    values.Add(secID, new Tuple<Dictionary<TickType, float[]>, Dictionary<TickType, uint[]>>(dict1, dict2));
                }
                DailyHist.ChangeData(mergedDatesArr, values);
            }
            catch (Exception e)
            {
                Utils.Logger.Error(e, "ReloadHistoricalDataAndSetTimer()");
            }

            m_lastHistoricalDataReload = DateTime.UtcNow;
            EvHistoricalDataReloaded?.Invoke();

            // reload times should be relative to ET (Eastern Time), because that is how USA stock exchanges work.
            // Reload History approx in UTC: at 9:00 (when IB resets its own timers and pre-market starts)(in the 3 weeks when summer-winter DST difference, it is 8:00), at 14:00 (30min before market open, last time to get correct data, because sometimes YF fixes data late), 21:30 (30min after close)
            // In ET time zone, these are:  4:00ET, 9:00ET, 16:30ET. IB starts premarket trading at 4:00. YF starts to have premarket data from 4:00ET.
            DateTime etNow = Utils.ConvertTimeFromUtcToEt(DateTime.UtcNow);
            int nowTimeOnlySec = etNow.Hour * 60 * 60 + etNow.Minute * 60 + etNow.Second;
            int targetTimeOnlySec;
            if (nowTimeOnlySec < 4 * 60 * 60)
                targetTimeOnlySec = 4 * 60 * 60;
            else if (nowTimeOnlySec < 9 * 60 * 60)  // Market opens 9:30ET, but reload data 30min before
                targetTimeOnlySec = 9 * 60 * 60;
            else if (nowTimeOnlySec < 16 * 60 * 60 + 30 * 60)   // Market closes at 16:00ET, but reload data 30min after
                targetTimeOnlySec = 16 * 60 * 60 + 30 * 60;
            else
                targetTimeOnlySec = 24 * 60 * 60 + 4 * 60 * 60; // next day 4:00

            DateTime targetDateEt = etNow.Date.AddSeconds(targetTimeOnlySec);
            Utils.Logger.Info($"m_reloadHistoricalDataTimer set next targetdate: {targetDateEt.ToSqDateTimeStr()} ET");
            m_historicalDataReloadTimer.Change(targetDateEt - etNow, TimeSpan.FromMilliseconds(-1.0));     // runs only once.
        }

        public void Exit()
        {
        }

    }

}