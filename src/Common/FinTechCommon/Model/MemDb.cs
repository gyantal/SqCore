using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using SqCommon;
using StackExchange.Redis;
using YahooFinanceApi;
using System.Text.Json;

namespace FinTechCommon
{

    public partial class MemDb
    {

        public static MemDb gMemDb = new MemDb();
        IDatabase m_redisDb;

        // RAM requirement: 1Year = 260*(2+4) = 1560B = 1.5KB,  5y data is: 5*260*(2+4) = 7.8K
        // Max RAM requirement if need only AdjClose: 20years for 5K stocks: 5000*20*260*(2+4) = 160MB (only one data per day: DivSplitAdjClose.)
        // Max RAM requirement if need O/H/L/C/AdjClose/Volume: 6x of previous = 960MB = 1GB
        // 2020-01 FinTimeSeries SumMem: 2+10+10+4*5 = 42 years. 42*260*(2+4)= 66KB.                                With 5000 stocks, 30years: 5000*260*30*(2+4)= 235MB
        // 2020-05 CompactFinTimeSeries SumMem: 2+10+10+4*5 = 42 years. Date + data: 2*260*10+42*260*(4)= 48KB.     With 5000 stocks, 30years: 2*260*30+5000*260*30*(4)= 156MB (a saving of 90MB)
        public List<Asset> Assets { get; } = new List<Asset>() { // to minimize mem footprint, only load the necessary dates (not all history).
            new Asset() { AssetId = new AssetId32Bits(AssetType.Stock, 1), PrimaryExchange = ExchangeId.NYSE, LastTicker = "SPY", ExpectedHistorySpan="Date: 2009-12-31"},    // history starts on 1993-01-29. Full history would be: 44KB, 
            new Asset() { AssetId = new AssetId32Bits(AssetType.Stock, 2), PrimaryExchange = ExchangeId.NYSE, LastTicker = "QQQ", ExpectedHistorySpan="Date: 2009-12-31"},    // history starts on 1999-03-10. Full history would be: 32KB.       // 2010-01-01 Friday is NewYear holiday, first trading day is 2010-01-04
            new Asset() { AssetId = new AssetId32Bits(AssetType.Stock, 3), PrimaryExchange = ExchangeId.NYSE, LastTicker = "TLT", ExpectedHistorySpan="10y"},                 // history starts on 2002-07-30
            new Asset() { AssetId = new AssetId32Bits(AssetType.Stock, 4), PrimaryExchange = ExchangeId.NYSE, LastTicker = "VXX", ExpectedHistorySpan="Date: 2018-01-25"},    // history starts on 2018-01-25 on YF, because VXX was restarted. The previously existed VXX.B shares are not on YF.
            new Asset() { AssetId = new AssetId32Bits(AssetType.Stock, 5), PrimaryExchange = ExchangeId.NYSE, LastTicker = "UNG", ExpectedHistorySpan="10y"},                 // history starts on 2007-04-18
            new Asset() { AssetId = new AssetId32Bits(AssetType.Stock, 6), PrimaryExchange = ExchangeId.NYSE, LastTicker = "USO", ExpectedHistorySpan="10y"},                 // history starts on 2006-04-10
            new Asset() { AssetId = new AssetId32Bits(AssetType.Stock, 7), PrimaryExchange = ExchangeId.NYSE, LastTicker = "GLD", ExpectedHistorySpan="10y"}
            };                  // history starts on 2004-11-18
        // MemDb should mirror persistent data in RedisDb. For Trades in Portfolios. The AssetId in MemDb should be the same AssetId as in Redis.
        // Alphabetical order of tickers for faster search is not realistic without Index tables or Hashtable/Dictionary.
        // There are ticker renames every other day, and we will not reorganize the whole Assets table in Redis just because there were ticker renames in real life.
        // In Redis, AssetId will be permanent. Starting from 1...increasing by 1. Redis 'tables' will be ordered by AssetId, because of faster JOIN operations.
        // The top bits of AssetId is the Type=Stock, Options, so there can be gaps in the AssetId ordered list. But at least, we can aim to order this array by AssetId. (as in Redis)
        
