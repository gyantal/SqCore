using System;
using System.IO;
using System.Linq;
using Fin.MemDb;
using QuantConnect.Lean.Engine.Results;
using QuantConnect.Parameters;
using QuantConnect.Securities;
using SqCommon;
using Xunit;
using Microsoft.Extensions.Configuration;

namespace Fin.Engine.tests;   // create test project name with '*.test' of the main project, so test namespace will be in the sub-namespace of the main: "dotnet new xunit -n SqCoreWeb.tests". using SqCoreWeb is not required then.

public class UnitTestBacktester
{
    [Fact]
    public void SqPctAllocationTest()
    {
        FinDb.gFinDb.Init_WT(FinDbRunningEnvironment.WindowsUnitTest);
        SqBacktestConfig backtestConfig = new SqBacktestConfig() { SqResultStat = SqResultStat.SqSimpleStat };
        BacktestingResultHandler? backtestResults = Backtester.BacktestInSeparateThreadWithTimeout("SqPctAllocation", "startDate=2002-07-29&endDate=now&startDateAutoCalcMode=WhenFirstTickerAlive&assets=SPY,TLT&weights=60,40&rebFreq=Daily,30d", null, @"{""ema-fast"":10,""ema-slow"":20}", backtestConfig);

        // Check that RawPV at the end doesn't equal to the RawPV at start. (e.g, Raw PV: 10,00,000 End PV: 63,10,898)
        if (backtestResults != null)
        {
            float rawPvStart = backtestResults.SqSampledLists["rawPV"][0].Value;
            float rawPvEnd = backtestResults.SqSampledLists["rawPV"][^1].Value;
            Assert.NotEqual(rawPvStart, rawPvEnd);
        }

        FinDb.Exit();
    }

    [Fact]
    public static void SqTradeAccumulationTest()
    {
        // For LegacyPortfolios, we have initialize the gMemDb, because that has legacyDbConnection and connectionstrings, etc.
        string sensitiveConfigFullPath = Utils.SensitiveConfigFolderPath() + $"SqCore.WebServer.SqCoreWeb.NoGitHub.json";
        IConfigurationBuilder builder = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())        // GetCurrentDirectory() is the folder of the '*.csproj'.
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)      // no need to copy appsettings.json to the sub-directory of the EXE. 
            .AddJsonFile(sensitiveConfigFullPath, optional: true, reloadOnChange: true);
        //.AddUserSecrets<Program>()    // Used mostly in Development only, not in Production. Stored in a JSON configuration file in a system-protected user profile folder on the local machine. (e.g. user's %APPDATA%\Microsoft\UserSecrets\), the secret values aren't encrypted, but could be in the future.
        // do we need it?: No. Sensitive files are in separate folders, not up on GitHub. If server is not hacked, we don't care if somebody who runs the code can read the settings file. Also, scrambling secret file makes it more difficult to change it realtime.
        //.AddEnvironmentVariables();   // not needed in general. We dont' want to clutter op.sys. environment variables with app specific values.
        Utils.Configuration = builder.Build();
        
        int redisDbIndex = 0;  // DB-0 is ProductionDB. DB-1+ can be used for Development when changing database schema, so the Production system can still work on the ProductionDB. DB-1: George, DB-2: Daya, DB-3: Balazs
        MemDb.MemDb.gMemDb.Init(redisDbIndex, MemDbRunningEnvironment.WindowsUnitTest); // high level DB used by functionalities

        FinDb.gFinDb.Init_WT(FinDbRunningEnvironment.WindowsUnitTest); // initialize the QC Backtesting Engine.
        SqBacktestConfig backtestConfig = new SqBacktestConfig() { SqResultStat = SqResultStat.SqSimpleStat };
        string endDateStrForDay13 = "2025-01-13";
        string endDateStrForDay14 = "2025-01-14";

        int quantityOnDay13 = RunBacktestAndGetQuantity(endDateStrForDay13, "UNG", backtestConfig);
        int quantityOnDay14 = RunBacktestAndGetQuantity(endDateStrForDay14, "UNG", backtestConfig);
        Assert.Equal(-1600, quantityOnDay14); // this will show as correct -1600
        Assert.Equal(-1700, quantityOnDay13); // we have a bug here, because it gives us -1600, that is wrong. It should be -1700.
        // 2025-01-16: UNG -1,600, and there was the following trades before that:
        // UNG	100	$18.47	USD	$1,847	2025-01-14 21:00
        // UNG	100	$18.22	USD	$1,822	2025-01-13 20:59

        // So, the followings should be the UNG positions:
        // 2025-01-10 morning: -1800
        // 2025-01-10 EOD: -1800  // correct
        // 2025-01-13 morning: -1800
        // 2025-01-13 EOD: -1700  // ! Not correct. QC didn't gives 1,700, but gives back -1600, which is wrong.  So, we want to see -1700 on 2025-01-13 
        // 2025-01-14 morning: -1,700
        // 2025-01-14 EOD: -1600  // correct
        FinDb.Exit();
        MemDb.MemDb.Exit();
    }

    private static int RunBacktestAndGetQuantity(string p_endDate, string p_symbol, SqBacktestConfig p_backtestConfig)
    {
        string backtestAlgorithmParam = $"endDate={p_endDate}&";
        BacktestingResultHandler? backtestResult = Backtester.BacktestInSeparateThreadWithTimeout("SqTradeAccumulation", backtestAlgorithmParam, new LegacyPortfolio { LegacyDbPortfName = "!IB-V Sobek-HL(Contango-Bond) harvester Agy Live" }.GetTradeHistory(), @"{""ema-fast"":10,""ema-slow"":20}", p_backtestConfig);

        if (backtestResult == null)
            return 0;

        int quantity = backtestResult.Algorithm.UniverseManager.ActiveSecurities.Values
            .Where(security => security?.Holdings.Symbol.ToString() == p_symbol)
            .Select(security => (int)security.Holdings.Quantity)
            .FirstOrDefault();
        return quantity;
    }
}
