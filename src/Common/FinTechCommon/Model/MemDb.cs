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
using System.IO.Compression;
using Microsoft.Extensions.Primitives;

namespace FinTechCommon
{
    public partial class MemDb
    {

        public static MemDb gMemDb = new MemDb();
        IDatabase m_redisDb;

        public User[] Users = new User[0];

        string m_lastUsersStr = String.Empty;
        string m_lastAllAssetsStr = String.Empty;
        string m_lastSqCoreWebAssetsStr = String.Empty;
        public AssetsCache AssetsCache = new AssetsCache();

        // RAM requirement: 1Year = 260*(2+4) = 1560B = 1.5KB,  5y data is: 5*260*(2+4) = 7.8K
        // Max RAM requirement if need only AdjClose: 20years for 5K stocks: 5000*20*260*(2+4) = 160MB (only one data per day: DivSplitAdjClose.)
        // Max RAM requirement if need O/H/L/C/AdjClose/Volume: 6x of previous = 960MB = 1GB
        // 2020-01 FinTimeSeries SumMem: 2+10+10+4*5 = 42 years. 42*260*(2+4)= 66KB.                                With 5000 stocks, 30years: 5000*260*30*(2+4)= 235MB
        // 2020-05 CompactFinTimeSeries SumMem: 2+10+10+4*5 = 42 years. Date + data: 2*260*10+42*260*(4)= 48KB.     With 5000 stocks, 30years: 2*260*30+5000*260*30*(4)= 156MB (a saving of 90MB)
        // { // to minimize mem footprint, only load the necessary dates (not all history). Because we would like to have 2008-09 market crash in history, start from 2005-01
        //     new Asset() { AssetId = new AssetId32Bits(AssetType.Stock, 1), PrimaryExchange = ExchangeId.NYSE, LastTicker = "SPY", ExpectedHistorySpan="Date: 2004-12-31"},    // history starts on 1993-01-29. Full history would be: 44KB, 
        //     new Asset() { AssetId = new AssetId32Bits(AssetType.Stock, 2), PrimaryExchange = ExchangeId.NYSE, LastTicker = "QQQ", ExpectedHistorySpan="Date: 2004-12-31"},    // history starts on 1999-03-10. Full history would be: 32KB.       // 2010-01-01 Friday is NewYear holiday, first trading day is 2010-01-04
        //     new Asset() { AssetId = new AssetId32Bits(AssetType.Stock, 3), PrimaryExchange = ExchangeId.NYSE, LastTicker = "TLT", ExpectedHistorySpan="15y"},                 // history starts on 2002-07-30
        //     new Asset() { AssetId = new AssetId32Bits(AssetType.Stock, 4), PrimaryExchange = ExchangeId.NYSE, LastTicker = "VXX", ExpectedHistorySpan="Date: 2018-01-25"},    // history starts on 2018-01-25 on YF, because VXX was restarted. The previously existed VXX.B shares are not on YF.
        //     new Asset() { AssetId = new AssetId32Bits(AssetType.Stock, 5), PrimaryExchange = ExchangeId.NYSE, LastTicker = "UNG", ExpectedHistorySpan="15y"},                 // history starts on 2007-04-18
        //     new Asset() { AssetId = new AssetId32Bits(AssetType.Stock, 6), PrimaryExchange = ExchangeId.NYSE, LastTicker = "USO", ExpectedHistorySpan="15y"},                 // history starts on 2006-04-10
        //     new Asset() { AssetId = new AssetId32Bits(AssetType.Stock, 7), PrimaryExchange = ExchangeId.NYSE, LastTicker = "GLD", ExpectedHistorySpan="15y"}                  // history starts on 2004-11-18
        //     };
        public CompactFinTimeSeries<DateOnly, uint, float, uint> DailyHist = new CompactFinTimeSeries<DateOnly, uint, float, uint>();

        public bool IsInitialized { get; set; } = false;

        Timer m_assetDataReloadTimer;
        Timer m_historicalDataReloadTimer;
        DateTime m_lastHistoricalDataReload = DateTime.MinValue; // UTC
        DateTime m_lastAssetsDataReload = DateTime.MinValue; // UTC