        // O(1) search for Dictionaries. Tradeoff between CPU-usage and RAM-usage. Use excess memory in order to search as fast as possible.
        // Another alternative is to implement a virtual ordering in an index table: int[] m_idxByTicker (used by Sql DBs), but that also uses RAM and the access would be only O(logN). Hashtable uses more memory, but it is faster.
        // BinarySearch is a good idea for 10,000 Dates in time series, but not for this, when we have a small number of discrete values of AssetID or Tickers
        Dictionary<AssetId32Bits, Asset> AssetsByAssetID = new Dictionary<AssetId32Bits, Asset>();  // Assets are ordered by AssetId, so BinarySearch could be used, but Hashtable is faster.

        // Dictionary<Key, List<Value>> vs. a Lookup<Key, Value> ; The main difference is a Lookup is immutable: it has no Add() methods and no public constructor
        // If you are trying to get a structure as efficient as a Dictionary but you dont know for sure there is no duplicate key in input, Lookup is safer.
        // It also supports null keys, and returns always a valid result, so it appears as more resilient to unknown input (less prone than Dictionary to raise exceptions).
        // https://stackoverflow.com/questions/13362490/difference-between-lookup-and-dictionaryof-list
        ILookup<string, Asset> AssetsByLastTicker = Enumerable.Empty<Asset>().ToLookup(x => default(string)!); // LSE:"VOD", NYSE:"VOD" both can be in database  // Lookup doesn't have default constructor.
        
        public CompactFinTimeSeries<DateOnly, uint, float, uint> DailyHist = new CompactFinTimeSeries<DateOnly, uint, float, uint>();

        public bool IsInitialized { get; set; } = false;

        public delegate void MemDbEventHandler();
        public event MemDbEventHandler? EvInitialized = null;
        


        Timer m_historicalDataReloadTimer;
        DateTime m_lastHistoricalDataReload = DateTime.MinValue; // UTC
        public event MemDbEventHandler? EvHistoricalDataReloaded = null;


#pragma warning disable CS8618 // Non-nullable field 'm_redisDb' is uninitialized.
        public MemDb()  // constructor runs in main thread
        {
            m_historicalDataReloadTimer = new System.Threading.Timer(new TimerCallback(ReloadHistoricalDataTimer_Elapsed), this, TimeSpan.FromMilliseconds(-1.0), TimeSpan.FromMilliseconds(-1.0));
        }
#pragma warning restore CS8618

        public void Init(IDatabase p_redisDb)
        {
            m_redisDb = p_redisDb;
            ThreadPool.QueueUserWorkItem(Init_WT);
        }

        void Init_WT(object? p_state)    // WT : WorkThread
        {
            Thread.CurrentThread.Name = "MemDb.Init_WT Thread";

            // Better to do long consuming data preprocess in working thread than in the constructor in the main thread
            AssetsByLastTicker = Assets.ToLookup(r => r.LastTicker); // if it contains duplicates, ToLookup() allows for multiple values per key.
            AssetsByAssetID = Assets.ToDictionary(r => r.AssetId);

            HistoricalDataReloadAndSetTimer();
            InitRt_WT(p_state); // call this after HistoricalDataReload downloads the Assets from DB

            IsInitialized = true;
            EvInitialized?.Invoke();
        }

        public void ServerDiagnostic(StringBuilder p_sb)
        {
            int memUsedKb = DailyHist.GetDataDirect().MemUsed() / 1024;
            p_sb.Append("<H2>MemDb</H2>");
            p_sb.Append($"Historical: #Assets: {Assets.Count}. Used RAM: {memUsedKb:N0}KB<br>");
            ServerDiagnosticRealtime(p_sb);
        }

        public Asset GetAsset(uint p_assetID)
        {
            if (AssetsByAssetID.TryGetValue(p_assetID, out Asset value))
                return value;
            throw new Exception($"AssetID '{p_assetID}' is missing from MemDb.Assets.");
        }


