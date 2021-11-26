using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using SqCommon;
using System.Threading.Tasks;
using System.Globalization;
using BrokerCommon;
using IBApi;

// MemDb.Init() Flowchart: https://docs.google.com/document/d/1fwW7u4IvMIFNwx_l1YI8vop0bJ1Cl0bAPETGC-FIvxs

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
        public volatile AssetsCache AssetsCache = new AssetsCache();  // writable: user might insert a new asset from HTML UI (although this is dangerous, how to propagate it the gSheet Asset replica)

        // As Portfolios are assets (nesting), we might store portfolios in AssetCache, not separately
        public volatile List<string> Portfolios = new List<string>(); // temporary illustration of a data that will be not only read, but written by SqCore. Portfolios are not necessary here, because they are Assets as well, so they can go to AssetsCache
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
        public CompactFinTimeSeries<DateOnly, uint, float, uint> DailyHist { get { return m_memData.DailyHist; } }
        public List<string> Portfolios { get { throw new NotImplementedException(); } } // Portfolios are Assets as well, so they can go to AssetsCache

        public bool IsInitialized { get; set; } = false;

        Timer m_dbReloadTimer;  // checks every 1 hour, but reloads RAM only if Db data change is detected.

        DateTime m_lastRedisReload = DateTime.MinValue; // UTC
        TimeSpan m_lastRedisReloadTs;  // RedisDb reload: 2-3 tables: 0.2sec, It is about 100ms per table.
        DateTime m_lastFullMemDbReload = DateTime.MinValue; // UTC
        TimeSpan m_lastFullMemDbReloadTs;

        public List<BrAccount> BrAccounts{ get; set; } = new List<BrAccount>();   // only Broker dependent data. When AssetCache is reloaded from RedisDb, this should not be wiped or reloaded

        public delegate void MemDbEventHandler();
        // Don't send too granual (separate EvAssetReloaded, EvBrokerDataReloaded) events to observers, because they will be confused.
        // These 2 broad categories are enough: Full MemDbReload (happened because Assets changed in RedisDb), and 3-4 times a day HistoricalData reload, which should be periodic as clients should know when PreviousClose prices change.
        // Observers will be confused to receive many messages. 
        // Just use 3 events: EvMemDbDataReloadedNoHistoryYet, EvFullMemDbDataReloaded, and EvOnlyHistoricalDataReloaded
        public event MemDbEventHandler? EvMemDbInitNoHistoryYet = null;
        public event MemDbEventHandler? EvFullMemDbDataReloaded = null;
        public event MemDbEventHandler? EvOnlyHistoricalDataReloaded = null;


#pragma warning disable CS8618 // Non-nullable field 'm_assetDataReloadTimer' is uninitialized.
        public MemDb()  // constructor runs in main thread
        {
        }
