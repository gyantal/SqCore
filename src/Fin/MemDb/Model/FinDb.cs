using System;
using System.Threading;
using QuantConnect.Configuration;
using QuantConnect.Data;
using QuantConnect.Data.Auxiliary;
using QuantConnect.Interfaces;
using QuantConnect.Lean.Engine;
using QuantConnect.Lean.Engine.Alpha;
using QuantConnect.Lean.Engine.DataFeeds;
using QuantConnect.Lean.Engine.HistoricalData;
using QuantConnect.Lean.Engine.RealTime;
using QuantConnect.Lean.Engine.Results;
using QuantConnect.Lean.Engine.Server;
using QuantConnect.Lean.Engine.Setup;
using QuantConnect.Lean.Engine.TransactionHandlers;
using QuantConnect.Util;
using SqCommon;

namespace Fin.MemDb;

public partial class FinDb : IDisposable
{
    public static readonly FinDb gFinDb = new();   // Singleton pattern. The C# base class Lazy<T> is unnecessary overhead each time Instance => LazyComposer.Value; is accessed.

    // Try to have our own globals, which is easily accessible, rather than the Composer.Instance Globals. Much slower to access them.
    public LocalDiskMapFileProvider MapFileProvider { get; set; } = null!; // ignore warning CS8618 Non-nullable property X must contain a non-null value when exiting constructor.
    public SubscriptionDataReaderHistoryProvider HistoryProvider { get; set; } = null!;
    // public LeanEngineSystemHandlers EngineSystemHandlers { get; set; } = null!; // ignore warning CS8618 Non-nullable property X must contain a non-null value when exiting constructor.
    // public LeanEngineAlgorithmHandlers EngineAlgorithmHandlers { get; set; } = null!; // ignore warning CS8618 Non-nullable property X must contain a non-null value when exiting constructor.

    private bool m_isInitialized = false;
    private bool disposedValue;

    public void Init()
    {
        Utils.RunInNewThread(ignored => Init_WT()); // Better to do long consuming data preprocess in working thread than in the main thread, so other services can start to initialize in parallel
    }

    void Init_WT() // WT : WorkThread
    {
        Thread.CurrentThread.Name = "FinDb.Init_WT Thread";
        Console.WriteLine("*FinDb is not yet ready! ReloadDbData is in progress...");
        Console.WriteLine($"*FinDb, Env.CurrentDir: {Environment.CurrentDirectory}");
        Console.WriteLine($"*FinDb, AppDomain.BaseDir: {AppDomain.CurrentDomain.BaseDirectory}");
        DateTime startTime = DateTime.UtcNow;
        try
        {
            // Globals.DataFolder and Globals.CacheDataFolder (coming from fin.config<.dev>.json) are relative paths from Environment.CurrentDir.
            // CurrentDir (in VsCode running): /WebServer/SqCoreWeb/ (dir of the project file)
            // Globals.CacheDataFolder (in VsCode running): "../../Fin/Data/"
            // CurrentDir (on Linux): /home/sq-vnc-client/SQ/WebServer/SqCoreWeb/published/publish/ (dir of the App)
            // Globals.CacheDataFolder (on Linux): "../FinData/"
            if (OperatingSystem.IsWindows())
                Config.SetConfigurationFile("config.fin.dev.json"); // on Linux use the default "config.fin.json", on Windows developing environment use the dev version.

            // For more initialization search code as "LeanEngineAlgorithmHandlers.FromConfiguration(Composer.Instance, researchMode);"
            // e.g. for historical data probably: var dataFeedHandlerTypeName = Config.Get("data-feed-handler", "FileSystemDataFeed");

            // class SecurityIdentifier.GenerateEquity() uses MapFileProvider as Composer.Instance.GetExportedValueByTypeName(), which is the global MapFileProvider.
            // in the future we try to eliminate this slow Composer.Instance globals, but at the moment, maybe too many code parts in Backtesting uses it.
            // However, in our code, we should use FinDb globals, and not the Composer.Instance globals which we will delete later.
            var dataProviderTypeName = Config.Get("data-provider", "DefaultDataProvider");
            var dataProv = Composer.Instance.GetExportedValueByTypeName<IDataProvider>(dataProviderTypeName);

            var mapFileProviderTypeName = Config.Get("map-file-provider", "LocalDiskMapFileProvider");
            var mapProv = Composer.Instance.GetExportedValueByTypeName<IMapFileProvider>(mapFileProviderTypeName);
            mapProv.Initialize(dataProv);

            MapFileProvider = (LocalDiskMapFileProvider)mapProv;

            // At this stage, Symbol creation works
            // string tickerAsTradedToday = "SPY";
            // Symbol symbolSpy = Symbol.Create(tickerAsTradedToday, SecurityType.Equity, Market.USA);
            // Console.WriteLine($"QC: Test1: , currentTradedSymbol:{symbolSpy.Value}, Unique SecurityID {symbolSpy.ID}, firstDate (of traded, first date in map file): {symbolSpy.ID.Date}, firstTradedSymbol: {symbolSpy.ID.Symbol}");

            // Factor files contains dividends. (and maybe splits. Check it later)
            // Data/daily/*.zip files contain raw price TradeBar data, without dividends.
            var factorFileProviderTypeName = Config.Get("factor-file-provider", "LocalDiskFactorFileProvider");
            var factorFileProv = Composer.Instance.GetExportedValueByTypeName<IFactorFileProvider>(factorFileProviderTypeName);
            factorFileProv.Initialize(mapProv, dataProv);

            // var historyProviderName = Config.Get("history-provider", "SubscriptionDataReaderHistoryProvider");
            // var historyProvider = Composer.Instance.GetExportedValueByTypeName<IHistoryProvider>(historyProviderName);
            HistoryProvider = new SubscriptionDataReaderHistoryProvider();

            var zipCacheProv = new ZipDataCacheProvider(dataProv, isDataEphemeral: true, cacheTimer: 20);   // cacheTimer seconds is 10sec, overwrite to 20sec cache of data
            HistoryProvider.Initialize(new HistoryProviderInitializeParameters(
                null,
                null,
                dataProv,
                zipCacheProv,
                mapProv,
                factorFileProv,
                null,
                false,
                new DataPermissionManager()));

            m_isInitialized = true;
            Backtester.Init();

            ScheduleDailyCrawlerTask();
        }
        catch (System.Exception e)
        {
            Console.WriteLine($"!!! Critical: FinDb.Init_WT() exception. No FinDb. Only some console commands are available. Check Log.");
            Utils.Logger.Error(e, "Error in FinDb.Init_WT().");
        }
        Console.WriteLine($"*FinDb is initialized({m_isInitialized}) full-ready! in {(DateTime.UtcNow - startTime).TotalSeconds:0.000}sec");
    }

    public static void Exit()
    {
        Backtester.Exit();
        gFinDb.Dispose(disposing: true);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            if (disposing)
            {
                // TODO: dispose managed state (managed objects)
            }

            // TODO: free unmanaged resources (unmanaged objects) and override finalizer
            // TODO: set large fields to null
            disposedValue = true;
        }
    }

    // // TODO: override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
    // ~Gateway()
    // {
    //     // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
    //     Dispose(disposing: false);
    // }

    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}