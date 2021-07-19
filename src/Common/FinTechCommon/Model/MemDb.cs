using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using SqCommon;
using System.Threading.Tasks;
using System.Globalization;
using BrokerCommon;

namespace FinTechCommon
{
    // the pure data MemData class has 2 purposes:
    // 1. collect the Data part of MemDb into one smaller entity. Without the MemDb functionality like timers, or Sql/Redis update.
    // 2. at UpdateAll data, the changing of all pure data is just a pointer assignment.
    // While Writer is swapping users/assets/HistData pointers client can get NewUserData and OldAssetData.
    // Without reader locking even careful clients - who don't store pointers - can get inconsistent pointers.
    // It can be solved with a global gMemDbUpdateLock, but then clients should use that, and they will forget it.
    // This way it is still possible for a client to get m_memData.OldUserData and 1ms later m_memData.NewAssetData (if write happened between them), but 
    //   - it is less likely, because pointer swap happened much quicker
    //   - if reader clients ask GetAssuredConsistentTables() that doesn't require waiting in a Lock, but very fast, just a pointer local copy. Clients should use that.
    // Still better to not expose MemData to the outside world, because clients could store its pointers, and GC will not collect old data
    // Clients can still store Assets pointers, but it is better if there are 5 old Assets pointers at clients consuming RAM, then if there are 5 old big MemData at clients holding a lot of memory.
    internal class MemData  // don't expose to clients.
    {
        public volatile User[] Users = new User[0];   // writable: admin might insert a new user from HTML UI
        public volatile AssetsCache AssetsCache = new AssetsCache();  // writable: user might insert a new asset from HTML UI

        // As Portfolios are assets (nesting), we might store portfolios in AssetCache, not separately
        public volatile List<string> Portfolios = new List<string>(); // temporary illustration of a data that will be not only read, but written by SqCore
        public volatile CompactFinTimeSeries<DateOnly, uint, float, uint> DailyHist = new CompactFinTimeSeries<DateOnly, uint, float, uint>();

        public MemData()
        {
        }

        public MemData(User[] newUsers, AssetsCache newAssetCache, CompactFinTimeSeries<DateOnly, uint, float, uint> newDailyHist)
        {
            Users = newUsers;
            AssetsCache = newAssetCache;
            DailyHist = newDailyHist;
        }
    }

    class MemDataWlocks // locks should not be replaced whem m_memData pointer is replaced, because Wait() was called before the pointer swap, Release() is called after the swap.
    {
        internal readonly SemaphoreSlim m_usersWlock = new SemaphoreSlim(1, 1);
        internal readonly SemaphoreSlim m_assetsWlock = new SemaphoreSlim(1, 1);
        internal readonly SemaphoreSlim m_portfoliosWlock = new SemaphoreSlim(1, 1);
        internal readonly SemaphoreSlim m_dailyHistWlock = new SemaphoreSlim(1, 1);
    }

    // Multithreading of shared resource. Implement Lockfree-Read, Copy-Modify-Swap-Write Pattern described https://stackoverflow.com/questions/10556806/make-a-linked-list-thread-safe/10557130#10557130
    // lockObj is necessary, volatile is necessary (because of readers to not have old copies hiding in their CpuCache)
    // the lock is a WriterLock only. No need for ReaderLock. For high concurrency.
    // Readers of the Users, Assets, DailyHist, Portfolios should keep the pointer locally as the pointer may get swapped out any time

    // lock(object) is banned in async function (because lock is intended for very short time.) 
    // The official way is SemaphoreSlim (although slower, but writing happens very rarely)
    // if same thread calls SemaphoreSlim.Wait() second time, it will block. It uses a counter mechanism, and it doesn't store which thread acquired the lock already. So, reentry is not possible.
    
    // AggregatedNav data:
    // AggregatedNav history: DailyHist stores the AggregatedNav merged history properly. Although it increases RAM usage, it has to be done only once, at MemDb reload. Better to store it.
    // AggregatedNav realtime: AssetsCache has the realtime prices for subNavs. But it is not merged automatically, so AggregatedNav.LastValue is not up-to-date. 
    // RT price has to be calculated from subNavs all the time. Two reasons: 
    // 1. RT NAV price can arrive every 5 seconds. We don't want to do CPU intensive searches all the time to find the parentNav, then find all children, then aggregate RT prices
    // 2. Aggregating RT is actually not easy. As when a new RT-Sub1 price arrives, if we update the AggregatedNav RT, that might be false. Because what if 1 sec later the RT-Sub2 price arrives.
    // That also has to do the searches and aggregate all the children again. And all of these calculations maybe totally pointless if nobody watches the AggregatedNav. 
    // So, better to aggregate RT prices only rarely when it is required by a user.
    public partial class MemDb
    {