        // Although Tickers are not unique (only AssetId), most of the time clients access data by LastTicker. 
        // It is not good for historical backtests, because it uses only the last ticker, not historical tickers, but it is enough 95% of the time for clients.
        // This can find both "VOD" (Vodafone) ticker in LSE (in GBP), NYSE (in USD).
        public Asset GetFirstMatchingAssetByLastTicker(string p_lasTicker, ExchangeId p_primExchangeID = ExchangeId.Unknown)
        {
            IEnumerable<Asset> assets = AssetsByLastTicker[p_lasTicker];
            if (assets == null)
                throw new Exception($"Ticker '{p_lasTicker}' is missing from MemDb.Assets.");

            foreach (var asset in assets)
            {
                if (p_primExchangeID == ExchangeId.Unknown || p_primExchangeID == asset.PrimaryExchange)
                    return asset;
            }
            throw new Exception($"Ticker '{p_lasTicker}' is in MemDb.Assets, but Exchange '{p_primExchangeID}' is not found.");
        }

        // Also can be historical using Assets.TickerChanges
        public Asset[] GetAllMatchingAssets(string p_ticker, ExchangeId p_primExchangeID =  ExchangeId.Unknown, DateTime? p_timeUtc = null)
        {
            return Assets.GetAllMatchingAssets(p_ticker, p_primExchangeID, p_timeUtc).ToArray();
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
                // TODO: this can be in a separate thread as RedisReload(out assetsMissingYfSplits) function
                string missingYfSplitsJson = m_redisDb.HashGet("memDb", "missingYfSplits");
                var assetsMissingYfSplits = JsonSerializer.Deserialize<Dictionary<string, Split[]>>(missingYfSplitsJson);   // JsonSerializer: Dictionary key <int>,<uint> is not supported

              
                // The startTime & endTime here defaults to EST timezone
                var assetsDates = new Dictionary<uint, DateOnly[]>();
                var assetsAdjustedCloses = new Dictionary<uint, float[]>();
                // YF sends this weird Texts, which are converted to Decimals, so we don't lose TEXT conversion info.
                // AAPL:    DateTime: 2016-01-04 00:00:00, Open: 102.610001, High: 105.370003, Low: 102.000000, Close: 105.349998, Volume: 67649400, AdjustedClose: 98.213585  (original)
                //          DateTime: 2016-01-04 00:00:00, Open: 102.61, High: 105.37, Low: 102, Close: 105.35, Volume: 67649400, AdjustedClose: 98.2136
                // we have to round values of 102.610001 to 2 decimals (in normal stocks), but some stocks price is 0.00452, then that should be left without conversion.
                // AdjustedClose 98.213585 is exactly how YF sends it, which is correct. Just in YF HTML UI, it is converted to 98.21. However, for calculations, we may need better precision.
                // In general, round these price data Decimals to 4 decimal precision.
                foreach (var asset in Assets)
                {
                    DateTime startDateET = new DateTime(2018, 02, 01, 0, 0, 0);
                    if (asset.ExpectedHistorySpan.StartsWith("Date: ")) {
                        if (!DateTime.TryParseExact(asset.ExpectedHistorySpan.Substring("Date: ".Length), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out startDateET))
                            throw new Exception($"ReloadHistoricalDataAndSetTimer(): wrong ExpectedHistorySpan for ticker {asset.LastTicker}");
                    } else if (asset.ExpectedHistorySpan.EndsWith("y")) {
                        if (!Int32.TryParse(asset.ExpectedHistorySpan.Substring(0, asset.ExpectedHistorySpan.Length - 1), out int nYears))
                            throw new Exception($"ReloadHistoricalDataAndSetTimer(): wrong ExpectedHistorySpan for ticker {asset.LastTicker}");
                        startDateET = DateTime.UtcNow.FromUtcToEt().AddYears(-1*nYears).Date;
                    }

                    // YF: all the Open/High/Low/Close are always adjusted for Splits;  In addition: AdjClose also adjusted for Divididends.
                    // YF gives back both the onlySplit(butNotDividend)-adjusted row.Close, and SplitAndDividendAdjusted row.AdjustedClose (checked with MO dividend and USO split).
                    // checked the YF returned data by stream.ReadToEnd(): it is a CSV structure, with columns. The line "Apr 29, 2020	1:8 Stock Split" is Not in the data. 
                    // https://finance.yahoo.com/quote/USO/history?p=USO The YF website queries the splits separately when it inserts in-between the raws.
                    // Therefore, we have to query the splits separately from YF.
                    var history = Yahoo.GetHistoricalAsync(asset.LastTicker, startDateET, DateTime.Now, Period.Daily).Result; // if asked 2010-01-01 (Friday), the first data returned is 2010-01-04, which is next Monday. So, ask YF 1 day before the intended
                    var dates = history.Select(r => new DateOnly(r!.DateTime)).ToArray();
                    assetsDates[asset.AssetId] = dates;
                    // for penny stocks, IB and YF considers them for max. 4 digits. UWT price (both in IB ask-bid, YF history) 2020-03-19: 0.3160, 2020-03-23: 2302
                    // sec.AdjCloseHistory = history.Select(r => (double)Math.Round(r.AdjustedClose, 4)).ToList();
                    var splitDivAdjCloses = history.Select(r => RowExtension.IsEmptyRow(r!) ? float.NaN : (float)Math.Round(r!.AdjustedClose, 4)).ToArray();
                    
                    // 2020-04-29: USO split: Split-adjustment was not done in YF. For these exceptional cases, so we need an additional data-source for double check
                    // First just add the manual data source from Redis/Sql database, and let’s see how unreliable YF is in the future. 
                    // If it fails frequently, then we can implement query-ing another website, like https://www.nasdaq.com/market-activity/stock-splits
                    var splitHistoryYF = Yahoo.GetSplitsAsync(asset.LastTicker, startDateET, DateTime.Now).Result;

                    // var missingYfSplitDb = new SplitTick[1] { new SplitTick() { DateTime = new DateTime(2020, 06, 08), BeforeSplit = 8, AfterSplit = 1 }};
                    assetsMissingYfSplits.TryGetValue(((byte)asset.AssetId.AssetTypeID).ToString() + ":" + asset.AssetId.SubTableID.ToString(), out var missingYfSplitDb);

                    // if any missingYfSplitDb record is not found in splitHistoryYF, we assume YF is wrong (and our DB is right (probably custom made)), so we do this extra split-adjustment assuming it is not in the YF quote history
                    for (int i = 0; missingYfSplitDb != null && i < missingYfSplitDb.Length; i++)
                    {
                        var missingSplitDb = missingYfSplitDb[i];
                        if (splitHistoryYF.FirstOrDefault(r => r!.DateTime == missingSplitDb.Date) != null)    // if that date exists in YF already, do nothing; assume YF uses it
                            continue;
                        
                        double multiplier = (double)missingSplitDb.Before / (double)missingSplitDb.After;
                        // USO split date from YF: "Apr 29, 2020" Time: 00:00. Means that very early morning, 9:30 hours before market open. That is the inflection point.
                        // Split adjust (multiply) everything before that time, but do NOT split adjust that exact date.
                        DateOnly missingSplitDbDate = new DateOnly(missingSplitDb.Date);
                        for (int j = 0; j < dates.Length; j++)
                        {
                            if (dates[j] < missingSplitDbDate)
                                splitDivAdjCloses[j] = (float)((double)splitDivAdjCloses[j] * multiplier);
                            else
                                break;  // dates are in increasing order. So, once we passed the critical date, we can safely exit
                        }
                    }
                    
                    assetsAdjustedCloses[asset.AssetId] = splitDivAdjCloses;
                    Debug.WriteLine($"{asset.LastTicker}, first: DateTime: {dates.First()}, Close: {splitDivAdjCloses.First()}, last: DateTime: {dates.Last()}, Close: {splitDivAdjCloses.Last()}");  // only writes to Console in Debug mode in vscode 'Debug Console'
                }

                // Merge all dates into a big date array
                // We don't know which days are holidays, so we have to walk all the dates simultaneously, and put into merged date array all the existing dates.
                // walk backward, because the first item will be the latest, the yesterday.
                var idx = new Dictionary<uint, int>();
                DateOnly minDate = DateOnly.MaxValue, maxDate = DateOnly.MinValue;
                foreach (var dates in assetsDates)  // assume first date is the oldest, last day is yesterday
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
                    foreach (var dates in assetsDates)
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
                foreach (var closes in assetsAdjustedCloses)
                {
                    var assetId = closes.Key;
                    var secDatesArr = assetsDates[assetId];
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
                    values.Add(assetId, new Tuple<Dictionary<TickType, float[]>, Dictionary<TickType, uint[]>>(dict1, dict2));
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