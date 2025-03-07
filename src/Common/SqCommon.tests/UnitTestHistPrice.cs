using System;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace SqCommon.tests;   // create test project name with '*.test' of the main project, so test namespace will be in the sub-namespace of the main: "dotnet new xunit -n SqCoreWeb.tests". using SqCoreWeb is not required then.

public class UnitTestHistPrice
{
    // From VsCode, xUnit will NOT output Console.WriteLine to the standard output when running tests. 
    // Console.WriteLine() would output the msg, if run from command line with a -v (verbose) flag as "dotnet test -v normal", but that also outputs too much info.
    // ITestOutputHelper WriteLine() will appear in the "TEST RESULTS" tab when you click on the specific test. As Output of that test.
    private readonly ITestOutputHelper m_output;
    public UnitTestHistPrice(ITestOutputHelper p_output)
    {
        m_output = p_output;
    }

    [Fact]
    public void TestHello()
    {
        m_output.WriteLine("Hello Test");
    }

    [Fact]
    public async Task HistoricalTest()
    {
        var histResult = await HistPrice.g_HistPrice.GetHistAsync("AAPL", HpDataNeed.AdjClose | HpDataNeed.Split | HpDataNeed.Dividend | HpDataNeed.OHLCV, new DateTime(2023, 1, 3), new DateTime(2023, 1, 4));
        Assert.Equal(130.28f, histResult!.Opens![0]);
        Assert.Equal(130.9f, histResult!.Highs![0]);
        Assert.Equal(124.17f, histResult!.Lows![0]);
        Assert.Equal(125.07f, histResult!.Closes![0]);
        Assert.Equal(112_117_500, histResult!.Volumes![0]);
    }

    [Fact]
    public async Task DividendTest()
    {
        var histResult = await HistPrice.g_HistPrice.GetHistAsync("AAPL", HpDataNeed.Dividend, new DateTime(2016, 2, 4), new DateTime(2016, 2, 5));
        Assert.Equal(0.13f, histResult!.Dividends![0].Amount);
    }

    [Fact]
    public async Task SplitTest()
    {
        var histResult = await HistPrice.g_HistPrice.GetHistAsync("AAPL", HpDataNeed.Split, new DateTime(2014, 6, 1), new DateTime(2014, 6, 15));
        Assert.Equal(1, histResult!.Splits![0].BeforeSplit);
        Assert.Equal(7, histResult!.Splits![0].AfterSplit); // 1 became 7

        histResult = await HistPrice.g_HistPrice.GetHistAsync("VXX", HpDataNeed.Split, new DateTime(2024, 7, 24), new DateTime(2024, 7, 25));
        Assert.Equal(4, histResult!.Splits![0].BeforeSplit);
        Assert.Equal(1, histResult!.Splits![0].AfterSplit); // 4 joined together to 1
    }

    [Fact]
    public async Task NonExistingTickerTest()
    {
        var histResult = await HistPrice.g_HistPrice.GetHistAsync("BLABLA", HpDataNeed.Split, new DateTime(2014, 6, 1), new DateTime(2014, 6, 15));
        Assert.Equal("Error. HttpResponse StatusCode: NotFound. Non-existing ticker?", histResult!.ErrorStr);
    }
}