        public static MemDb gMemDb = new MemDb();   // Singleton pattern
        // public object gMemDbUpdateLock = new object();  // the rare clients who care about inter-table consintency (VBroker) should obtain the lock before getting pointers to subtables
        Db m_Db;

        MemData m_memData = new MemData();  // strictly private. Don't allow clients to store separate MemData pointers. Clients should use GetAssuredConsistentTables() in general.
        MemDataWlocks m_memDataWlocks = new MemDataWlocks(); // locks should not be replaced whem m_memData pointer is replaced, because Wait() was called before the pointer swap, Release() is called after the swap.
 
        public User[] Users { get { return m_memData.Users; } }
        public AssetsCache AssetsCache { get { return m_memData.AssetsCache; } }
        public List<string> Portfolios { get { return m_memData.Portfolios; } }
        public CompactFinTimeSeries<DateOnly, uint, float, uint> DailyHist { get { return m_memData.DailyHist; } }

        public bool IsInitialized { get; set; } = false;

        Timer m_dbReloadTimer;  // checks every 1 hour, but reloads RAM only if Db data change is detected.
        DateTime m_lastDbReload = DateTime.MinValue; // UTC
        TimeSpan m_lastDbReloadTs;  // RedisDb reload: 2-3 tables: 0.2sec, It is about 100ms per table.
        Timer m_historicalDataReloadTimer; // forced reload In ET time zone: 4:00ET, 9:00ET, 16:30ET. 
        DateTime m_lastHistoricalDataReload = DateTime.MinValue; // UTC
        TimeSpan m_lastHistoricalDataReloadTs;  // YF downloads. For 12 stocks, it is 3sec. so for 120 stocks 30sec, for 600 stocks 2.5min, for 1200 stocks 5min
 
        public List<BrAccount> BrAccounts{ get; set; } = new List<BrAccount>();   // only Broker dependent data. When AssetCache is reloaded from RedisDb, this should not be wiped or reloaded

        public delegate void MemDbEventHandler();
        public event MemDbEventHandler? EvFirstInitialized = null;     // it can be ReInitialized in every 1 hour because of Database polling
        public event MemDbEventHandler? EvDbDataReloaded = null;
        public event MemDbEventHandler? EvHistoricalDataReloaded = null;


#pragma warning disable CS8618 // Non-nullable field 'm_assetDataReloadTimer' is uninitialized.
        public MemDb()  // constructor runs in main thread
        {
        }
#pragma warning restore CS8618

        public void Init(Db p_db)
        {
            m_Db = p_db;
            Utils.RunInNewThread(() => Init_WT());
        }

        async Task Init_WT()    // WT : WorkThread
        {
            // Better to do long consuming data preprocess in working thread than in the constructor in the main thread
            Thread.CurrentThread.Name = "MemDb.Init_WT Thread";
            m_dbReloadTimer = new System.Threading.Timer(new TimerCallback(ReloadDbDataTimer_Elapsed), this, TimeSpan.FromMilliseconds(-1.0), TimeSpan.FromMilliseconds(-1.0));
            m_historicalDataReloadTimer = new System.Threading.Timer(new TimerCallback(ReloadHistoricalDataTimer_Elapsed), this, TimeSpan.FromMilliseconds(-1.0), TimeSpan.FromMilliseconds(-1.0));
            InitRt_WT();
            InitNavRt_WT();

            await ReloadDbDataIfChangedAndSetNewTimer();  // Polling for changes every 1 hour. Downloads the AllAssets, SqCoreWeb-used-Assets from Redis Db, and even HistData 

            IsInitialized = true;
            EvFirstInitialized?.Invoke();    // inform observers that MemDb was reloaded

            // User updates only the JSON text version of data (assets, OptionPrices in either Redis or in SqlDb). But we use the Redis's Brotli version for faster DB access.
            // Thread.Sleep(TimeSpan.FromSeconds(20));     // can start it in a separate thread, but it is fine to use this background thread
            UpdateRedisBrotlisService.SetTimer(new UpdateBrotliParam() { Db = m_Db });
            UpdateNavsService.SetTimer(new UpdateNavsParam() { Db = m_Db });

            await ReloadHistDataAndSetNewTimer();   // initial ReloadDbData doesn't fill up HistData. We say MemDb is initialized and we fill up HistData later.
        }

