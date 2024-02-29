using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Fin.Base;
using Newtonsoft.Json;
using QuantConnect;
using QuantConnect.Configuration;
using QuantConnect.Data;
using QuantConnect.Data.Auxiliary;
using QuantConnect.Interfaces;
using QuantConnect.Lean.Engine;
using QuantConnect.Lean.Engine.Alpha;
using QuantConnect.Lean.Engine.Alphas;
using QuantConnect.Lean.Engine.DataFeeds;
using QuantConnect.Lean.Engine.RealTime;
using QuantConnect.Lean.Engine.Results;
using QuantConnect.Lean.Engine.Server;
using QuantConnect.Lean.Engine.Setup;
using QuantConnect.Lean.Engine.TransactionHandlers;
using QuantConnect.Packets;
using QuantConnect.Parameters;
using QuantConnect.Util;

namespace Fin.MemDb;

public static class Backtester
{
    static Controls gControls = null!;

    static string gAlgorithmDllRelPath = null!;
    static byte[] gAlgorithmDllBinary = null!;
    static string gPacketAccessToken = null!;
    internal static void Init()
    {
        // global inits not related to a specific QcAlgorithm
        gControls = new Controls()
        {
            MinuteLimit = Config.GetInt("symbol-minute-limit", 10000),
            SecondLimit = Config.GetInt("symbol-second-limit", 10000),
            TickLimit = Config.GetInt("symbol-tick-limit", 10000),
            RamAllocation = int.MaxValue,
            MaximumDataPointsPerChartSeries = Config.GetInt("maximum-data-points-per-chart-series", 4000),
            StorageLimit = Config.GetValue("storage-limit", 10737418240L),
            StorageFileCount = Config.GetInt("storage-file-count", 10000),
            StoragePermissions = (FileAccess)Config.GetInt("storage-permissions", (int)FileAccess.ReadWrite)
        };

        gAlgorithmDllRelPath = OperatingSystem.IsWindows() ? "bin/Debug/net8.0/Fin.Algorithm.CSharp.dll" : "Fin.Algorithm.CSharp.dll";
        gAlgorithmDllBinary = File.ReadAllBytes(gAlgorithmDllRelPath); // reads the whole DLL as binary. Keep the Fin.Algorithm.CSharp.dll small

        gPacketAccessToken = Config.Get("api-access-token");
    }

    internal static void Exit()
    {
        OS.Dispose(); // CpuPerformance._cpuThread will be reused, don't dispose it in Backtest(), but when app closes
    }

    public static LeanEngineSystemHandlers CreateSystemHandlers(Composer composer)
    {
        // Currently composer.GetExportedValueByTypeName() gives back the global instances. Not good if Backtests run in parallel.
        // Later, replace them to non-global objects. Maybe we don't have to do it if we will not use this Engine based backtest. See the quicker backtest version.
        return new LeanEngineSystemHandlers(
            composer.GetExportedValueByTypeName<IJobQueueHandler>(Config.Get("job-queue-handler")),
            composer.GetExportedValueByTypeName<IApi>(Config.Get("api-handler")),
            composer.GetExportedValueByTypeName<IMessagingHandler>(Config.Get("messaging-handler")),
            composer.GetExportedValueByTypeName<ILeanManager>(Config.Get("lean-manager-type", "LocalLeanManager")));
    }