        public delegate void MemDbEventHandler();
        public event MemDbEventHandler? EvFirstInitialized = null;     // it can be ReInitialized in every 1 hour because of RedisDb polling
        public event MemDbEventHandler? EvAssetDataReloaded = null;
        public event MemDbEventHandler? EvHistoricalDataReloaded = null;


#pragma warning disable CS8618 // Non-nullable field 'm_redisDb' is uninitialized.
        public MemDb()  // constructor runs in main thread
        {
        }
#pragma warning restore CS8618

        public void Init(IDatabase p_redisDb)
        {
            m_redisDb = p_redisDb;
            ThreadPool.QueueUserWorkItem(Init_WT);
        }

        void Init_WT(object? p_state)    // WT : WorkThread, input p_state = null
        {
            // Better to do long consuming data preprocess in working thread than in the constructor in the main thread
            Thread.CurrentThread.Name = "MemDb.Init_WT Thread";
            m_assetDataReloadTimer = new System.Threading.Timer(new TimerCallback(ReloadAssetDataTimer_Elapsed), this, TimeSpan.FromMilliseconds(-1.0), TimeSpan.FromMilliseconds(-1.0));
            m_historicalDataReloadTimer = new System.Threading.Timer(new TimerCallback(ReloadHistoricalDataTimer_Elapsed), this, TimeSpan.FromMilliseconds(-1.0), TimeSpan.FromMilliseconds(-1.0));
            InitRt_WT();

            ReloadAssetsDataIfChangedAndSetTimer();  // Polling for changes every 1 hour. Downloads the AllAssets, SqCoreWeb-used-Assets from Redis Db, and 
            // if necessary it reloads Historical and Realtime data
            // ReloadHistoricalDataAndSetTimer() // Polling for changes 3x every day
            // ReloadRealtimeDataAndSetTimer() // Polling for changes in every 3sec, every 1min or every 60min

            IsInitialized = true;
            EvFirstInitialized?.Invoke();    // inform observers that MemDb was reloaded

            // User updates only the JSON text version of data (assets, OptionPrices in either Redis or in SqlDb). But we use the Redis's Brotli version for faster DB access.
            Thread.Sleep(TimeSpan.FromSeconds(20));     // can start it in a separate thread, but it is fine to use this background thread
            UpdateRedisBrotlisService.SetTimer(new UpdateBrotliParam() { RedisDb = m_redisDb});
            UpdateNavsService.SetTimer(new UpdateNavsParam() { RedisDb = m_redisDb});
        }

        public void ServerDiagnostic(StringBuilder p_sb)
        {
            int memUsedKb = DailyHist.GetDataDirect().MemUsed() / 1024;
            p_sb.Append("<H2>MemDb</H2>");
            p_sb.Append($"Historical: #SqCoreWebAssets+virtualNavs: {AssetsCache.Assets.Count}. ({String.Join(',', AssetsCache.Assets.Select(r => r.LastTicker))}). Used RAM: {memUsedKb:N0}KB<br>");
            ServerDiagnosticRealtime(p_sb);
        }

        public void ReloadAssetDataTimer_Elapsed(object state)    // Timer is coming on a ThreadPool thread
        {
            ((MemDb)state).ReloadAssetsDataIfChangedAndSetTimer();
        }

