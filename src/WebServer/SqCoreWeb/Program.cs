using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Fin.Base;
using Fin.BrokerCommon;
using Fin.MemDb;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using QuantConnect;
using QuantConnect.Data;
using QuantConnect.Data.Market;
using QuantConnect.Lean.Engine.Results;
using QuantConnect.Parameters;
using QuantConnect.Securities;
using QuantConnect.Util;
using SqCommon;
using SqCoreWeb.Controllers;
using YahooFinanceApi;

namespace SqCoreWeb;

public interface IWebAppGlobals
{
    DateTime WebAppStartTime { get; set; }
    IWebHostEnvironment? KestrelEnv { get; set; } // instead of the 3 member variables separately, store the container p_env
    Queue<HttpRequestLog> HttpRequestLogs { get; set; } // Fast Insert, limited size. Better that List
}

public class WebAppGlobals : IWebAppGlobals
{
    DateTime IWebAppGlobals.WebAppStartTime { get; set; } = DateTime.UtcNow;
    // KestrelEnv.ContentRootPath: "...\SqCore\src\WebServer\SqCoreWeb"
    // KestrelEnv.WebRootPath:   "...\SqCore\src\WebServer\SqCoreWeb\wwwroot"
    IWebHostEnvironment? IWebAppGlobals.KestrelEnv { get; set; }
    Queue<HttpRequestLog> IWebAppGlobals.HttpRequestLogs { get; set; } = new Queue<HttpRequestLog>();
}

public partial class Program
{
    const int cHeartbeatTimerFrequencyMinutes = 10;
    public static IWebAppGlobals WebAppGlobals { get; set; } = new WebAppGlobals();
    private static readonly NLog.Logger gLogger = NLog.LogManager.GetLogger("Program");   // the name of the logger will be not the "Namespace.Class", but whatever you prefer: "Program"

    static Timer? gHeartbeatTimer = null; // If timer object goes out of scope and gets erased by Garbage Collector after some time, which stops callbacks from firing. Save reference to it in a member of class.
    static long gNheartbeat = 0;