    public static LeanEngineAlgorithmHandlers CreateAlgorithmHandlers(Composer composer, bool researchMode = false, bool liveMode = false)
    {
        // have to create new BacktestingTransactionHandler() every time for Engine.Run()
        // Otherwise at second Engine.Run(), BacktestingTransactionHandler._cancellationTokenSource : 'The CancellationTokenSource has been disposed'
        var setupHandlerTypeName = Config.Get("setup-handler", "ConsoleSetupHandler");
        // var transactionHandlerTypeName = Config.Get("transaction-handler", "BacktestingTransactionHandler");
        // var realTimeHandlerTypeName = Config.Get("real-time-handler", "BacktestingRealTimeHandler");
        // var dataFeedHandlerTypeName = Config.Get("data-feed-handler", "FileSystemDataFeed");
        // var resultHandlerTypeName = Config.Get("result-handler", "BacktestingResultHandler");
        var mapFileProviderTypeName = Config.Get("map-file-provider", "LocalDiskMapFileProvider");
        var factorFileProviderTypeName = Config.Get("factor-file-provider", "LocalDiskFactorFileProvider");
        var dataProviderTypeName = Config.Get("data-provider", "DefaultDataProvider");
        // var alphaHandlerTypeName = Config.Get("alpha-handler", "DefaultAlphaHandler");
        var objectStoreTypeName = Config.Get("object-store", "LocalObjectStore");
        var dataPermissionManager = Config.Get("data-permission-manager", "DataPermissionManager");

        var result = new LeanEngineAlgorithmHandlers(
            new BacktestingResultHandler(), // ResultsHandler.Result is the outcome. Should be not a shared instance
            composer.GetExportedValueByTypeName<ISetupHandler>(setupHandlerTypeName),
            new FileSystemDataFeed(), // FileSystemDataFeed contains _subscriptions collection, that contains the data subscriptions (SPY-Daily, SPY-QC-Minute) for that algorithm. Should be not a shared instance
            new BacktestingTransactionHandler(), // Exception otherwise: BacktestingTransactionHandler._cancellationTokenSource : 'The CancellationTokenSource has been disposed'
            // AlgorithmHandlers.RealTime handles backtesting realtime time, and Schedule On() events. Should be not a shared instance.
            // AlgorithmHandlers.RealTime.ScheduledEvents has to be emptied before second runs, otherwise the Triggers are duplicated. But doing so, didn't solve the second run problem yet. There is something else.
            // And thinking about multithread runs, it is better to always recreate (new) the AlgorithmHandlers.RealTime for the backtest.
            new BacktestingRealTimeHandler(),
            composer.GetExportedValueByTypeName<IMapFileProvider>(mapFileProviderTypeName),
            composer.GetExportedValueByTypeName<IFactorFileProvider>(factorFileProviderTypeName),
            composer.GetExportedValueByTypeName<IDataProvider>(dataProviderTypeName),
            new DefaultAlphaHandler(), // DefaultAlphaHandler contains List<Insight>, that contains the symbols for that algorithm. Should be not a shared instance
            composer.GetExportedValueByTypeName<IObjectStore>(objectStoreTypeName),
            composer.GetExportedValueByTypeName<IDataPermissionManager>(dataPermissionManager),
            liveMode,
            researchMode);

        result.FactorFileProvider.Initialize(result.MapFileProvider, result.DataProvider);
        result.MapFileProvider.Initialize(result.DataProvider);

        if (result.DataProvider is ApiDataProvider
            && (result.FactorFileProvider is not LocalZipFactorFileProvider || result.MapFileProvider is not LocalZipMapFileProvider))
        {
            throw new ArgumentException($"The {typeof(ApiDataProvider)} can only be used with {typeof(LocalZipFactorFileProvider)}" +
                $" and {typeof(LocalZipMapFileProvider)}, please update 'config.json'");
        }

        return result;
    }