        void ReloadAssetsDataIfChangedAndSetTimer()
        {
            Utils.Logger.Info("ReloadAssetsDataIfChangedAndSetTimer() START");
            try
            {
                // GA.IM.NAV assets have user_id data, so User data has to be reloaded too.
                string sqUserDataStr = m_redisDb.StringGet("sq_user");
                bool isUsersChangedInDb = m_lastUsersStr != sqUserDataStr;
                if (isUsersChangedInDb)
                {
                    m_lastUsersStr = sqUserDataStr;
                }

                // start using Redis:'allAssets.brotli' (520bytes instead of 1.52KB) immediately. See UpdateRedisBrotlisService();
                byte[] allAssetsBin = m_redisDb.HashGet("memDb", "allAssets.brotli");
                var allAssetsBinToStr = Utils.BrotliBin2Str(allAssetsBin);
                bool isAllAssetsChangedInDb = m_lastAllAssetsStr != allAssetsBinToStr;
                if (isAllAssetsChangedInDb)
                {
                    m_lastAllAssetsStr = allAssetsBinToStr;
                }

                string sqCoreWebAssetsStr = m_redisDb.HashGet("memDb", "SqCoreWebAssets");
                bool isSqCoreWebAssetsChanged = m_lastSqCoreWebAssetsStr != sqCoreWebAssetsStr;
                if (isSqCoreWebAssetsChanged)
                {
                    m_lastSqCoreWebAssetsStr = sqCoreWebAssetsStr;
                }

                bool isReloadNeeded = isUsersChangedInDb || isAllAssetsChangedInDb || isSqCoreWebAssetsChanged;

                if (isReloadNeeded)
                {
                    var usersInDb = JsonSerializer.Deserialize<List<UserInDb>>(sqUserDataStr);
                    Users = usersInDb.Select(r =>
                        {
                            return new User()
                            {
                                Id = r.id,
                                Username = r.username,
                                Password = r.password,
                                Title = r.title,
                                Firstname = r.firstname,
                                Lastname = r.lastname,
                                Email = r.email
                            };
                        }).ToArray();

                    var sqCoreWebAssets = JsonSerializer.Deserialize<Dictionary<string, SqCoreWebAssetInDb>>(m_lastSqCoreWebAssetsStr);
                    var allAssets = JsonSerializer.Deserialize<Dictionary<string, AssetInDb[]>>(m_lastAllAssetsStr);

                    // select only a subset of the allAssets in DB that SqCore webapp needs
                    List<Asset> sqAssets = sqCoreWebAssets.Select(r =>
                    {
                        var assetId = new AssetId32Bits(r.Key);
                        var assetTypeArr = allAssets[((byte)assetId.AssetTypeID).ToString()];
                        // Linq is slow. List<T>.Find() is faster than Linq.FirstOrDefault() https://stackoverflow.com/questions/14032709/performance-of-find-vs-firstordefault
                        var assetFromDb = Array.Find(assetTypeArr, k => k.ID == assetId.SubTableID);

                        User? user = null;
                        if (assetId.AssetTypeID == AssetType.BrokerNAV)
                        {
                            user = Users.FirstOrDefault(k => k.Id == Int32.Parse(assetFromDb.user_id));
                        }
                        return new Asset()
                        {
                            AssetId = assetId,
                            PrimaryExchange = ExchangeId.NYSE, // NYSE is is larger than Nasdaq. If it is not specified assume NYSE. Saving DB space. https://www.statista.com/statistics/270126/largest-stock-exchange-operators-by-market-capitalization-of-listed-companies/
                            LastTicker = assetFromDb.Ticker,
                            LastName = assetFromDb.Name,
                            ExpectedHistorySpan = r.Value.LoadPrHist,
                            ExpectedHistoryStartDateET = GetExpectedHistoryStartDate(r.Value.LoadPrHist, assetFromDb.Ticker),
                            User = user
                        };
                    }).ToList();

                    AssetsCache = AssetsCache.CreateAssetCache(sqAssets);
                    m_lastAssetsDataReload = DateTime.UtcNow;

                    ReloadHistoricalDataAndSetTimer();  // downloads historical prices from YF
                    ReloadRealtimeDataAndSetTimer(); // downloads realtime prices from YF

                    EvAssetDataReloaded?.Invoke();
                } // isReloadNeeded
            }
            catch (Exception e)
            {
                Utils.Logger.Error(e, "ReloadAssetsDataIfChangedAndSetTimer()");
            }

            DateTime etNow = Utils.ConvertTimeFromUtcToEt(DateTime.UtcNow);
            DateTime targetDateEt = etNow.AddHours(1);  // Polling for change in every 1 hour
            Utils.Logger.Info($"m_reloadAssetsDataTimer set next targetdate: {targetDateEt.ToSqDateTimeStr()} ET");
            m_assetDataReloadTimer.Change(targetDateEt - etNow, TimeSpan.FromMilliseconds(-1.0));     // runs only once
        }

        public void ReloadHistoricalDataTimer_Elapsed(object state)    // Timer is coming on a ThreadPool thread
        {
            ((MemDb)state).ReloadHistoricalDataAndSetTimer();
        }