#pragma warning restore CS8618

        public void Init(Db p_db)
        {
            m_Db = p_db;
            Utils.RunInNewThread(ignored => Init_WT()); // Better to do long consuming data preprocess in working thread than in the main thread, so other services can start to initialize in parallel
        }

        // MemDb.Init() Flowchart: https://docs.google.com/document/d/1fwW7u4IvMIFNwx_l1YI8vop0bJ1Cl0bAPETGC-FIvxs
        async void Init_WT()    // WT : WorkThread
        {
            Thread.CurrentThread.Name = "MemDb.Init_WT Thread";
            Console.WriteLine("*MemDb is not yet ready! ReloadDbData is in progress...");
            DateTime startTime = DateTime.UtcNow;
            try
            {
                // Step 1: Redis Assets, Users
                // GA.IM.NAV assets have user_id data, so User data has to be reloaded too before Assets
                (bool isDbReloadNeeded, User[]? newUsers, List<Asset>? newAssets) = m_Db.GetDataIfReloadNeeded();    // isDbReloadNeeded can be ignored as it is surely true at Init()
                var newAssetCache = new AssetsCache(newAssets!);               // TODO: var newPortfolios = GeneratePortfolios();
                m_memData = new MemData(newUsers!, newAssetCache, new CompactFinTimeSeries<DateOnly, uint, float, uint>());
                m_lastRedisReload = DateTime.UtcNow;
                m_lastRedisReloadTs = DateTime.UtcNow - startTime;
                // can inform Observers that MemDb is 1/4th ready: Users, Assets OK
                Console.WriteLine($"*MemDb is 1/4-ready! RedisAssets (#Assets:{AssetsCache.Assets.Count},#Brokers:0,#HistAssets:0) in {m_lastRedisReloadTs.TotalSeconds:0.000}sec");
                // spawn threads for Broker Connections and ReloadHistData

                // Step 2: BrokerNav, Poss in separate threads
                BrAccounts.Add(new BrAccount() { GatewayId = GatewayId.CharmatMain, NavAsset = (BrokerNav)m_memData.AssetsCache.GetAsset("N/DC.IM") });
                BrAccounts.Add(new BrAccount() { GatewayId = GatewayId.DeBlanzacMain, NavAsset = (BrokerNav)m_memData.AssetsCache.GetAsset("N/DC.ID") });
                BrAccounts.Add(new BrAccount() { GatewayId = GatewayId.GyantalMain, NavAsset = (BrokerNav)m_memData.AssetsCache.GetAsset("N/GA.IM") });
                List<Task> brTasks = new List<Task>();
                foreach (var brAccount in BrAccounts)
                {
                    Task brTask = Task.Run(() =>    // Task.Run() runs it immediately
                    {
                        // IB API is not async. Thread waits until the connection is established.
                        // Task.Run() uses threads from the thread pool, so it executes those connections parallel in the background. Then wait for them.
                        bool isConnected = BrokersWatcher.gWatcher.GatewayReconnect(brAccount.GatewayId);
                        if (!isConnected)
                            return;
                        MemDb.gMemDb.UpdateBrAccount(brAccount, brAccount.GatewayId);  // brAccount.Poss
                        brAccount.NavAsset!.LastValue = (float)brAccount.NetLiquidation;    // fill RT price
                    });
                    brTasks.Add(brTask);
                }

                // Step 3: Assets history in separate thread
                Task histTask = Task.Run(async () =>    // Task.Run() runs it 'immediately'
                {
                    var newDailyHist = await CreateDailyHist(m_memData, m_Db);
                    if (newDailyHist != null)   // if it fails, keep the empty DailyHist that was just created at the start
                        m_memData.DailyHist = newDailyHist;  // swap pointer in atomic operation
                    // Here don't call EvOnlyHistoricalDataReloaded.Invoke(), because we will call later the EvFullMemDbDataReloaded?.Invoke();
                });

                // Step 4: PriorClose and Rt prices download in the current thread. This is the quickest.
                InitAllAssetsPriorCloseAndLastPrice(newAssetCache);  // many services need PriorClose and LastPrice immediately. HistPrices can wait, but not this.
                Console.WriteLine($"*MemDb is 2/4-ready! Prior,Rt (#Assets:{AssetsCache.Assets.Count},#Brokers:0,#HistAssets:0) in {(DateTime.UtcNow - startTime).TotalSeconds:0.000}sec");

                // Step 5: Wait for threads completion and inform observers via events or WaitHandles (ManualResetEvent)
                await Task.WhenAll(brTasks);
                int nConnectedBrokers = BrAccounts.Count(r => BrokersWatcher.gWatcher.IsGatewayConnected(r.GatewayId));
                Console.WriteLine($"*MemDb is 3/4-ready! BrokerNav,Poss (#Assets:{AssetsCache.Assets.Count},#Brokers:{nConnectedBrokers},#HistAssets:0) in {(DateTime.UtcNow - startTime).TotalSeconds:0.000}sec");
                EvMemDbInitNoHistoryYet?.Invoke();

                // inform Observers that MemDb is 3/4rd ready: Assets, PriorAndRtPrices, BrokerNavAndPoss.
                // However, don't go too many gradual events, because observers will be confused to receive many messages. 
                // Just use these 3 events: EvMemDbInitNoHistoryYet, EvFullMemDbDataReloaded, and EvOnlyHistoricalDataReloaded

                // When this is the first time to load DB from Redis, then we don't demand HistData. Assume HistData crawling for 700 stocks takes 20min
                // Many clients can survive without historical data first. MarketDashboard. However, they need Asset and User data immediately.
                // BrAccInfo is fine wihout historical. It will send NaN as a PriorClose. Fine. Client will handle it.
                // So, we don't need to wait for Historical to finish InitDb (that might take 20 minutes in the future).
                // !!! Also, in development, we don't want to wait until All HistData arrives, but start Debugging code right away after starting the WebServer.
                // Clients of MemDb should handle properly if HistData is not yet ready (NaN and later Refresh).

                await histTask;
                m_lastFullMemDbReload = DateTime.UtcNow;
                m_lastFullMemDbReloadTs = DateTime.UtcNow - startTime;
                Console.WriteLine($"*MemDb is full-ready! Hist (#Assets:{AssetsCache.Assets.Count},#Brokers:{nConnectedBrokers},#HistAssets:{m_memData.DailyHist.GetDataDirect().Data.Count }) in {m_lastFullMemDbReloadTs.TotalSeconds:0.000}sec");
                // Benchmarking: EvMemDbInitNoHistoryYet: 1.2sec (when MemDb is usable for clients), because Broker.Connections is 800ms each, but it is parallelized, while 20 YF history is extra 5sec, 
                // so altogether if single-threaded it would be 0.2(Redis)+1+1+1(Brokers)+5 = 8.3sec, 
                // but because it is massively multithreaded it finishes in 5.3sec, which is the necessery Redis + the longest task the YfHistory. A saving of 3seconds

                // inform Observers that MemDb is full ready: Assets, PriorAndRtPrices, BrokerNavAndPoss, AssetHist
                IsInitialized = true;
                EvFullMemDbDataReloaded?.Invoke();

                // Step 6: Start timers: Broker.ReconnectTimers, MemDb.StockRt-priceTimers(high-low-mid), MemDb.HistDataTimer. The CheckBrokerPoss-timer works in a Service, which schedules itself
                m_dbReloadTimer = new System.Threading.Timer(new TimerCallback(ReloadDbDataTimer_Elapsed), this, TimeSpan.FromMilliseconds(-1.0), TimeSpan.FromMilliseconds(-1.0));
                ScheduleReloadDbDataIfChangedTimer();
                BrokersWatcher.gWatcher.ScheduleReconnectTimer();
                InitAndScheduleHistoricalTimer();
                OnReloadAssetData_InitAndScheduleRtTimers();
                InitAndScheduleNavRtTimers();

                // Thread.Sleep(TimeSpan.FromSeconds(20));     // can start it in a separate thread, but it is fine to use this background thread
                UpdateRedisBrotlisService.SetTimer(new UpdateBrotliParam() { Db = m_Db });
                UpdateNavsService.SetTimer(new UpdateNavsParam() { Db = m_Db });
            }
            catch (System.Exception e)
            {
                Console.WriteLine($"!!! Critical: MemDb.Init_WT() exception. No MemDb. Only some console commands are available. Check Log.");
                Utils.Logger.Error(e, "Error in MemDb.Init_WT().");
            }
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
            var lastDbReloadWithoutHist = m_lastFullMemDbReloadTs - g_lastHistoricalDataReloadTs;
            p_sb.Append($"m_lastHistoricalDataReloadTimeUtc: '{g_lastHistoricalDataReload}', lastDbReloadWithoutHist: {lastDbReloadWithoutHist.TotalSeconds:0.000}sec, m_lastHistoricalDataReloadTs: {g_lastHistoricalDataReloadTs.TotalSeconds:0.000}sec.<br>");

            var yfTickers = AssetsCache.Assets.Where(r => r.AssetId.AssetTypeID == AssetType.Stock).Select(r => ((Stock)r).YfTicker).ToArray();
            p_sb.Append($"StockAssets (#{yfTickers.Length}): ");
            p_sb.AppendLongListByLine(yfTickers, ",", 30, "<br>");
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
            ScheduleReloadDbDataIfChangedTimer();
        }

        internal void ScheduleReloadDbDataIfChangedTimer()
        {
            DateTime etNow = DateTime.UtcNow.FromUtcToEt();
            DateTime targetDateEt = etNow.AddHours(1);  // Polling for change in every 1 hour
            Utils.Logger.Info($"ReloadDbDataIfChangedAndSetTimer() END. m_reloadAssetsDataTimer set next targetdate: {targetDateEt.ToSqDateTimeStr()} ET");
            m_dbReloadTimer.Change(targetDateEt - etNow, TimeSpan.FromMilliseconds(-1.0));     // runs only once
        }

        async Task ReloadDbDataIfChangedImpl()   // if necessary it reloads Historical and Realtime data
        {
            Console.WriteLine("*ReloadDbDataIfChangedImpl() is in progress...");
            DateTime startTime = DateTime.UtcNow;
            // GA.IM.NAV assets have user_id data, so User data has to be reloaded too before Assets
            (bool isDbReloadNeeded, User[]? newUsers, List<Asset>? sqCoreAssets) = m_Db.GetDataIfReloadNeeded();
            if (!isDbReloadNeeded)
                return;

            // to minimize the time memDb is not consintent we create everything into new pointers first, then update them quickly
            var newAssetCache = new AssetsCache(sqCoreAssets!);
            var newMemData = new MemData(newUsers!, newAssetCache, new CompactFinTimeSeries<DateOnly, uint, float, uint>());
            
            var newDailyHist = await CreateDailyHist(newMemData, m_Db); // downloads historical prices from YF. Assume it takes 20min
            // If reload HistData fails AND if it is a forced ReloadRedisDb, because assets changed Assets => we throw away old history, even if download fails.
            if (newDailyHist != null)
                newMemData.DailyHist = newDailyHist;

            InitAllAssetsPriorCloseAndLastPrice(newAssetCache);  // many services need PriorClose and LastPrice immediately. HistPrices can wait, but not this.
            
            m_memData = newMemData; // swap pointer in atomic operation
            Console.WriteLine($"*MemDb is ready! (#Assets: {AssetsCache.Assets.Count}, #HistoricalAssets: {DailyHist.GetDataDirect().Data.Count}) in {g_lastHistoricalDataReloadTs.TotalSeconds:0.000}sec");

            m_lastFullMemDbReload = DateTime.UtcNow;
            m_lastFullMemDbReloadTs = DateTime.UtcNow - startTime;

            // at ReloadDbData(), we can decide to fully reload the BrokerNav, Poss from Brokers (maybe safer). But at the moment, we decided just to update the in-memory BrAccounts array with the new AssetIds
            foreach (var brAccount in BrAccounts)
            {
                UpdateBrAccPosAssetIds(brAccount);
            }

            OnReloadAssetData_InitAndScheduleRtTimers();
            OnReloadAssetData_ReloadRtNavDataAndSetTimer();   // downloads realtime NAVs from VBrokers
            EvFullMemDbDataReloaded?.Invoke();
        }

        public (User[], AssetsCache, CompactFinTimeSeries<DateOnly, uint, float, uint>) GetAssuredConsistentTables()
        {
            // if client wants to be totally secure and consistent when getting subtables
            MemData localMemData = m_memData; // if m_memData swap occurs, that will not ruin our consistency
            return (localMemData.Users, localMemData.AssetsCache, localMemData.DailyHist);
        }

        public void UpdateBrAccount(GatewayId p_gatewayId)
        {
            BrAccount? brAccount = null;
            foreach (var account in BrAccounts)
            {
                if (account.GatewayId == p_gatewayId)
                {
                    brAccount = account;
                    break;
                }
            }
            if (brAccount == null)
            {
                brAccount = new BrAccount() { GatewayId = p_gatewayId };
                BrAccounts.Add(brAccount);
            }
            UpdateBrAccount(brAccount, p_gatewayId);
        }

        public void UpdateBrAccount(BrAccount brAccount, GatewayId p_gatewayId)
        {
            List<BrAccSum>? accSums = BrokersWatcher.gWatcher.GetAccountSums(p_gatewayId);
            if (accSums == null)
                return;

            List<BrAccPos>? accPoss = BrokersWatcher.gWatcher.GetAccountPoss(p_gatewayId);
            if (accPoss == null)
                return;

            brAccount.NetLiquidation = accSums.GetValue(AccountSummaryTags.NetLiquidation);
            brAccount.GrossPositionValue = accSums.GetValue(AccountSummaryTags.GrossPositionValue);
            brAccount.TotalCashValue = accSums.GetValue(AccountSummaryTags.TotalCashValue);
            brAccount.InitMarginReq = accSums.GetValue(AccountSummaryTags.InitMarginReq);
            brAccount.MaintMarginReq = accSums.GetValue(AccountSummaryTags.MaintMarginReq);
            brAccount.AccPoss = accPoss;
            brAccount.LastUpdate = DateTime.UtcNow;

            UpdateBrAccPosAssetIds(brAccount);
        }

        public void UpdateBrAccPosAssetIds(BrAccount p_brAccount)
        {
            p_brAccount.AccPossUnrecognizedAssets = new List<BrAccPos>();
            foreach (BrAccPos pos in p_brAccount.AccPoss)
            {
                Asset? asset = null;
                Contract contract = pos.Contract;
                switch (contract.SecType)
                {
                    case "STK":
                        asset = AssetsCache.TryGetAsset("S/" + contract.Symbol);
                        if (asset == null)
                            p_brAccount.AccPossUnrecognizedAssets.Add(pos);
                        break;
                    case "OPT":
                        if (contract.Symbol == "VIX")
                        {
                            Utils.Logger.Info($"UpdateBrAccPosAssetIds(): Skip VIX-futures option contract.'{contract.LocalSymbol}'");
                            break;  // TEMP: ignore VIX index options at the moment. Just concentrate on StockOptions, not FuturesOptions. Too many different contracts. They just add up clutter on the UI. Also, they are based on Futures, not stocks, so more difficult to implement them.
                        }

                        string sqTicker = Option.GenerateSqTicker(contract.Symbol, contract.LastTradeDateOrContractMonth, contract.Right[0], contract.Strike);
                        asset = AssetsCache.TryGetAsset(sqTicker);
                        if (asset == null)
                        {
                            if (contract.Currency != "USD")
                                break;
                            if (!Int32.TryParse(contract.Multiplier, out int multiplier))
                                break;

                            OptionRight right = contract.Right switch
                            {
                                "C" => OptionRight.Call,
                                "P" => OptionRight.Put,
                                _ => OptionRight.Unknown
                            };
                            asset = AssetsCache.CreateNonPersistedOption(CurrencyId.USD, OptionType.StockOption, contract.Symbol, contract.LastTradeDateOrContractMonth, right, contract.Strike, multiplier);
                        }
                        // p_brAccount.AccPossUnrecognizedAssets.Add(pos);
                        break;
                    case "CASH":    // Ignore Virtual Forex positions. "Cash contract" is virtual (to follow the AvgPrice). 
                        // Cash position is real, but that is part of the Account, not the contracts. For the BrAccViewer UI, the cash positions are not important. They don't change intraday, and they would just clutter the datatable.
                        // You can remove Virtual Forex positions in IB: Account/VirtualFx positions/RightClick/Change Virtual price or position/Set position = 0 / Restart IB.
                        Utils.Logger.Info($"UpdateBrAccPosAssetIds(): Skip Virtual Forex contracts, which is not the real cash. It is only the VirtualFX position in the Account dialog. Set Pos=0 in TWS and it will disappear. Pos: {pos.Position}, Currency: {contract.Currency}, LocalSymbol: {contract.LocalSymbol}. Something weird. Seemingly wrong position, and only one Cash arrived.");
                        break;
                    default:
                        Utils.Logger.Warn($"UpdateBrAccPosAssetIds(): unrecognized SecType: '{contract.SecType}'");
                        break;
                }

                pos.AssetObj = asset;
                if (asset == null)
                    pos.AssetId = AssetId32Bits.Invalid;
                else
                    pos.AssetId = asset.AssetId;
            }

            StringBuilder sb = new StringBuilder($"MemDb.UpdateBrAccPosAssetIds(). Unrecognised IB contracts as valid SqCore assets (#{p_brAccount.AccPossUnrecognizedAssets.Count}): ");
            var unrecognizedExtendedSymbols = p_brAccount.AccPossUnrecognizedAssets.Select(r => r.Contract.SecType + ":" + r.Contract.Symbol);
            sb.AppendLongListByLine(unrecognizedExtendedSymbols, ",", 1000, "");
            Utils.Logger.Warn(sb.ToString());
        }

        public void Exit()
        {
        }
    }
}