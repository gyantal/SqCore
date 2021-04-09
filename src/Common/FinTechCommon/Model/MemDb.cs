using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using SqCommon;
using System.Threading.Tasks;

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
    public partial class MemDb
    {

        public static MemDb gMemDb = new MemDb();   // Singleton pattern
        // public object gMemDbUpdateLock = new object();  // the rare clients who care about inter-table consintency (VBroker) should obtain the lock before getting pointers to subtables
        Db m_Db;

        MemData m_memData = new MemData();  // strictly private. Don't allow clients to store separate MemData pointers.
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

            await ReloadDbDataIfChangedAndSetNewTimer();  // Polling for changes every 1 hour. Downloads the AllAssets, SqCoreWeb-used-Assets from Redis Db, and 

            IsInitialized = true;
            EvFirstInitialized?.Invoke();    // inform observers that MemDb was reloaded

            // User updates only the JSON text version of data (assets, OptionPrices in either Redis or in SqlDb). But we use the Redis's Brotli version for faster DB access.
            Thread.Sleep(TimeSpan.FromSeconds(20));     // can start it in a separate thread, but it is fine to use this background thread
            UpdateRedisBrotlisService.SetTimer(new UpdateBrotliParam() { Db = m_Db });
            UpdateNavsService.SetTimer(new UpdateNavsParam() { Db = m_Db });
        }

        public void ServerDiagnostic(StringBuilder p_sb)
        {
            int memUsedKb = DailyHist.GetDataDirect().MemUsed() / 1024;
            p_sb.Append("<H2>MemDb</H2>");
            p_sb.Append($"Historical: #SqCoreWebAssets+virtualNavs: {AssetsCache.Assets.Count}. ({String.Join(',', AssetsCache.Assets.Select(r => r.LastTicker))}). Used RAM: {memUsedKb:N0}KB<br>");
            p_sb.Append($"m_lastDbReloadTs {m_lastDbReloadTs.TotalSeconds:0.0}sec, m_lastHistoricalDataReloadTs {m_lastHistoricalDataReloadTs.TotalSeconds:0.0}sec,.<br>");
            ServerDiagnosticRealtime(p_sb);
            ServerDiagnosticNavRealtime(p_sb);
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
            int memUsedKb = DailyHist.GetDataDirect().MemUsed() / 1024;
            sb.Append($"Historical: #SqCoreWebAssets+virtualNavs: {AssetsCache.Assets.Count}. ({String.Join(',', AssetsCache.Assets.Select(r => r.LastTicker))}). Used RAM: {memUsedKb:N0}KB{((p_isHtml) ? "<br>" : string.Empty)}");
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
            DateTime startTime = DateTime.UtcNow;
            // GA.IM.NAV assets have user_id data, so User data has to be reloaded too before Assets
            (bool isDbReloadNeeded, User[]? newUsers, List<Asset>? sqCoreAssets) = m_Db.GetDataIfReloadNeeded();
            if (!isDbReloadNeeded)
                return;

            // to minimize the time memDb is not consintent we create everything into new pointers first, then update them quickly
            var newAssetCache = AssetsCache.CreateAssetCache(sqCoreAssets!);
            // var newPortfolios = GeneratePortfolios();

            DateTime startTimeHist = DateTime.UtcNow;
            var newDailyHist = await CreateDailyHist(m_Db, newUsers!, newAssetCache);  // downloads historical prices from YF
            if (newDailyHist == null)
                newDailyHist = new CompactFinTimeSeries<DateOnly, uint, float, uint>();
            m_lastHistoricalDataReload = DateTime.UtcNow;
            m_lastHistoricalDataReloadTs = DateTime.UtcNow - startTimeHist;

            var newMemData = new MemData(newUsers!, newAssetCache, newDailyHist);
            m_memData = newMemData; // swap pointer in atomic operation
            m_lastDbReload = DateTime.UtcNow;
            m_lastDbReloadTs = DateTime.UtcNow - startTime;

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

        public void Exit()
        {
        }

    }

}