        void ReloadHistoricalDataAndSetTimer()
        {
            Utils.Logger.Info("ReloadHistoricalDataAndSetTimer() START");
            try
            {
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
                foreach (var asset in AssetsCache.Assets)
                {
                    DateOnly[] dates = new DateOnly[0];  // to avoid "Possible multiple enumeration of IEnumerable" warning, we have to use Arrays, instead of Enumerable, because we will walk this lists multiple times, as we read it backwards
                    float[] adjCloses = new float[0];

                    if (asset.AssetId.AssetTypeID == AssetType.Stock)
                    {
                        // https://github.com/lppkarl/YahooFinanceApi
                        // YF: all the Open/High/Low/Close are always adjusted for Splits;  In addition: AdjClose also adjusted for Divididends.
                        // YF gives back both the onlySplit(butNotDividend)-adjusted row.Close, and SplitAndDividendAdjusted row.AdjustedClose (checked with MO dividend and USO split).
                        // checked the YF returned data by stream.ReadToEnd(): it is a CSV structure, with columns. The line "Apr 29, 2020	1:8 Stock Split" is Not in the data. 
                        // https://finance.yahoo.com/quote/USO/history?p=USO The YF website queries the splits separately when it inserts in-between the rows.
                        // Therefore, we have to query the splits separately from YF.
                        var history = Yahoo.GetHistoricalAsync(asset.LastTicker, asset.ExpectedHistoryStartDateET, DateTime.Now, Period.Daily).Result; // if asked 2010-01-01 (Friday), the first data returned is 2010-01-04, which is next Monday. So, ask YF 1 day before the intended
                        dates = history.Select(r => new DateOnly(r!.DateTime)).ToArray();
                        // for penny stocks, IB and YF considers them for max. 4 digits. UWT price (both in IB ask-bid, YF history) 2020-03-19: 0.3160, 2020-03-23: 2302
                        // sec.AdjCloseHistory = history.Select(r => (double)Math.Round(r.AdjustedClose, 4)).ToList();
                        adjCloses = history.Select(r => RowExtension.IsEmptyRow(r!) ? float.NaN : (float)Math.Round(r!.AdjustedClose, 4)).ToArray();

                        // 2020-04-29: USO split: Split-adjustment was not done in YF. For these exceptional cases, so we need an additional data-source for double check
                        // First just add the manual data source from Redis/Sql database, and let’s see how unreliable YF is in the future. 
                        // If it fails frequently, then we can implement query-ing another website, like https://www.nasdaq.com/market-activity/stock-splits
                        var splitHistoryYF = Yahoo.GetSplitsAsync(asset.LastTicker, asset.ExpectedHistoryStartDateET, DateTime.Now).Result;

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
                                    adjCloses[j] = (float)((double)adjCloses[j] * multiplier);
                                else
                                    break;  // dates are in increasing order. So, once we passed the critical date, we can safely exit
                            }
                        }
                    }

                    if (adjCloses.Length != 0)
                    {
                        assetsDates[asset.AssetId] = dates;
                        assetsAdjustedCloses[asset.AssetId] = adjCloses;
                        Debug.WriteLine($"{asset.LastTicker}, first: DateTime: {dates.First()}, Close: {adjCloses.First()}, last: DateTime: {dates.Last()}, Close: {adjCloses.Last()}");  // only writes to Console in Debug mode in vscode 'Debug Console'
                    }
                }