        public void ServerDiagnostic(StringBuilder p_sb)
        {
            p_sb.Append("<H2>MemDb</H2>");
            ServerDiagnosticMemDb(p_sb, true);
            p_sb.Append($"<br><br>");

            ServerDiagnosticRealtime(p_sb);
            p_sb.Append($"<br>");
            ServerDiagnosticNavRealtime(p_sb);

            p_sb.Append($"<br>");
            ServerDiagnosticBrAccount(p_sb, true);
        }

        private void ServerDiagnosticMemDb(StringBuilder p_sb, bool p_isHtml)
        {
            p_sb.Append($"#Users: {Users.Length}: <br>");

            var hist = DailyHist.GetDataDirect();
            int memUsedKb = hist.MemUsed() / 1024;
            p_sb.Append($"#Assets: {AssetsCache.Assets.Count}, #HistoricalAssets: {hist.Data.Count}, Used RAM: {memUsedKb:N0}KB<br>");  // hist.Data.Count = Srv.LoadPrHist + DC Aggregated NAV 
            var lastDbReloadWithoutHist = m_lastDbReloadTs - m_lastHistoricalDataReloadTs;
            p_sb.Append($"m_lastHistoricalDataReloadTimeUtc: '{m_lastHistoricalDataReload}', lastDbReloadWithoutHist: {lastDbReloadWithoutHist.TotalSeconds:0.000}sec, m_lastHistoricalDataReloadTs: {m_lastHistoricalDataReloadTs.TotalSeconds:0.000}sec.<br>");

            var yfTickers = AssetsCache.Assets.Where(r => r.AssetId.AssetTypeID == AssetType.Stock).Select(r => ((Stock)r).YfTicker).ToArray();
            p_sb.Append($"StockAssets (#{yfTickers.Length}): ");
            p_sb.AppendLongListByLine(yfTickers, ",", 10, "<br>");
        }

        private void ServerDiagnosticBrAccount(StringBuilder p_sb, bool p_isHtml)
        {
            foreach (var brAccount in BrAccounts)
            {
                p_sb.Append($"BrAccount GatewayId: {brAccount.GatewayId}, LastUpdateUtc: {brAccount.LastUpdate.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)}, NAV: {brAccount.NetLiquidation}, #Pos: {brAccount.AccPoss.Count}<br>");
            }
        }

        public async void ReloadDbDataTimer_Elapsed(object? p_state)    // Timer is coming on a ThreadPool thread
        {
            if (p_state == null)
                throw new Exception("ReloadDbDataTimer_Elapsed() received null object.");
            await ((MemDb)p_state).ReloadDbDataIfChangedAndSetNewTimer();
        }

        public async Task<StringBuilder> ReloadDbDataIfChanged(bool p_isHtml)  // print log to Console or HTML
        {
            StringBuilder sb = new StringBuilder();
            await ReloadDbDataIfChangedAndSetNewTimer();
            ServerDiagnosticMemDb(sb, p_isHtml);
            return sb;
        }

        async Task ReloadDbDataIfChangedAndSetNewTimer()
        {
            Utils.Logger.Info("ReloadDbDataIfChangedAndSetTimer() START");
            m_memDataWlocks.m_usersWlock.Wait();
            try
            {
                m_memDataWlocks.m_assetsWlock.Wait();
                try
                {
                    m_memDataWlocks.m_portfoliosWlock.Wait();
                    try
                    {
                        m_memDataWlocks.m_dailyHistWlock.Wait();
                        try
                        {
                            try
                            {
                                await ReloadDbDataIfChangedImpl();
                            }
                            catch (Exception e)
                            {
                                Utils.Logger.Error(e, "ReloadDbDataIfChangedAndSetTimer()");
                            }
                        }
                        finally
                        {
                            m_memDataWlocks.m_dailyHistWlock.Release();
                        }
                    }
                    finally
                    {
                        m_memDataWlocks.m_portfoliosWlock.Release();
                    }
                }
                finally
                {
                    m_memDataWlocks.m_assetsWlock.Release();
                }
            }
            finally
            {
                m_memDataWlocks.m_usersWlock.Release();
            }

            DateTime etNow = DateTime.UtcNow.FromUtcToEt();
            DateTime targetDateEt = etNow.AddHours(1);  // Polling for change in every 1 hour
            Utils.Logger.Info($"ReloadDbDataIfChangedAndSetTimer() END. m_reloadAssetsDataTimer set next targetdate: {targetDateEt.ToSqDateTimeStr()} ET");
            m_dbReloadTimer.Change(targetDateEt - etNow, TimeSpan.FromMilliseconds(-1.0));     // runs only once
        }