    // p_algorithmParam: SqCore params: e.g. "assets=SPY,TLT&weights=60,40&rebFreq=Daily,30d"
    // p_parameters (QC params from config.json): e.g. "{\"ema-fast\":10,\"ema-slow\":20}"
    public static BacktestingResultHandler BacktestInSeparateThreadWithTimeout(string p_algorithmTypeName,  string p_algorithmParam, List<Trade>? p_portTradeHist, string p_parameters, SqResult p_sqResult) // Engine uses a separate thread with ExecuteWithTimeLimit()
    {
        string? previousThreadName = Thread.CurrentThread.Name;
        Thread.CurrentThread.Name = $"QC.Engine.Run({p_algorithmTypeName})";
        Console.WriteLine($"QC: Engine.Run backtest...{p_algorithmTypeName}...");
        Stopwatch stopwatch = Stopwatch.StartNew();

        SqBacktestConfig sqBacktestConfig = new()
        {
            SqResult = p_sqResult
        };

        // Instead of using JobQueue as in QC.Launcher, we implement the gist of it. Better to see what is required, and better to customize. Some parts can go to Backtester Init(). Like Loading Fin.Algorithm.CSharp.dll.
        // 1. Create the job.
        // var parametersConfigString = Config.Get("parameters");
        var parameters = (p_parameters == string.Empty) ? new Dictionary<string, string>() : JsonConvert.DeserializeObject<Dictionary<string, string>>(p_parameters);
        var job = new BacktestNodePacket(0, 0, string.Empty, Array.Empty<byte>(), Config.Get("backtest-name"))
        {
            Type = PacketType.BacktestNode,
            Algorithm = gAlgorithmDllBinary,
            HistoryProvider = Config.Get("history-provider", "SubscriptionDataReaderHistoryProvider"),
            Channel = gPacketAccessToken,
            UserToken = gPacketAccessToken,
            UserId = Config.GetInt("job-user-id", 0),
            ProjectId = Config.GetInt("job-project-id", 0),
            OrganizationId = Config.Get("job-organization-id"),
            Version = Globals.Version,
            BacktestId = p_algorithmTypeName, // Config.Get("algorithm-id", Config.Get("algorithm-type-name")), // "algorithm-type-name": "BasicTemplateFrameworkAlgorithm"
            Language = Language.CSharp,
            Parameters = parameters,
            Controls = gControls,
            PythonVirtualEnvironment = Config.Get("python-venv")
        };

        // 2. Create the algorithm manager and start our engine
        bool liveMode = false;   // var liveMode = Config.GetBool("live-mode");
        var systemHandlers = Backtester.CreateSystemHandlers(Composer.Instance); // even SystemHandlers cannot be global, because Engine.Run() calls SystemHandlers.LeanManager.SetAlgorithm(algorithm); so, they have a state. There are many users who can run backtests parallel
        systemHandlers.Initialize();
        AlgorithmManager algorithmManager = new(liveMode, job);
        LeanEngineAlgorithmHandlers algorithmHandlers = Backtester.CreateAlgorithmHandlers(Composer.Instance, false, liveMode); // BacktestingTransactionHandler() has to be a new instance
        systemHandlers.LeanManager.Initialize(systemHandlers, algorithmHandlers, job, algorithmManager);
        algorithmHandlers.Results.SqBacktestConfig = sqBacktestConfig; // Initialize BacktestingResultHandler with our config very early. SqBacktestConfig might be needed in early Inits()
        algorithmHandlers.DataMonitor.SqBacktestConfig = sqBacktestConfig;
        algorithmHandlers.Alphas.SqBacktestConfig = sqBacktestConfig;

        // 3. OS is needed. Because BasicResultHandler creates a new Thread ("Result Thread"), that collects CPU Usage% periodically for a 'performanceCharts'
        // Also engine Trace: "Isolator.ExecuteWithTimeLimit(): Used: 9, Sample: 79, App: 208, CurrentTimeStepElapsed: 00:00.000. CPU: 1%" (OS.CpuUsage)
        OS.Initialize();

        var engine = new Engine(systemHandlers, algorithmHandlers, liveMode);

        // in engine.Run(), the BacktestingResultHandler.ConfigureConsoleTextWriter() redirects Console.Out for collecting BacktestingResultHandler.LogStore, and forgets to set it back.
        // BacktestingResultHandler.Exit() should revert it back, but it is badly written code. It doesn't even save the old Console.Out.
        // Actually, BaseResultsHandler.Exit() reverts it back. Just there was an exception and that is why it didn't go further.
        // But it is a good lesson: engine.Run() highjacks the global Console, so any other thread cannot use that. It does it for collecting Logs. Maybe it shouldn't do that.
        // Actually, now that BaseResultsHandler.Exit() is called, and it sets back something, but that is not a proper Console,
        // Even in QuantConnect.Launcher.exe: Console.Write() doesn't work after engine.Run(). The bug is in their code, not in SqCore.

        // BaseResultsHandler.StandardOut: 'static' is not good. If it is static, it will only assigned when it is first used in BaseResultsHandler.Exit(),
        // but at that time it is already overwritten by BacktestingResultsHandler.ConfigureConsoleTextWriter()
        // The quickest modification, just eleminating the 'static'. As instance field, it will be filled up at ctor time.
        // private static readonly TextWriter StandardOut = Console.Out;
        // private static readonly TextWriter StandardError = Console.Error;
        // var savedConsoleOut = Console.Out;
        // var savedConsoleError = Console.Error;

        // QC example uses static WorkerThread.Instance, but that is bad. Because:
        // 1. WorkerThread is Disposed in Engine.Run(), so next run raises exception: "The collection has been marked as complete with regards to additions."
        // 2. In our WebServer, we want to run Engin.Run() parallel if many users doing backtest at the same time.
        // We don't do a single JobQueue if possible. (maybe in the future, but we have enough CPU cores to run these backtest parallel)
        // SqCore Change ORIGINAL:
        // engine.Run(job, algorithmManager, algorithmDllRelPath, WorkerThread.Instance);
        // SqCore Change NEW:
        engine.Run(job, algorithmManager, gAlgorithmDllRelPath, null, sqBacktestConfig, p_algorithmParam, p_portTradeHist);
        // SqCore Change END

        // Console.SetOut(savedConsoleOut);
        // Console.SetError(savedConsoleError);

        stopwatch.Stop();
        Console.WriteLine($"Backtest took {stopwatch.Elapsed.TotalMilliseconds:f3}ms"); // On linux (1st, 2nd, 3rd runs): 896ms, 166ms, 90ms (some Providers cache data probably)

        // There is an order Fee, because the QCAlgorithm.BrokerageModel = DefaultBrokerageModel, and its GetFeeModel() uses InteractiveBrokersFeeModel();
        // If we want to change that for our backtest, we have to replace the DefaultBrokerageModel in QCAlgorithm.Initialize() (or change the QC default code)

        // File Outputs (created in the CurrentDir: the EXE folder in QC.Launcher and in the SqCoreWeb folder in SqCore):
        // 1. Log.txt : the long Log file.
        // 2. BasicTemplateFrameworkAlgorithm-log.txt : The backtestResults.LogStore. We have it.
        // 3. data-monitor-report-20221215212656824.json : contains number of succeeded-data-requests, failed-data-requests
        // 4. succeeded-data-requests-20221215221325192.txt : list of files that were successfully loaded.
        // 5. failed-data-requests-20221215221325192.txt

        // 6. BasicTemplateFrameworkAlgorithm.json : 110KB. Everything. 9 Charts. PV, Drawdown charts. Everything.
        // 7. BasicTemplateFrameworkAlgorithm\alpha-results.json : Insights
        // 8. BasicTemplateFrameworkAlgorithm-order-events.json : all the orders in a nice JSON format.
        // 9. <optional> it can save a Transaction-log TXT in SaveListOfTrades() if Config."transaction-log" is set.

        // We have all of those files, but all is created in the current folder, which is the SqCore.Web.
        // First, try to redirect them to the EXE folder (to not clutter the source code).
        // Possible with config.json: ResultsDestinationFolder = Config.Get("results-destination-folder", Directory.GetCurrentDirectory());
        // Secondly, try to disable the creation with a bool variables. SqCoreWeb doesn't need files only in rare Debugging situations.
        // Just memory results. Creating these files can be 100ms out of the 600ms time.

        // clean up resources
        // systemHandlers.DisposeSafely(); // don't dispose them in Backtest(), because now they are still globally reused next time running a Backtest()
        // algorithmHandlers.DisposeSafely(); // don't dispose them in Backtest(), because now they are still globally eused next time running a Backtest()

        var backtestResults = (BacktestingResultHandler)engine.AlgorithmHandlers.Results;
        Thread.CurrentThread.Name = previousThreadName;
        return backtestResults;
    }