                // NAV assets should be grouped by user, because we create a synthetic aggregatedNAV. This aggregate should add up the RAW UnadjustedNAV (not adding up the adjustedNAV), so we have to create it at MemDbReload.
                var navAssetsByUser = AssetsCache.Assets.Where(r => r.AssetId.AssetTypeID == AssetType.BrokerNAV).ToLookup(r => r.User);
                int nVirtualAggNavAssets = 0;
                foreach (var navAssetsOfUser in navAssetsByUser)
                {
                    User user = navAssetsOfUser.Key!;
                    List<DateOnly[]> navsDates = new List<DateOnly[]>();
                    List<double[]> navsUnadjustedCloses = new List<double[]>();
                    List<KeyValuePair<DateOnly, double>[]> navsDeposits = new List<KeyValuePair<DateOnly, double>[]>();
                    foreach (var navAsset in navAssetsOfUser)
                    {
                        string redisKey = navAsset.AssetId.ToString() + ".brotli"; // // key: "9:1.brotli"
                        byte[] dailyNavBrotli = m_redisDb.HashGet("assetQuoteRaw", redisKey);
                        if (dailyNavBrotli == null)
                            continue; // temproraly: only [9:1] is in RedisDb.
                        var dailyNavStr = Utils.BrotliBin2Str(dailyNavBrotli);  // 47K text data from 9.5K brotli data, starts with FormatString: "D/C,20090102/16460,20090105/16826,..."
                        int iFirstComma = dailyNavStr.IndexOf(',');
                        string formatString = dailyNavStr.Substring(0, iFirstComma);  // "D/C" for Date/Closes
                        if (formatString != "D/C")
                            continue;

                        var dailyNavStrSplit = dailyNavStr.Substring(iFirstComma + 1, dailyNavStr.Length - (iFirstComma + 1)).Split(',', StringSplitOptions.RemoveEmptyEntries);
                        DateOnly[] dates = dailyNavStrSplit.Select(r => new DateOnly(Int32.Parse(r.Substring(0, 4)), Int32.Parse(r.Substring(4, 2)), Int32.Parse(r.Substring(6, 2)))).ToArray();
                        double[] unadjustedClosesNav = dailyNavStrSplit.Select(r => Double.Parse(r.Substring(9))).ToArray();
                        
                        byte[] dailyDepositBrotli = m_redisDb.HashGet("assetBrokerNavDeposit", redisKey);
                        var dailyDepositStr = Utils.BrotliBin2Str(dailyDepositBrotli);  // 479 byte text data from 179 byte brotli data, starts with FormatString: "20090310/1903,20100305/2043,..."
                        KeyValuePair<DateOnly, double>[] deposits = dailyDepositStr.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(r => {
                            // format: "20200323/-1000000"
                            var depositsDays = r.Split('/', StringSplitOptions.RemoveEmptyEntries);
                            DateTime date = Utils.FastParseYYYYMMDD(new StringSegment(r, 0, 8));
                            double deposit = Double.Parse(new StringSegment(r, 9, r.Length - 9));
                            return new KeyValuePair<DateOnly, double>(new DateOnly(date), deposit);
                        }).ToArray();


                        CreateAdjNavAndIntegrate(navAsset, dates, unadjustedClosesNav, deposits, assetsDates, assetsAdjustedCloses);
                        navsDates.Add(dates);
                        navsUnadjustedCloses.Add(unadjustedClosesNav);
                        navsDeposits.Add(deposits);
                    }   // All NAVs of the user
                    if (navsDates.Count >= 2)   // if more than 2 NAVs for the user, a virtual synthetic aggregatedNAV and a virtual AssetID should be generated.
                    {
                        string  aggAssetTicker = user.Initials + ".NAV"; // e.g. "DC.NAV";
                        var aggNavAsset = AssetsCache.GetFirstMatchingAssetByLastTicker(aggAssetTicker, ExchangeId.Unknown, false); // at sucessive ReloadHistoricalData(), the AssetsCache already contains the aggregated virtual asset
                        if (aggNavAsset == null)
                        {
                            var aggAssetId = new AssetId32Bits(AssetType.BrokerNAV, (uint)(10000 + nVirtualAggNavAssets++));
                            aggNavAsset = new Asset()
                            {
                                AssetId = aggAssetId,
                                PrimaryExchange = ExchangeId.NYSE,
                                LastTicker = aggAssetTicker,
                                LastName = "Aggregated NAV, " + user.Initials,
                                ExpectedHistorySpan = "1y",
                                ExpectedHistoryStartDateET = GetExpectedHistoryStartDate("1y", aggAssetTicker),
                                User = user
                            };
                            AssetsCache.AddAsset(aggNavAsset);
                        }

                        // merging Lists. Union and LINQ has very bad performance. Use Dict. https://stackoverflow.com/questions/4031262/how-to-merge-2-listt-and-removing-duplicate-values-from-it-in-c-sharp
                        var aggDatesDict = new Dictionary<DateOnly, double>();
                        for (int iNav = 0; iNav < navsDates.Count; iNav++)
                        {
                            for (int j = 0; j < navsDates[iNav].Length; j++)
                            {
                                if (!aggDatesDict.ContainsKey(navsDates[iNav][j]))
                                    aggDatesDict[navsDates[iNav][j]] = navsUnadjustedCloses[iNav][j];
                                else
                                    aggDatesDict[navsDates[iNav][j]] += navsUnadjustedCloses[iNav][j];
                            }
                        }
                        var aggDatesSortedDict = aggDatesDict.OrderBy(p => p.Key); // if you need to sort it once, don't use SortedDictionary() https://www.c-sharpcorner.com/article/performance-sorteddictionary-vs-dictionary/
                        var aggDates = aggDatesSortedDict.Select(r => r.Key).ToArray();    // it is ordered from earliest (2011) to latest (2020) exactly how the dates come from RedisDb
                        var aggUnadjustedCloses = aggDatesSortedDict.Select(r => r.Value).ToArray();

                        var aggDepositsDict = new Dictionary<DateOnly, double>();
                        for (int iNav = 0; iNav < navsDates.Count; iNav++)
                        {
                            foreach (var deposit in navsDeposits[iNav])
                            {
                                if (!aggDepositsDict.ContainsKey(deposit.Key))
                                    aggDepositsDict[deposit.Key] = deposit.Value;
                                else
                                    aggDepositsDict[deposit.Key] += deposit.Value;
                            }
                        }
                        var aggDepositsSortedDict = aggDepositsDict.OrderBy(p => p.Key); // if you need to sort it once, don't use SortedDictionary() https://www.c-sharpcorner.com/article/performance-sorteddictionary-vs-dictionary/
                        CreateAdjNavAndIntegrate(aggNavAsset, aggDates, aggUnadjustedCloses, aggDepositsSortedDict.ToArray(), assetsDates, assetsAdjustedCloses);
                    }   // if there is 2 or more NAVs for the user.
                } // NAVs per user

