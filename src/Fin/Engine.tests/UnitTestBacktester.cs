using System;
using Fin.MemDb;
using QuantConnect.Parameters;
using QuantConnect.Lean.Engine.Results;
using Xunit;

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
    }
}