    public static BacktestingResultHandler BacktestInThreadWithNoTimeout(/* string p_algorithmTypeName, string p_parameters */) // lighter weight. Less global variable usage. Calls algorithmManager.Run() directly. No timeout logic.
    {
        // We can do everything here what Engine.Run() can do. All the initializations, but only the Necessary ones. Also, pay attention to not use global Handlers.
        // However, when this is ready, check and compare the running time of the Engine.Run() global Handlers version.
        // That running time is // On linux (1st, 2nd, 3rd runs): 896ms, 166ms, 90ms (some Providers cache data probably)
        // So, if we some of our handlers can be kept as Global, than it is faster execution. We will only know  this after we implemented this function here.

        // The TestAlgorithmManagerSpeed() example is too small. Not enough. It uses nullSynchronizer, which is not good for us. We have to implement the whole Init what Engine.Run() does.
        // It can be a long and erronous task. Can take 2 days to shine it. We will do it later.

        // var algorithm = PerformanceBenchmarkAlgorithms.SingleSecurity_Second;
        // var feed = new FileSystemDataFeed();
        // var marketHoursDatabase = MarketHoursDatabase.FromDataFolder();
        // var symbolPropertiesDataBase = SymbolPropertiesDatabase.FromDataFolder();
        // var dataPermissionManager = new DataPermissionManager();
        // var dataManager = new DataManager(feed,
        //  new UniverseSelection(
        //      algorithm,
        //      new SecurityService(algorithm.Portfolio.CashBook, marketHoursDatabase, symbolPropertiesDataBase, algorithm, RegisteredSecurityDataTypesProvider.Null, new SecurityCacheProvider(algorithm.Portfolio)),
        //      dataPermissionManager,
        //      TestGlobals.DataProvider),
        // algorithm,
        // algorithm.TimeKeeper,
        // marketHoursDatabase,
        // false,   // _liveMode
        // RegisteredSecurityDataTypesProvider.Null,
        // dataPermissionManager);

        // algorithm.SubscriptionManager.SetDataManager(dataManager);

        // synchronizer.Initialize(algorithm, dataManager);

        // // Initialize the data feed before we initialize so he can intercept added securities/universes via events
        // AlgorithmHandlers.DataFeed.Initialize(
        //     algorithm,
        //     job,
        //     AlgorithmHandlers.Results,
        //     AlgorithmHandlers.MapFileProvider,
        //     AlgorithmHandlers.FactorFileProvider,
        //     AlgorithmHandlers.DataProvider,
        //     dataManager,
        //     (IDataFeedTimeProvider)synchronizer,
        //     AlgorithmHandlers.DataPermissionsManager.DataChannelProvider);

        // //Initialize the internal state of algorithm and job: executes the algorithm.Initialize() method.
        // initializeComplete = AlgorithmHandlers.Setup.Setup(new SetupHandlerParameters(dataManager.UniverseSelection, algorithm, brokerage, job, AlgorithmHandlers.Results,
        //     AlgorithmHandlers.Transactions, AlgorithmHandlers.RealTime, AlgorithmHandlers.ObjectStore, AlgorithmHandlers.DataCacheProvider, AlgorithmHandlers.MapFileProvider));

        // //Run Algorithm Job: (From Engine.Run())
        // // -> Using this Data Feed,
        // // -> Send Orders to this TransactionHandler,
        // // -> Send Results to ResultHandler.
        // algorithmManager.Run(job, algorithm, synchronizer, AlgorithmHandlers.Transactions, AlgorithmHandlers.Results, AlgorithmHandlers.RealTime, SystemHandlers.LeanManager, AlgorithmHandlers.Alphas, isolator.CancellationToken);

        // // From AlgorithmManagerTests.cs
        // algorithmManager.Run(job, algorithm, nullSynchronizer, transactions, results, realtime, leanManager, alphas, token);

        return new BacktestingResultHandler();

        // ************************************************************

        // >In one of the Tests:
        // var algorithm = PerformanceBenchmarkAlgorithms.SingleSecurity_Second;
        // var feed = new FileSystemDataFeed();
        // var marketHoursDatabase = MarketHoursDatabase.FromDataFolder();
        // var symbolPropertiesDataBase = SymbolPropertiesDatabase.FromDataFolder();
        // var dataPermissionManager = new DataPermissionManager();
        // var dataManager = new DataManager(feed,
        //  new UniverseSelection(
        //      algorithm,
        //      new SecurityService(algorithm.Portfolio.CashBook, marketHoursDatabase, symbolPropertiesDataBase, algorithm, RegisteredSecurityDataTypesProvider.Null, new SecurityCacheProvider(algorithm.Portfolio)),
        //      dataPermissionManager,
        //      TestGlobals.DataProvider),
        // algorithm,
        // algorithm.TimeKeeper,
        // marketHoursDatabase,
        // false,   // _liveMode
        // RegisteredSecurityDataTypesProvider.Null,
        // dataPermissionManager);

        // algorithm.SubscriptionManager.SetDataManager(dataManager);

        // synchronizer.Initialize(algorithm, dataManager);

        // // Initialize the data feed before we initialize so he can intercept added securities/universes via events
        // AlgorithmHandlers.DataFeed.Initialize(
        //     algorithm,
        //     job,
        //     AlgorithmHandlers.Results,
        //     AlgorithmHandlers.MapFileProvider,
        //     AlgorithmHandlers.FactorFileProvider,
        //     AlgorithmHandlers.DataProvider,
        //     dataManager,
        //     (IDataFeedTimeProvider)synchronizer,
        //     AlgorithmHandlers.DataPermissionsManager.DataChannelProvider);

        // //Initialize the internal state of algorithm and job: executes the algorithm.Initialize() method.
        // initializeComplete = AlgorithmHandlers.Setup.Setup(new SetupHandlerParameters(dataManager.UniverseSelection, algorithm, brokerage, job, AlgorithmHandlers.Results,
        //     AlgorithmHandlers.Transactions, AlgorithmHandlers.RealTime, AlgorithmHandlers.ObjectStore, AlgorithmHandlers.DataCacheProvider, AlgorithmHandlers.MapFileProvider));

        // //Run Algorithm Job:
        // // -> Using this Data Feed,
        // // -> Send Orders to this TransactionHandler,
        // // -> Send Results to ResultHandler.
        // algorithmManager.Run(job, algorithm, synchronizer, AlgorithmHandlers.Transactions, AlgorithmHandlers.Results, AlgorithmHandlers.RealTime, SystemHandlers.LeanManager, AlgorithmHandlers.Alphas, isolator.CancellationToken);

        // From AlgorithmManagerTests.cs
        // algorithmManager.Run(job, algorithm, nullSynchronizer, transactions, results, realtime, leanManager, alphas, token);

        // >public class BasicTemplateFrameworkAlgorithm : QCAlgorithm, IRegressionAlgorithmDefinition

        // > "nextMarginCallTime = time + marginCallFrequency;"
        // It checks every 5 minutes if there was a Margin call. It might be useful for RealTime, but this slows down backtest a lot.
        // We have to develop our Own Simulator Engine, which is very quick. Don't use these kind of unnecessary overhead.
    }