                // Merge all dates into a big date array
                // We don't know which days are holidays, so we have to walk all the dates simultaneously, and put into merged date array all the existing dates.
                // walk backward, because the first item will be the latest, the yesterday.
                var idx = new Dictionary<uint, int>();  // AssetId => to index of the walking where we are
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
                DateOnly currDate = maxDate; // this maxDate is today, or yesterday. We walk backwards.
                while (true) 
                {
                    mergedDates.Add(currDate);

                    DateOnly largestPrevDate = DateOnly.MinValue;
                    foreach (var dates in assetsDates)
                    {
                        if (idx[dates.Key] >= 0 && dates.Value[idx[dates.Key]] == currDate)
                        {
                            idx[dates.Key]--;    // decrease index
                        }

                        if (idx[dates.Key] >= 0 && dates.Value[idx[dates.Key]] > largestPrevDate)
                            largestPrevDate = dates.Value[idx[dates.Key]];
                    }
                    if (largestPrevDate == DateOnly.MinValue)  // it was not updated, so all idx reached 0
                        break;
                    currDate = largestPrevDate;
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
                            // There is no USA stock price data on specific USA stock market holidays (e.g. 2020-09-07: Labour day). 
                            // However, brokerNAV values, Forex, commodity futures, London-based stocks can have data on that day. 
                            // We most certainly have to contain that date in the global MemDb.Data. 
                            // We have a choice, however: leave the USA-stock value Float.NaN or flow the last-day price into this non-existent date. 
                            // We choose the Float.NaN, because otherwise users of MemDb would have no chance to know that this was a day with a non-existent price.
                            closesArr.Add(float.NaN);   // in a very rare cases prices might be missing from the front, or from the middle (if stock didn't trade on that day)
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
                Utils.Logger.Error(e, "Exception in ReloadHistoricalDataAndSetTimer()");
                HealthMonitorMessage.SendAsync($"Exception in SqCoreWebsite.C#.MemDb. Exception: '{ e.ToStringWithShortenedStackTrace(1200)}'", HealthMonitorMessageID.SqCoreWebCsError).TurnAsyncToSyncTask();
            }

            m_lastHistoricalDataReload = DateTime.UtcNow;
            EvHistoricalDataReloaded?.Invoke();

            // reload times should be relative to ET (Eastern Time), because that is how USA stock exchanges work.
            // Reload History approx in UTC: at 9:00 (when IB resets its own timers and pre-market starts)(in the 3 weeks when summer-winter DST difference, it is 8:00), at 14:00 (30min before market open, last time to get correct data, because sometimes YF fixes data late), 21:30 (30min after close)
            // In ET time zone, these are: 4:00ET, 9:00ET, 16:30ET. IB starts premarket trading at 4:00. YF starts to have premarket data from 4:00ET.
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