    public static void Main(string[] args) // entry point Main cannot be flagged as async, because at first await, Main thread would go back to Threadpool, but that terminates the Console app
    {
        string appName = System.Reflection.MethodBase.GetCurrentMethod()?.ReflectedType?.Namespace ?? "UnknownAppName";
        string systemEnvStr = $"(v1.0.15,{Utils.RuntimeConfig() /* Debug | Release */},CLR:{System.Environment.Version},{System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription},OS:{System.Environment.OSVersion},usr:{System.Environment.UserName},CPU:{System.Environment.ProcessorCount},ThId-{Environment.CurrentManagedThreadId})";

        ThreadPool.GetMinThreads(out int minWorkerTh, out int minIoThread);
        ThreadPool.GetMaxThreads(out int maxWorkerTh, out int maxIoThread);
        string threadEnvStr = $"ProcThreads#:{Process.GetCurrentProcess().Threads.Count}, ThreadPoolTh#:{ThreadPool.ThreadCount}, WorkerTh: [{minWorkerTh}...{maxWorkerTh}], IoTh: [{minIoThread}...{maxIoThread}]";

        Console.WriteLine($"Hi {appName}.{systemEnvStr}.{threadEnvStr}");
        gLogger.Info($"********** Main() START {systemEnvStr}.{threadEnvStr}");
        // Setting Console.Title
        // on Linux use it only in GUI mode. It works with graphical Xterm in VNC, but with 'screen' or with Putty it is buggy and after this, the next 200 characters are not written to console.
        // Future work if needed: bring a flag to use it in string[] args, but by default, don't set Title on Linux
        if (!OperatingSystem.IsLinux()) // https://stackoverflow.com/questions/47059468/get-or-set-the-console-title-in-linux-and-macosx-with-net-core
            Console.Title = $"{appName} v1.0.15"; // "SqCoreWeb v1.0.15"

        gHeartbeatTimer = new System.Threading.Timer(
            (e) => // Heartbeat log is useful to find out when VM was shut down, or when the App crashed
            {
                Utils.Logger.Info($"**g_nHeartbeat: {gNheartbeat} (at every {cHeartbeatTimerFrequencyMinutes} minutes)");
                gNheartbeat++;
            }, null, TimeSpan.FromMinutes(0.5), TimeSpan.FromMinutes(cHeartbeatTimerFrequencyMinutes));

        string sensitiveConfigFullPath = Utils.SensitiveConfigFolderPath() + $"SqCore.WebServer.{appName}.NoGitHub.json";
        string systemEnvStr2 = $"Current working directory of the app: '{Directory.GetCurrentDirectory()}',{Environment.NewLine}SensitiveConfigFullPath: '{sensitiveConfigFullPath}'";
        gLogger.Info(systemEnvStr2);

        var builder = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory()) // GetCurrentDirectory() is the folder of the '*.csproj'.
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true) // no need to copy appsettings.json to the sub-directory of the EXE.
            .AddJsonFile(sensitiveConfigFullPath, optional: true, reloadOnChange: true);
        // .AddUserSecrets<Program>()    // Used mostly in Development only, not in Production. Stored in a JSON configuration file in a system-protected user profile folder on the local machine. (e.g. user's %APPDATA%\Microsoft\UserSecrets\), the secret values aren't encrypted, but could be in the future.
        // do we need it?: No. Sensitive files are in separate folders, not up on GitHub. If server is not hacked, we don't care if somebody who runs the code can read the settings file. Also, scrambling secret file makes it more difficult to change it realtime.
        // .AddEnvironmentVariables();   // not needed in general. We dont' want to clutter op.sys. environment variables with app specific values.
        Utils.Configuration = builder.Build();
        Utils.MainThreadIsExiting = new ManualResetEventSlim(false);
        // HealthMonitorMessage.InitGlobals(ServerIp.HealthMonitorPublicIp, ServerIp.DefaultHealthMonitorServerPort);       // until HealthMonitor runs on the same Server, "localhost" is OK

        Email.SenderName = Utils.Configuration["Emails:HQServer"]!;
        Email.SenderPwd = Utils.Configuration["Emails:HQServerPwd"]!;
        PhoneCall.TwilioSid = Utils.Configuration["PhoneCall:TwilioSid"]!;
        PhoneCall.TwilioToken = Utils.Configuration["PhoneCall:TwilioToken"]!;
        PhoneCall.PhoneNumbers[Caller.Gyantal] = Utils.Configuration["PhoneCall:PhoneNumberGyantal"]!;
        PhoneCall.PhoneNumbers[Caller.Charmat0] = Utils.Configuration["PhoneCall:PhoneNumberCharmat0"]!;

        StrongAssert.G_strongAssertEvent += StrongAssertMessageSendingEventHandler;
        AppDomain.CurrentDomain.UnhandledException += new UnhandledExceptionEventHandler(AppDomain_BckgThrds_UnhandledException);
        TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException; // Occurs when a faulted task's unobserved exception is about to trigger exception which, by default, would terminate the process.

        try
        {
            // 1. PreInit services. They might add callbacks to MemDb's events.
            DashboardClient.PreInit();    // services add handlers to the MemDb.EvMemDbInitialized event.

            // 2. Init services
            BrokersWatcher.gWatcher.Init(); // Returns quickly, because Broker connections happen in a separate ThreadPool threads. FintechCommon's MemDb is built on BrokerCommon's BrokerWatcher. So, it makes sense to initialize Brokers asap. Before MemDb uses it for RtNavTimer_Elapsed.ownloadLastPriceNav() very early
            int redisDbIndex = 0;  // DB-0 is ProductionDB. DB-1+ can be used for Development when changing database schema, so the Production system can still work on the ProductionDB. DB-1: George, DB-2: Daya, DB-3: Balazs
            MemDb.gMemDb.Init(redisDbIndex, MemDbRunningEnvironment.SqCoreWebApp); // high level DB used by functionalities
            FinDb.gFinDb.Init();
            SqTaskScheduler.gTaskScheduler.Init();

            Services_Init();
            KestrelWebServer_Init();

            // 3. Run services.
            // Create a dedicated thread for a single task that is running for the lifetime of my application.
            KestrelWebServer_Run(args);

            string userInput = string.Empty;
            do
            {
                userInput = DisplayMenuAndExecute();  // we cannot 'await' it, because Main thread would terminate, which would close the whole Console app.
            }
            while (userInput != "UserChosenExit" && userInput != "ConsoleIsForcedToShutDown");
        }
        catch (Exception e)
        {
            gLogger.Error(e, $"CreateHostBuilder(args).Build().Run() exception.");
            if (e is System.Net.Sockets.SocketException)
            {
                gLogger.Error("SocketException (Permission denied)! Potential Error on Linux. Kestrel couldn't bind to port number. See 'Allow non-root process to bind to port under 1024.txt'. If Dotnet.exe was updated, it lost privilaged port. Try 'whereis dotnet','sudo setcap 'cap_net_bind_service=+ep' /usr/lib/dotnet/dotnet'.");
            }
            HealthMonitorMessage.SendAsync($"Exception in SqCoreWebsite.C#.MainThread. Exception: '{e.ToStringWithShortenedStackTrace(1600)}'", HealthMonitorMessageID.SqCoreWebCsError).TurnAsyncToSyncTask();
        }

        Utils.MainThreadIsExiting.Set(); // broadcast main thread shutdown
        gHeartbeatTimer.Dispose();

        // 4. Try to gracefully stop services.
        KestrelWebServer_Stop();

        int timeBeforeExitingSec = 2;
        Console.WriteLine($"Exiting in {timeBeforeExitingSec}sec...");
        Thread.Sleep(TimeSpan.FromSeconds(timeBeforeExitingSec)); // give some seconds for long running background threads to quit

        // 5. Dispose service resources
        KestrelWebServer_Exit();
        Services_Exit();

        SqTaskScheduler.Exit();
        FinDb.Exit();
        MemDb.Exit();
        BrokersWatcher.gWatcher.Exit();

        gLogger.Info("****** Main() END");
        NLog.LogManager.Shutdown();
    }

    static bool gIsMenuFirstCall = true;
    public static string DisplayMenuAndExecute()
    {
        if (!gIsMenuFirstCall)
            Console.WriteLine();
        gIsMenuFirstCall = false;

        ColorConsole.WriteLine(ConsoleColor.Magenta, "----  (type and press Enter)  ----");
        Console.WriteLine("1. Say Hello. Don't do anything. Check responsivenes.");
        Console.WriteLine("2. DB Admin (Redis, SQL) ...");
        Console.WriteLine("3. MemDb Admin (Reloads)...");
        Console.WriteLine("4. Backtester Admin...");
        Console.WriteLine("5. Show next schedule times (only earliest trigger)");
        Console.WriteLine("6. Elapse Task: Overmind, Trigger1-MorningCheck");
        Console.WriteLine("7. Elapse Task: Overmind, Trigger2-MiddayCheck");
        Console.WriteLine("8. Elapse Task: WebsitesMonitor, Crawl SpIndexChanges");
        Console.WriteLine("X. Elapse Task: VBroker-Sobek (First Simulation)");
        Console.WriteLine("X. Elapse Task: VBroker-UberVxx (First Simulation)");
        Console.WriteLine("9. Exit gracefully (Avoid Ctrl-^C).");
        string userInput;
        try
        {
            userInput = Console.ReadLine() ?? string.Empty;
        }
        catch (System.IO.IOException e) // on Linux, of somebody closes the Terminal Window, Console.Readline() will throw an Exception with Message "Input/output error"
        {
            gLogger.Info($"Console.ReadLine() exception. Somebody closes the Terminal Window: {e.Message}");
            return "ConsoleIsForcedToShutDown";
        }

        string userInputSub;
        switch (userInput)
        {
            case "1":
                Console.WriteLine("Hello. I am not crashed yet! :)");
                gLogger.Info("Hello. I am not crashed yet! :)");
                break;
            case "2":
                do
                {
                    userInputSub = DisplaySubMenuAndExecute_DbAdmin();
                }
                while (userInputSub != "UserChosenExit" && userInputSub != "ConsoleIsForcedToShutDown");
                break;
            case "3":
                do
                {
                    userInputSub = DisplaySubMenuAndExecute_MemDbAdmin();
                }
                while (userInputSub != "UserChosenExit" && userInputSub != "ConsoleIsForcedToShutDown");
                break;
            case "4":
                do
                {
                    userInputSub = DisplaySubMenuAndExecute_BacktesterAdmin();
                }
                while (userInputSub != "UserChosenExit" && userInputSub != "ConsoleIsForcedToShutDown");
                break;
            case "5":
                Console.WriteLine(SqTaskScheduler.PrintNextScheduleTimes(false).ToString());
                break;
            case "6":
                SqTaskScheduler.TestElapseTrigger("Overmind", 0);
                break;
            case "7":
                SqTaskScheduler.TestElapseTrigger("Overmind", 1);
                break;
            case "8":
                SqTaskScheduler.TestElapseTrigger("WebsitesMonitor", 0);
                break;

            case "9":
                return "UserChosenExit";
        }
        return string.Empty;
    }

    public static string DisplaySubMenuAndExecute_DbAdmin()
    {
        ColorConsole.WriteLine(ConsoleColor.Magenta, "---- DbAdmin !!!  ----");
        Console.WriteLine("1. RedisDb: Ping");
        Console.WriteLine("2. RedisDb: Get Active DB index (Production: DB-0)");
        Console.WriteLine("3. RedisDb: Mirror DB-i to DB-j");
        Console.WriteLine("4. RedisDb: Upsert gSheet Assets to DB-?.(!!! See steps as comments in Controller.cs))");
        Console.WriteLine("5. MemDb: Reload data from RedisDb (DB-ActiveIndex) to MemDb");
        Console.WriteLine("6. LegacyDb: Test connection");
        Console.WriteLine("7. LegacyDb: Get example trades");
        Console.WriteLine("8. LegacyDb: Insert example trades");
        Console.WriteLine("9. Exit to main menu.");
        string userInput;
        try
        {
            userInput = Console.ReadLine() ?? string.Empty;
        }
        catch (System.IO.IOException e) // on Linux, of somebody closes the Terminal Window, Console.Readline() will throw an Exception with Message "Input/output error"
        {
            gLogger.Info($"Console.ReadLine() exception. Somebody closes the Terminal Window: {e.Message}");
            return "ConsoleIsForcedToShutDown";
        }

        switch (userInput)
        {
            case "1":
                Console.WriteLine(MemDb.gMemDb.TestRedisExecutePing());
                break;
            case "2":
                Console.WriteLine($"MemDb.gMemDb.RedisDbInd: {MemDb.gMemDb.RedisDbIdx}. Active database is db{MemDb.gMemDb.RedisDbIdx}");
                break;
            case "3":
                Controller.RedisMirrorDb();
                break;
            case "4":
                Controller.UpsertgSheetAssets();
                break;
            case "5":
                MemDb.gMemDb.ReloadDbDataIfChangedImpl().TurnAsyncToSyncTask();
                break;
            case "6":
                MemDb.gMemDb.TestLegacyDbConnection();
                break;
            case "7":
                List<Trade>? trades = MemDb.gMemDb.GetLegacyPortfolioTradeHistoryToList("! CXO Combined Value-Momentum 2021 Live");
                Console.WriteLine($"Number of trades in the '! CXO Combined Value-Momentum 2021 Live' portfolio: {trades?.Count.ToString() ?? "N/A"}");
                break;
            case "8":
                // Create new trade objects with example data
                Trade newTrade1 = new()
                {
                    Time = DateTime.Now.AddDays(-2),
                    Action = TradeAction.Buy,
                    Symbol = "TSLA",
                    Quantity = 10,
                    Price = 420.0f,
                    Note = "Test trade insert"
                };

                Trade newTrade2 = new()
                {
                    Time = DateTime.Now.AddDays(-1),
                    Action = TradeAction.Buy,
                    Symbol = "NVDA",
                    Quantity = 20,
                    Price = 123.0f,
                };

                List<Trade> newTrades = new() { newTrade1, newTrade2 };

                // Call the InsertTrade method to insert the new trades
                foreach (Trade trade in newTrades)
                {
                    bool isInserted = MemDb.gMemDb.InsertLegacyPortfolioTrade("Balazs Earnings Live", trade);
                    Console.WriteLine(isInserted ? $"{trade.Symbol} trade inserted successfully." : $"Failed to insert {trade.Symbol} trade.");
                }

                break;
            case "9":
                return "UserChosenExit";
        }
        return string.Empty;
    }

    public static string DisplaySubMenuAndExecute_MemDbAdmin()
    {
        ColorConsole.WriteLine(ConsoleColor.Magenta, "---- MemDbAdmin !!!  ----");
        Console.WriteLine("1. MemDb: Force Reload only HistData And SetNewTimer");
        Console.WriteLine("2. MemDb: Reload All DbData Only If Changed");
        Console.WriteLine("3. YF: Test getting SPY history");
        Console.WriteLine("4. YF: Test getting SPY realtime");
        Console.WriteLine("50. FinDb: Force daily YF PriceHistory Crawler for securities having MAP file");
        Console.WriteLine("51. FinDb: Test getting FundamentalData from Fundamental files");
        Console.WriteLine("52. FinDb: Creating MAP files semi-automatically");
        Console.WriteLine("6. MemDb: Test getting PortfolioTradeHistory from RedisDb");
        Console.WriteLine("7. MemDb: Test append-writing PortfolioTradeHistory to RedisDb");
        Console.WriteLine("8. MemDb: Test delete PortfolioTradeHistory from RedisDb");
        Console.WriteLine("9. Exit to main menu.");
        string userInput;
        try
        {
            userInput = Console.ReadLine() ?? string.Empty;
        }
        catch (System.IO.IOException e) // on Linux, of somebody closes the Terminal Window, Console.Readline() will throw an Exception with Message "Input/output error"
        {
            gLogger.Info($"Console.ReadLine() exception. Somebody closes the Terminal Window: {e.Message}");
            return "ConsoleIsForcedToShutDown";
        }

        switch (userInput)
        {
            case "1":
                Console.WriteLine(MemDb.gMemDb.ForceReloadHistData(false).TurnAsyncToSyncTask().ToString());
                break;
            case "2":
                Console.WriteLine(MemDb.gMemDb.ReloadDbDataIfChanged(false).TurnAsyncToSyncTask().ToString());
                break;
            case "3":
                // Stopwatch stopwatch = Stopwatch.StartNew();
                // IReadOnlyList<Candle?> history = Yahoo.GetHistoricalAsync("QQQ").TurnAsyncToSyncTask();
                // stopwatch.Stop();
                // Console.WriteLine($"Yahoo.GetHistoricalAsync: {(long)((stopwatch.ElapsedTicks * 1_000_000) / Stopwatch.Frequency):N2} microsec");
                // Candle? lastCandle = history[^1];
                // if (lastCandle != null)
                //     Console.WriteLine($"QQQ History length: {history.Count}. LastCandle: {lastCandle.DateTime}: {lastCandle.Close}");
                // else
                //     Console.WriteLine($"QQQ History is not received.");

                var histResult = HistPrice.g_HistPrice.GetHistAsync("QQQ", HpDataNeed.AdjClose | HpDataNeed.Split | HpDataNeed.Dividend | HpDataNeed.OHLCV).TurnAsyncToSyncTask();
                // var histResult = HistPrice.g_HistPrice.GetHistAsync("QQQ", DataNeed.AdjClose).TurnAsyncToSyncTask(); // returns all 9 output arrays
                // var histResult = HistPrice.g_HistPrice.GetHistAdjCloseAsync("QQQ").TurnAsyncToSyncTask(); // returns only the required 2 output arrays: Dates, AdjCloses
                if (histResult.ErrorStr != null)
                    Utils.Logger.Error($"HistPrice Error: {histResult.ErrorStr}");
                else
                    Console.WriteLine($"QQQ History length: {histResult.AdjCloses!.Length}. Last: {histResult.Dates![^1]}: {histResult.AdjCloses![^1]}");
                break;
            case "4":
                try
                {
                    IReadOnlyDictionary<string, YahooFinanceApi.Security> quotes = Yahoo.Symbols(new string[] { "SPY" }).Fields(new Field[] { Field.Symbol, Field.RegularMarketPreviousClose, Field.RegularMarketPrice, Field.MarketState, Field.PostMarketPrice, Field.PreMarketPrice, Field.PreMarketChange }).QueryAsync().TurnAsyncToSyncTask(); // takes 45 ms from WinPC (30 tickers)
                    YahooFinanceApi.Security quote = quotes["SPY"];
                    Console.WriteLine($"SPY RegularMarketPreviousClose: {quote.RegularMarketPreviousClose}");
                }
                catch (System.Exception e)
                {
                    Console.WriteLine($"Exception: {e.Message}");
                }
                break;
            case "50":
                SqTaskScheduler.TestElapseTrigger("FinDbDailyCrawler", 0);
                // Console.WriteLine(FinDb.CrawlData(false).TurnAsyncToSyncTask().ToString());
                break;
            case "51":
                try
                {
                    List<string> tickers = new() { "AAPL", "AMZN", "MSFT", "TSLA", "GOOGL", "DE", "SPY", "SVXY", "META", "HIMS" };
                    DateTime date = DateTime.Now;
                    List<FundamentalProperty> propertyNames = new() { FundamentalProperty.CompanyReference_ShortName, FundamentalProperty.CompanyReference_StandardName, FundamentalProperty.CompanyProfile_SharesOutstanding, FundamentalProperty.CompanyProfile_MarketCap };

                    Dictionary<string, Dictionary<FundamentalProperty, object>> fundamentals = new();
                    Utils.BenchmarkElapsedTime("GetFundamentalData()", () =>
                    {
                        fundamentals = FinDb.GetFundamentalData(tickers, date, propertyNames);
                    });
                    Console.WriteLine($"Ready. Example data. Meta companyName: '{fundamentals["META"][FundamentalProperty.CompanyReference_ShortName].ToString()}'");
                }
                catch (System.Exception e)
                {
                    Console.WriteLine($"Exception: {e.Message}");
                }
                break;
            case "52":
                try
                {
                    string[] qcOutput = { "AAPL, 19980102, Q", "TSLA, 20100629, Q", "SPY, 19980102, P", "STZ, 19980102, STZ:19980102, CBRNA:19991012, CDB:20000920, N", "HON, 19980102, HON:19980102, ALD:19991202, N" };
                    FinDb.CreateTickers(qcOutput);

                    Console.WriteLine($"Ready. Map files were successfully created.");
                }
                catch (System.Exception e)
                {
                    Console.WriteLine($"Exception: {e.Message}");
                }
                break;
            case "6":
                try
                {
                    List<Trade>? portTradeHist = new();
                    Utils.BenchmarkElapsedTime("GetPortfolioTradeHistory()", () =>
                    {
                        portTradeHist = MemDb.gMemDb.GetPortfolioTradeHistoryToList(1, null, null); // TradeHistory = 1 is used by Portfolio 21 : "TradePortfolio test 1"
                    });

                    Console.WriteLine($"portTradeHist.Count: {(portTradeHist == null ? 0 : portTradeHist.Count)}");
                }
                catch (System.Exception e)
                {
                    Console.WriteLine($"Exception: {e.Message}");
                }
                break;
            case "7":
                try
                {
                    List<Trade> testTrades = new();
                    Trade trade1 = new Trade(testTrades) { AssetType = AssetType.Stock, Action = TradeAction.Buy, Symbol = "TSLA", Price = 123, Quantity = 65, Commission = 1.2f, Time = DateTime.Now.AddDays(-1) };
                    testTrades.Add(trade1);
                    Trade trade2 = new Trade(testTrades) { AssetType = AssetType.Option, Action = TradeAction.Exercise, Symbol = "AMD 1234C0123", UnderlyingSymbol = "AMD", Quantity = 1, Time = DateTime.Now.AddDays(-0.1), ConnectedTrades = new List<int> { 2 } };
                    testTrades.Add(trade2);
                    Trade trade3 = new Trade(testTrades) { AssetType = AssetType.Stock, Action = TradeAction.Buy, Symbol = "AMD", Price = 54, Quantity = 100, Time = DateTime.Now.AddDays(-1.1) };
                    testTrades.Add(trade3);

                    Utils.BenchmarkElapsedTime("WritePortfolioTradeHistory()", () =>
                    {
                        MemDb.gMemDb.AppendPortfolioTradeHistory(0, testTrades, true); // be careful not to overwrite valid tradeHistoryId that is used by any portfolio. Use Id = 0 for debugging. It is not used by any portfolio.
                    });
                    Console.WriteLine($"WritePortfolioTradeHistory(): OK.");
                }
                catch (System.Exception e)
                {
                    Console.WriteLine($"Exception: {e.Message}");
                }
                break;
            case "8":
                try
                {
                    MemDb.gMemDb.DeletePortfolioTradeHistory(0);
                    // int id = MemDb.gMemDb.InsertPortfolioTradeHistory(new List<Trade>());
                }
                catch (System.Exception e)
                {
                    Console.WriteLine($"Exception: {e.Message}");
                }
                break;
            case "9":
                return "UserChosenExit";
        }
        return string.Empty;
    }

    public static string DisplaySubMenuAndExecute_BacktesterAdmin()
    {
        ColorConsole.WriteLine(ConsoleColor.Magenta, "---- QcAdmin !!!  ----");
        Console.WriteLine("1. Test Symbol/SecurityID creations");
        Console.WriteLine("2. Test Historical price data");
        Console.WriteLine("3. Test BacktestInSeparateThreadWithTimeout()");
        Console.WriteLine("4. Test ManyBacktestsParallelInMultipleThreads()");
        Console.WriteLine("5. PercentileChannel calculation");
        Console.WriteLine("6. PercentileChannel calculation with dates");
        Console.WriteLine("9. Exit to main menu.");
        string userInput;
        try
        {
            userInput = Console.ReadLine() ?? string.Empty;
        }
        catch (System.IO.IOException e) // on Linux, of somebody closes the Terminal Window, Console.Readline() will throw an Exception with Message "Input/output error"
        {
            gLogger.Info($"Console.ReadLine() exception. Somebody closes the Terminal Window: {e.Message}");
            return "ConsoleIsForcedToShutDown";
        }

        SqBacktestConfig? backtestConfig = null;
        BacktestingResultHandler? backtestResults = null;
        switch (userInput)
        {
            case "1":
                // Symbol cashUsd1 = new Symbol(SecurityIdentifier.GenerateBase(null, "CASH", Market.USA), "CASH");
                // Symbol cashUsd2 = Symbol.Create("CASH", SecurityType.Base, Market.USA); // same, but shorter. It will use sid = SecurityIdentifier.GenerateBase(baseDataType, ticker, market);

                // Version1: Symbol.Create() uses MapFileProvider as Composer.Instance.GetExportedValueByTypeName(), which is the global MapFileProvider.
                string tickerAsTradedTodayVxx = "VXX";
                Symbol symbolVxx = Symbol.Create(tickerAsTradedTodayVxx, SecurityType.Equity, Market.USA);
                Console.WriteLine($"QC: Test1: currentTradedSymbol:{symbolVxx.Value}, Unique SecurityID {symbolVxx.ID}, firstDate (of traded, first date in map file): {symbolVxx.ID.Date}, firstTradedSymbol: {symbolVxx.ID.Symbol}");

                // Version2: SecurityIdentifier.GenerateEquity() uses our global MapFileProvider, without the Composer.Instance slow functionality.
                string tickerAsTradedToday = "SPY";
                SecurityIdentifier sidSpy = SecurityIdentifier.GenerateEquity(tickerAsTradedToday, Market.USA, true, FinDb.gFinDb.MapFileProvider);
                var symbolSpy = new Symbol(sidSpy, tickerAsTradedToday);
                Console.WriteLine($"QC: Test2: currentTradedSymbol:{symbolSpy.Value}, Unique SecurityID {symbolSpy.ID}, firstDate (of traded, first date in map file): {symbolSpy.ID.Date}, firstTradedSymbol: {symbolSpy.ID.Symbol}");
                break;
            case "2":
                string tickerAsTradedToday2 = "SPY"; // if symbol.zip doesn't exist in Data folder, it will not download it (cost money, you have to download in their shop). It raises an exception.
                Symbol symbol = new(SecurityIdentifier.GenerateEquity(tickerAsTradedToday2, Market.USA, true, FinDb.gFinDb.MapFileProvider), tickerAsTradedToday2);

                DateTime startTimeUtc = new(2008, 01, 01);
                // If you want to get 20080104 day data too, it has to be specified like this:
                // class TimeBasedFilter assures that (data.EndTime <= EndTimeLocal)
                // It is assumed that any TradeBar final values are only released at TradeBar.EndTime (OK for minute, hourly data, but not perfect for daily data which is known at 16:00)
                // Any TradeBar's EndTime is Time+1day (assuming that ClosePrice is released not at 16:00, but later, at midnight)
                // So the 20080104's row in CVS is: Time: 20080104:00:00, EndTime:20080105:00:00
                DateTime endTimeUtc = new(2025, 01, 25, 23, 59, 0); // this will be => 2025-01-25:18:59 endTimeLocal

                // Use TickType.TradeBar. That is in the daily CSV file. TickType.Quote file would contains Ask(Open/High/Low/Close) + Bid(Open/High/Low/Close), like a Quote from a Broker at trading realtime.
                var historyRequests = new[]
                {
                    new HistoryRequest(startTimeUtc, endTimeUtc, typeof(TradeBar), symbol, Resolution.Daily, SecurityExchangeHours.AlwaysOpen(TimeZones.NewYork),
                        // TimeZones.NewYork, null, false, false, DataNormalizationMode.Raw, QuantConnect.TickType.Trade)
                        TimeZones.NewYork, null, false, false, DataNormalizationMode.Adjusted, QuantConnect.TickType.Trade)
                };

                NodaTime.DateTimeZone sliceTimeZone = TimeZones.NewYork; // "algorithm.TimeZone"

                var result = FinDb.gFinDb.HistoryProvider.GetHistory(historyRequests, sliceTimeZone).ToList(); // see comment in FinDb.HistoryProvider
                Console.WriteLine($" Test Historical price data. Number of TradeBars: {result.Count}. SPY RAW/Adjusted ClosePrice on {result[^1].Bars.Values.ToArray()[0].Time}-{result[^1].Bars.Values.ToArray()[0].EndTime}: {result[^1].Bars.Values.ToArray()[0].Close}");
                break;
            case "3":
                Console.WriteLine("Backtest: SqSPYMonFriAtMoc");
                // backtestConfig = new SqBacktestConfig() { SqResultStat = SqResultStat.QcOriginalStat };
                // backtestResults = Backtester.BacktestInSeparateThreadWithTimeout("BasicTemplateFrameworkAlgorithm", string.Empty, null, @"{""ema-fast"":10,""ema-slow"":20}", backtestConfig); // ! For QC strategies, use the SqResult.QcOriginal backtestConfig

                backtestConfig = new SqBacktestConfig() { SqResultStat = SqResultStat.SqSimpleStat };
                // backtestResults = Backtester.BacktestInSeparateThreadWithTimeout("SqSPYMonFriAtMoc", string.Empty, null, @"{""ema-fast"":10,""ema-slow"":20}", backtestConfig);
                // backtestResults = Backtester.BacktestInSeparateThreadWithTimeout("SqDualMomentum", "startDate=2006-01-01&endDate=now&assets=VNQ,EEM,DBC,SPY,TLT,SHY&lookback=63&noETFs=3", null, @"{""ema-fast"":10,""ema-slow"":20}", backtestConfig);
                // backtestResults = Backtester.BacktestInSeparateThreadWithTimeout("SqFundamentalDataFiltered", string.Empty, null, @"{""ema-fast"":10,""ema-slow"":20}", backtestConfig);
                // backtestResults = Backtester.BacktestInSeparateThreadWithTimeout("SqPctAllocation", "assets=SVXY-SQ,VXX-SQ,VXZ-SQ,TQQQ-SQ,TLT,USO,UNG&weights=15,-5,10,25,255,-27,-78&rebFreq=Daily,1d", null, @"{""ema-fast"":10,""ema-slow"":20}", backtestConfig);
                // backtestResults = Backtester.BacktestInSeparateThreadWithTimeout("SqPctAllocation", "startDate=2002-07-29&endDate=now&startDateAutoCalcMode=WhenFirstTickerAlive&assets=SPY,TLT&weights=60,40&rebFreq=Daily,30d",  MemDb.gMemDb.GetPortfolioTradeHistoryToList(1, null, null), @"{""ema-fast"":10,""ema-slow"":20}", backtestConfig); // testing sending TradeHist input
                // backtestResults = Backtester.BacktestInSeparateThreadWithTimeout("SqTradeAccumulation", string.Empty,
                //    [new Trade() { Id = 0, Action = TradeAction.Deposit, Symbol = "USD", Price = 100000, Quantity = 1, Time = new DateTime(2022, 09, 01) }, new Trade() { Id = 1, AssetType = AssetType.Stock, Action = TradeAction.Buy, Symbol = "TSLA", Price = 100, Quantity = 87, Time = new DateTime(2022, 09, 01) }, new Trade() { Id = 2, AssetType = AssetType.Stock, Action = TradeAction.Sell, Symbol = "TSLA", Price = 200, Quantity = 87, Time = new DateTime(2023, 08, 31) }], @"{""ema-fast"":10,""ema-slow"":20}", backtestConfig); // testing sending TradeHist input
                // backtestResults = Backtester.BacktestInSeparateThreadWithTimeout("SqTradeAccumulation", string.Empty, MemDb.gMemDb.GetPortfolioTradeHistoryToList(2, null, null), @"{""ema-fast"":10,""ema-slow"":20}", backtestConfig); // testing sending TradeHist input
                // backtestResults = Backtester.BacktestInSeparateThreadWithTimeout("SqTradeAccumulation", string.Empty, new LegacyPortfolio { LegacyDbPortfName = "! CXO Combined Value-Momentum 2021 Live" }.GetTradeHistory(), @"{""ema-fast"":10,""ema-slow"":20}", backtestConfig); // testing sending TradeHist input
                // backtestResults = Backtester.BacktestInSeparateThreadWithTimeout("SqTradeAccumulation", string.Empty, new LegacyPortfolio { LegacyDbPortfName = "! Harry Long2(Contango-Bond) harvester Live" }.GetTradeHistory(), @"{""ema-fast"":10,""ema-slow"":20}", backtestConfig); // testing sending TradeHist input
                // backtestResults = Backtester.BacktestInSeparateThreadWithTimeout("SqTradeAccumulation", string.Empty, new LegacyPortfolio { LegacyDbPortfName = "!IB-V Sobek-HL(Contango-Bond) harvester Agy Live" }.GetTradeHistory(), @"{""ema-fast"":10,""ema-slow"":20}", backtestConfig); // testing sending TradeHist input
                // backtestResults = Backtester.BacktestInSeparateThreadWithTimeout("SqCxoMomentum", "startDate=2009-01-05&endDate=now&assets=SPY,DBC,EMB,EFA,GLD,IWM,TLT,VNQ&lookbackMonth=4&noETFs=2", null, @"{""ema-fast"":10,""ema-slow"":20}", backtestConfig);
                // backtestResults = Backtester.BacktestInSeparateThreadWithTimeout("SqCxoValue", "startDate=2009-01-05&endDate=now", null, @"{""ema-fast"":10,""ema-slow"":20}", backtestConfig);
                // backtestResults = Backtester.BacktestInSeparateThreadWithTimeout("SqCxoCombined", "startDate=2009-01-05&endDate=now&assets=SPY,DBC,EMB,EFA,GLD,IWM,TLT,VNQ&lookbackMonth=4&noETFs=2&subStratWeights=50,50", null, @"{""ema-fast"":10,""ema-slow"":20}", backtestConfig);
                backtestResults = Backtester.BacktestInSeparateThreadWithTimeout("SqPctAllocation", "startDate=2002-07-29&endDate=now&startDateAutoCalcMode=WhenFirstTickerAlive&assets=SPY,TLT&weights=60,40&rebFreq=Daily,30d", null, @"{""ema-fast"":10,""ema-slow"":20}", backtestConfig);
                break;
            case "4":
                try
                {
                    Backtester.ManyBacktestsParallelInMultipleThreads();
                }
                catch (System.Exception e)
                {
                    Console.WriteLine($"Exception message: {e.Message}");
                }
                break;
            case "5":
                try
                {
                    string ticker = "AAPL";
                    DateTime endDate = DateTime.UtcNow;
                    DateTime startDate = endDate.AddDays(-600);

                    // Fetch historical data using the custom historical data source with the AdjClose flag only
                    var histResult = HistPrice.g_HistPrice.GetHistAsync(ticker, HpDataNeed.AdjClose, startDate, endDate).Result;

                    // Check if there was an error in fetching data or if the necessary data is missing
                    if (histResult.ErrorStr != null || histResult.AdjCloses == null)
                    {
                        Console.WriteLine($"Error fetching adjusted close prices for '{ticker}': {histResult.ErrorStr ?? "Adjusted close data is null."}");
                        return string.Empty;
                    }

                    // Populate the adjustedClosePrices list
                    List<float> adjustedClosePrices = histResult.AdjCloses.Select(adjClose => (float)adjClose).ToList();

                    int bottomPctThreshold = 25;
                    int topPctThreshold = 75;
                    int[] pctChnLookbackDays = new int[] { 60, 120, 180, 252 };
                    int calculationLookbackDays = 50;
                    int resultLengthDays = 20;
                    List<AggregatePctlChannel> pctChannelRes = Controllers.StrategyUberTaaController.PctChnWeights(adjustedClosePrices, pctChnLookbackDays, calculationLookbackDays, resultLengthDays, bottomPctThreshold, topPctThreshold);
                    Console.WriteLine($"Current weight of {ticker}: {pctChannelRes[^1].Aggregate}");
                }
                catch (System.Exception e)
                {
                    Console.WriteLine($"Exception message: {e.Message}");
                }
                break;
            case "6":
                try
                {
                    string ticker = "AAPL";
                    DateTime endDate = DateTime.UtcNow;
                    int bottomPctThreshold = 25;
                    int topPctThreshold = 75;
                    int[] pctChnLookbackDays = new int[] { 60, 120, 180, 252 };
                    int calculationLookbackDays = 50;
                    int resultLengthDays = 20;
                    List<AggregateDatePctlChannel> pctChannelRes = Controllers.StrategyUberTaaController.PctChnWeightsWithDates(ticker, endDate, pctChnLookbackDays, calculationLookbackDays, resultLengthDays, bottomPctThreshold, topPctThreshold);
                    Console.WriteLine($"Current weight of {ticker}: {pctChannelRes[^1].Date: yyyy-MM-dd}: {pctChannelRes[^1].Aggregate}");
                }
                catch (System.Exception e)
                {
                    Console.WriteLine($"Exception message: {e.Message}");
                }
                break;
            case "9":
                return "UserChosenExit";
        }
        if (backtestResults != null)
        {
            Console.WriteLine("BacktestResults.LogStore (from Algorithm)"); // we can force the Trade Logs into a text file. ("SaveListOfTrades(AlgorithmHandlers.Transactions, csvTransactionsFileName);"). But our Algo also can put it into the LogStore
            backtestResults.LogStore.ForEach(r => Console.WriteLine(r.Message)); // Trade Logs. "Time: 10/07/2013 13:31:00 OrderID: 1 EventID: 2 Symbol: SPY Status: Filled Quantity: 688 FillQuantity: 688 FillPrice: 144.7817 USD OrderFee: 3.44 USD"

            Console.WriteLine($"BacktestResults. startPV:{backtestResults.StartingPortfolioValue:N0}, endPV:{backtestResults.DailyPortfolioValue:N0}");

            if (backtestConfig!.SamplingQcOriginal)
            {
                var equityChart = backtestResults.Charts["Strategy Equity"].Series["Equity"].Values;
                Console.WriteLine($"#QcEqu:{backtestResults.Charts["Strategy Equity"].Series.Count}. The QC Equity (Raw PV) chart: {equityChart[0].y:N0}, {equityChart[1].y:N0} ... {equityChart[^2].y:N0}, {equityChart[^1].y:N0}");
            }

            if (backtestConfig!.SamplingSqDailyRawPv)
                Console.WriteLine($"#RawPv:{backtestResults.SqSampledLists["rawPV"].Count}. Raw PV: {backtestResults.SqSampledLists["rawPV"][0].Value:N0}, {backtestResults.SqSampledLists["rawPV"][1].Value:N0} ... {backtestResults.SqSampledLists["rawPV"][^2].Value:N0}, {backtestResults.SqSampledLists["rawPV"][^1].Value:N0}");

            if (backtestConfig!.SamplingSqDailyTwrPv)
                Console.WriteLine($"#TwrPv:{backtestResults.SqSampledLists["twrPV"].Count}. TWR PV: {backtestResults.SqSampledLists["twrPV"][0].Value:N2}, {backtestResults.SqSampledLists["twrPV"][1].Value:N2} ... {backtestResults.SqSampledLists["twrPV"][^2].Value:N2}, {backtestResults.SqSampledLists["twrPV"][^1].Value:N2}");

            Dictionary<string, string> finalStat = backtestResults.FinalStatistics;
            var statisticsStr = $"{Environment.NewLine}" + $"{string.Join(Environment.NewLine, finalStat.Select(x => $"STATISTICS:: {x.Key} {x.Value}"))}";
            Console.WriteLine(statisticsStr);
        }
        return string.Empty;
    }

    internal static void StrongAssertMessageSendingEventHandler(StrongAssertMessage p_msg)
    {
        gLogger.Info("StrongAssertEmailSendingEventHandler()");
        HealthMonitorMessage.SendAsync($"Msg from SqCore.Website.C#.StrongAssert. StrongAssert Warning (if Severity is NoException, it is just a mild Warning. If Severity is ThrowException, that exception triggers a separate message to HealthMonitor as an Error). Severity: {p_msg.Severity}, Message: {p_msg.Message}, StackTrace: {p_msg.StackTrace.ToStringWithShortenedStackTrace(1600)}", HealthMonitorMessageID.SqCoreWebCsError).TurnAsyncToSyncTask();
    }

    private static void AppDomain_BckgThrds_UnhandledException(object p_sender, UnhandledExceptionEventArgs p_e)
    {
        Exception exception = (p_e.ExceptionObject as Exception) ?? new SqException($"Unhandled exception doesn't derive from System.Exception: {p_e.ToString() ?? "Null ExceptionObject"}");
        Utils.Logger.Error(exception, $"AppDomain_BckgThrds_UnhandledException(). Terminating '{p_e?.IsTerminating.ToString() ?? "Null ExceptionObject"}'. Exception: '{exception.ToStringWithShortenedStackTrace(2000)}'");

        // isSendable check is not required. This background thread crash will terminate the main app. We should surely notify HealthMonitor.
        string msg = $"App 'SqCore.Website' is terminated because exception in background thread. C#.AppDomain_BckgThrds_UnhandledException(). See log files.";
        HealthMonitorMessage.SendAsync(msg, HealthMonitorMessageID.SqCoreWebCsError).TurnAsyncToSyncTask();
    }

    // Called by the GC.FinalizerThread. Occurs when a faulted task's unobserved exception is about to trigger exception which, by default, would terminate the process.
    private static void TaskScheduler_UnobservedTaskException(object? p_sender, UnobservedTaskExceptionEventArgs p_e)
    {
        gLogger.Error(p_e.Exception, $"TaskScheduler_UnobservedTaskException()");

        string msg = $"Exception in SqCore.WebServer.SqCoreWeb.C#.TaskScheduler_UnobservedTaskException. Exception: '{p_e.Exception.ToStringWithShortenedStackTrace(1600)}'. ";
        Console.WriteLine((p_e.Exception as AggregateException)?.InnerException?.Message ?? "cannot get data from InnerException");
        if (p_e.Exception is AggregateException aggrEx && aggrEx.InnerException is System.Net.Sockets.SocketException)
        {
            string msgConsole = "SocketException! Potential Error on Linux. Kestrel couldn't bind to port number. See 'Allow non-root process to bind to port under 1024.txt'. If Dotnet.exe was updated, it lost privilaged port. Try 'whereis dotnet','sudo setcap 'cap_net_bind_service=+ep' /usr/lib/dotnet/dotnet'.";
            Console.WriteLine(msgConsole);
            msg = msgConsole + msg;
        }

        msg += Utils.TaskScheduler_UnobservedTaskExceptionMsg(p_sender, p_e);
        gLogger.Warn(msg);
        p_e.SetObserved();        // preventing it from triggering exception escalation policy which, by default, terminates the process.

        bool isSendable = SqFirewallMiddlewarePreAuthLogger.IsSendableToHealthMonitorForEmailing(p_e.Exception);
        if (isSendable)
            HealthMonitorMessage.SendAsync(msg, HealthMonitorMessageID.SqCoreWebCsError).TurnAsyncToSyncTask();
    }

    public static void ServerDiagnostic(StringBuilder p_sb)
    {
        p_sb.Append("<H2>Program.exe</H2>");
        var timeSinceAppStart = DateTime.UtcNow - WebAppGlobals.WebAppStartTime;
        p_sb.Append($"WebAppStartTimeUtc: {WebAppGlobals.WebAppStartTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)}({timeSinceAppStart:dd} days {timeSinceAppStart:hh\\:mm} hours ago)<br>");
        ThreadPool.GetMinThreads(out int minWorkerTh, out int minIoThread);
        ThreadPool.GetMaxThreads(out int maxWorkerTh, out int maxIoThread);
        p_sb.Append($"ThId-{Environment.CurrentManagedThreadId}, ProcThreads#:{Process.GetCurrentProcess().Threads.Count}, ThreadPoolTh#:{ThreadPool.ThreadCount}, WorkerTh: [{minWorkerTh}...{maxWorkerTh}], IoTh: [{minIoThread}...{maxIoThread}] <br>");
    }
}