        async Task ReloadDbDataIfChangedImpl()   // if necessary it reloads Historical and Realtime data
        {
            Console.WriteLine("*MemDb is not yet ready! ReloadDbData is in progress...");
            DateTime startTime = DateTime.UtcNow;
            // GA.IM.NAV assets have user_id data, so User data has to be reloaded too before Assets
            (bool isDbReloadNeeded, User[]? newUsers, List<Asset>? sqCoreAssets) = m_Db.GetDataIfReloadNeeded();
            if (!isDbReloadNeeded)
                return;

            // to minimize the time memDb is not consintent we create everything into new pointers first, then update them quickly
            var newAssetCache = AssetsCache.CreateAssetCache(sqCoreAssets!);
            // var newPortfolios = GeneratePortfolios();

            if (IsInitialized)
            {
                // if this is the periodic (not initial) reload of RedisDb, then we don't surprise clients by emptying HistPrices 
                // and not having HistPrices for 20minutes. So, we download HistPrices before swapping m_memData pointer
                DateTime startTimeHist = DateTime.UtcNow;
                var newDailyHist = await CreateDailyHist(m_Db, newUsers!, newAssetCache);  // downloads historical prices from YF. Assume it takes 20min
                if (newDailyHist == null)
                    newDailyHist = new CompactFinTimeSeries<DateOnly, uint, float, uint>();
                m_lastHistoricalDataReload = DateTime.UtcNow;
                m_lastHistoricalDataReloadTs = DateTime.UtcNow - startTimeHist;

                var newMemData = new MemData(newUsers!, newAssetCache, newDailyHist);
                m_memData = newMemData; // swap pointer in atomic operation
                Console.WriteLine($"*MemDb is ready! (#Assets: {AssetsCache.Assets.Count}, #HistoricalAssets: {DailyHist.GetDataDirect().Data.Count}) in {m_lastHistoricalDataReloadTs.TotalSeconds:0.000}sec");
            }
            else
            {
                // if this is the first time to load DB from Redis, then we don't demand HistData. Assume HistData crawling takes 20min
                // many clients can survive without historical data first. MarketDashboard. However, they need Asset and User data immediately.
                // BrAccInfo is fine wihout historical. It will send NaN as a LastClose. Fine. Client will handle it.
                // So, we don't need to wait for Historical to finish InitDb (that might take 20 minutes in the future).
                // !!! Also, in development, we don't want to wait until All HistData arrives, but start Debugging code right away after starting the WebServer.
                // Clients of MemDb should handle properly if HistData is not yet ready (NaN and later Refresh).
                var newMemData = new MemData(newUsers!, newAssetCache, new CompactFinTimeSeries<DateOnly, uint, float, uint>());
                m_memData = newMemData; // swap pointer in atomic operation
                Console.WriteLine($"*MemDb is half-ready! (#Assets: {AssetsCache.Assets.Count}, #HistoricalAssets: 0)");
            }

            m_lastDbReload = DateTime.UtcNow;
            m_lastDbReloadTs = DateTime.UtcNow - startTime;

            foreach (var brAccount in BrAccounts)
            {
                UpdateBrAccPosAssetIds(brAccount.AccPoss);
            }

            OnReloadAssetData_ReloadRtDataAndSetTimer();    // downloads realtime prices from YF or IEX
            OnReloadAssetData_ReloadRtNavDataAndSetTimer();   // downloads realtime NAVs from VBrokers
            EvDbDataReloaded?.Invoke();
        }

        public (User[], AssetsCache, CompactFinTimeSeries<DateOnly, uint, float, uint>) GetAssuredConsistentTables()
        {
            // if client wants to be totally secure and consistent when getting subtables
            MemData localMemData = m_memData; // if m_memData swap occurs, that will not ruin our consistency
            return (localMemData.Users, localMemData.AssetsCache, localMemData.DailyHist);
        }

        public void UpdateBrAccPosAssetIds(List<BrAccPos> p_accPoss)
        {
            foreach (BrAccPos pos in p_accPoss)
            {
                pos.AssetId = AssetId32Bits.Invalid;
                if (pos.Contract.SecType != "STK")
                    continue;

                var asset = AssetsCache.TryGetAsset("S/" + pos.Contract.Symbol);
                if (asset != null)
                    pos.AssetId = asset.AssetId;
            }

        }

        public void Exit()
        {
        }
    }
}