        private void CreateAdjNavAndIntegrate(Asset navAsset, DateOnly[] dates, double[] unadjustedClosesNav, KeyValuePair<DateOnly, double>[] deposits, Dictionary<uint, DateOnly[]> assetsDates, Dictionary<uint, float[]> assetsAdjustedCloses)
        {
            // ************************ Stock dividend ajustment and NAV deposit adjustment
            // https://www.investopedia.com/ask/answers/06/adjustedclosingprice.asp
            // "The adjusted closing price is often used when examining historical returns "
            // For example, let's assume that the closing price for one share of XYZ Corp. is $20 on Thursday. After the close on Thursday, XYZ Corp. announces a dividend distribution of $1.50 per share. The adjusted closing price for the stock would then be $18.50 ($20-$1.50). So, we take away the dividend from the All Previous prices.
            // > this is correct, but this will not work iteratively, the subtraction should be converted to an equivalent multiplication.
            // So, multiplier = (originalPrice-dividend)/originalPrice, which is (20-1.50)/20
            // we use this multiplier on original price 20, that gives 20 * multiplier = 20-1.5=18.5 fine.
            // And this multiplier can be used iteratively.

            // >Another example: imagine there was a 50% increase in the previous day, then a 10% dividend.
            // 100, 100, 200, 200, 180+20, 180, 180
            // We calculate daily%returns: 0%, 0%, 100%, 0%, 0%
            // If we just simply take away -20 from all prevous prices, 80, 80 (?), 180, 180, 180, so daily return: 0% 0%, 125% (which is incorrect), 0%, 0%
            // To do it correctly, we have to reformulate that daily %return stays the same as before. The ? is not 80, it is something different.
            // So, multiplier = (originalPrice-dividend)/originalPrice = 200-20/200=90%.
            // Then we don't subtract -20 from each previous day's values, but we multiply each previous day's values by 90%, to obtain:
            // 90, 90, 180, 180, 180  which gives the daily returns of 0%, 100%, 0%. Correct.

            // >For adjusting NAV values by deposit/withdrawal, the same can be done.
            // If there was a withdrawal (negative value), we consider it as dividend.
            // So, when original NAV in $M was 9, 9, 10, 9+1 withdrawal, 9, 11 then the %return is not lastNAV/firstNAV = 11/9, which is 22%
            // But multiplier = (10-1)/10 = 90%, and using that
            // AdjustedNAV is 8.1, 8.1, 9, 9, 11, therefore %return = 11/8.1= 35.8%
            // That gives that every daily %return is the same as before.
            // Same can be said for positive Deposits. If we add deposit, NAV is increased, but we don't want to see that as a performance measure %return.

            // >It is like doing a Time-Weighted-Return per day. Basically, we calculate the synthetic daily%returns that every day gives back the daily %return. Then we aggregate these in a way that the final price is the current NAV.

            double multiplier = 1.0;    // cummulative multiplier
            float[] adjCloses = new float[dates.Length];
            int iDeposits = deposits.Length - 1;
            for (int i = dates.Length - 1; i >= 0; i--)    // go from yesterday backwards, because adjustment is a multiplier.
            {
                DateOnly date = dates[i];
                adjCloses[i] = (float)(unadjustedClosesNav![i] * multiplier);
                if (iDeposits >= 0 && date == deposits[iDeposits].Key)
                {
                    // >Deposit: Date/Value
                    // 20200323/-1000000  // assume it was taken in the middle of the day. The end of the day NAV doesn't contain that.
                    // >NAV: Date/Value
                    // 20200319/10270249
                    // 20200320/10522203
                    // 20200323/9630437	// decreased, don't change that day. But add the withdrawal to it, as if that would have been the correct price.
                    // 20200324/9590033
                    // >Then OriginalNAV_on_20200323 = (9630437 + 1000000)
                    // multiplier = (OriginalNAV-dividend)/OriginalNAV = 9630437 / (9630437 + 1000000) =  0.90593048997 = 90.5%.
                    // Warning. Don't multiply the 20200323 date NAV with this (because that is already the 9.6M reduced value, not the 10.5M big value), but multiply all prevous days.
                    // At the end multiplier becomes: 1.16; because Deposits are more.
                    // >The result of this deposit-adjustment:
                    // Unadjusted: from 9.8M to 11.7M, a 18% return in 2020. -15% maxDD
                    // Adjusted: from 8.9M to 11.7M, a 31% return in 2020. -9.8% maxDD
                    double sumDeposit = 0.0;    // aggregate deposits on that day.
                    while (iDeposits >= 0 && date == deposits[iDeposits].Key) // it can be that for the same date, there are many deposits + withdrawal records. DeBlan: 04/30/2015: -200K, +450K on the same day
                    {
                        sumDeposit += deposits[iDeposits].Value;
                        iDeposits--;
                    }
                    double unadjustedActualNav = unadjustedClosesNav![i] - sumDeposit; // if it is a negative withdrawal, it will add it
                    double mult = (unadjustedActualNav + sumDeposit) / unadjustedActualNav;
                    multiplier *= mult;
                }
            }

            // For debugging purposes. To copy to a CSV file.
            // StringBuilder sb = new StringBuilder();
            // for (int i = 0; i < adjCloses.Length; i++)
            // {
            //     sb.Append(dates[i] + ", " + adjCloses[i] + Environment.NewLine);
            // }
            // string adjClosesStr = sb.ToString();

            assetsDates[navAsset.AssetId] = dates;
            assetsAdjustedCloses[navAsset.AssetId] = adjCloses;
            Debug.WriteLine($"{navAsset.LastTicker}, first: DateTime: {dates.First()}, Close: {adjCloses.First()}, last: DateTime: {dates.Last()}, Close: {adjCloses.Last()}");  // only writes to Console in Debug mode in vscode 'Debug Console'
        }