    public static void ManyBacktestsParallelInMultipleThreads()
    {
        Task basicTempTask = Task.Run(() => // Task.Run() runs it immediately
        {
            Console.WriteLine("Backtest: BasicTemplateFrameworkAlgorithm");
            BacktestingResultHandler backtestResults = Backtester.BacktestInSeparateThreadWithTimeout("BasicTemplateFrameworkAlgorithm", string.Empty, null, @"{""ema-fast"":10,""ema-slow"":20}", SqResult.QcOriginal);
            Console.WriteLine($"BacktestResults.PV. startPV:{backtestResults.StartingPortfolioValue:N0}, endPV:{backtestResults.DailyPortfolioValue:N0} ({(backtestResults.DailyPortfolioValue / backtestResults.StartingPortfolioValue - 1) * 100:N2}%)");
        });

        Task spyTask = Task.Run(() => // Task.Run() runs it immediately
        {
            Console.WriteLine("Backtest: SqSPYMonFriAtMoc");
            BacktestingResultHandler backtestResults = Backtester.BacktestInSeparateThreadWithTimeout("SqSPYMonFriAtMoc", string.Empty, null, @"{""ema-fast"":10,""ema-slow"":20}", SqResult.SqSimple);
            Console.WriteLine($"BacktestResults.PV. startPV:{backtestResults.StartingPortfolioValue:N0}, endPV:{backtestResults.DailyPortfolioValue:N0} ({(backtestResults.DailyPortfolioValue / backtestResults.StartingPortfolioValue - 1) * 100:N2}%)");
        });

        // Task dualMomTask = Task.Run(() => // Task.Run() runs it immediately
        // {
        //     Console.WriteLine("Backtest: SqDualMomentum");
        //     BacktestingResultHandler backtestResults = Backtester.BacktestInSeparateThreadWithTimeout("SqDualMomentum", "startDate=2006-01-01&endDate=now&assets=VNQ,EEM,DBC,SPY,TLT,SHY&lookback=63&noETFs=3", @"{""ema-fast"":10,""ema-slow"":20}", SqResult.SqSimple);
        //     Console.WriteLine($"BacktestResults.PV. startPV:{backtestResults.StartingPortfolioValue:N0}, endPV:{backtestResults.DailyPortfolioValue:N0} ({(backtestResults.DailyPortfolioValue / backtestResults.StartingPortfolioValue - 1) * 100:N2}%)");
        // });

        // Task.WaitAll(new Task[] { basicTempTask, spyTask, dualMomTask });
        Task.WaitAll(new Task[] { basicTempTask, spyTask });
        Console.WriteLine("Backtest: ManyBacktestsParallelInMultipleThreads(). All threads terminated.");
    }
}