        DateTime GetExpectedHistoryStartDate(string p_expectedHistorySpan, string p_ticker)
        {
            DateTime startDateET = new DateTime(2018, 02, 01, 0, 0, 0);
            if (p_expectedHistorySpan.StartsWith("Date:"))
            {
                if (!DateTime.TryParseExact(p_expectedHistorySpan.Substring("Date:".Length), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out startDateET))
                    throw new Exception($"ReloadHistoricalDataAndSetTimer(): wrong ExpectedHistorySpan for ticker {p_ticker}");
            }
            else if (p_expectedHistorySpan.EndsWith("y"))
            {
                if (!Int32.TryParse(p_expectedHistorySpan.Substring(0, p_expectedHistorySpan.Length - 1), out int nYears))
                    throw new Exception($"ReloadHistoricalDataAndSetTimer(): wrong ExpectedHistorySpan for ticker {p_ticker}");
                startDateET = DateTime.UtcNow.FromUtcToEt().AddYears(-1 * nYears).Date;
            }
            else if (p_expectedHistorySpan.EndsWith("m")) // RenewedUber requires only the last 2-3 days. Last 1year is unnecessary, so do only last 2 months
            {
                if (!Int32.TryParse(p_expectedHistorySpan.Substring(0, p_expectedHistorySpan.Length - 1), out int nMonths))
                    throw new Exception($"ReloadHistoricalDataAndSetTimer(): wrong ExpectedHistorySpan for ticker {p_ticker}");
                startDateET = DateTime.UtcNow.FromUtcToEt().AddMonths(-1 * nMonths).Date;
            }

            if (!p_expectedHistorySpan.StartsWith("Date:")) // if "Date:" was given, we assume admin was specific for a reason. Then don't go back 1 day earlier. Otherwise (months, years), go back 1 day earlier for safety.
            {
                // Keep this method in MemDb, cos we might use MemDb.Holiday data in the future.
                // if startDateET is weekend, we have to go back to previous Friday
                if (startDateET.DayOfWeek == DayOfWeek.Sunday)
                    startDateET = startDateET.AddDays(-2);
                if (startDateET.DayOfWeek == DayOfWeek.Saturday)
                    startDateET = startDateET.AddDays(-1);
                startDateET = startDateET.AddDays(-1);  // go back another extra day, in case that Friday was a stock market holiday
            }

            return startDateET;
        }

        public void Exit()
        {
        }

    }

}