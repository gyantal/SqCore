using System;
using System.Linq;
using System.Threading.Tasks;
using Fin.MemDb;
using QuantConnect;
using QuantConnect.Data;
using QuantConnect.Data.Market;
using QuantConnect.Securities;
using Xunit;
using Xunit.Abstractions;

namespace SqCommon.tests   // create test project name with '*.test' of the main project, so test namespace will be in the sub-namespace of the main: "dotnet new xunit -n SqCoreWeb.tests". using SqCoreWeb is not required then.
{
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
        public void HistoryProviderBaseTest()
        {
            FinDb.gFinDb.Init_WT(FinDbRunningEnvironment.WindowsUnitTest);

            string tickerAsTradedToday2 = "SPY"; // if symbol.zip doesn't exist in Data folder, it will not download it (cost money, you have to download in their shop). It raises an exception.
            Symbol symbol = new(SecurityIdentifier.GenerateEquity(tickerAsTradedToday2, Market.USA, true, FinDb.gFinDb.MapFileProvider), tickerAsTradedToday2);

            DateTime startTimeUtc = new(2008, 01, 01, 8, 0, 0); // 8:00 UTC, that is 3:00 in ET
            // If you want to get 20080104 day data too, it has to be specified like this:
            // class TimeBasedFilter assures that (data.EndTime <= EndTimeLocal)
            // It is assumed that any TradeBar final values are only released at TradeBar.EndTime (OK for minute, hourly data, but not perfect for daily data which is known at 16:00)
            // Any TradeBar's EndTime is Time+1day (assuming that ClosePrice is released not at 16:00, but later, at midnight)
            // So the 20080104's row in CVS is: Time: 20080104:00:00, EndTime:20080105:00:00
            // DateTime endTimeUtc = new(2008, 01, 05, 5, 0, 0); // this will be => 2008-01-05:00:00 endTimeLocal
            DateTime endTimeUtc = new(2025, 01, 25, 23, 59, 0); // test. This is UTC, that is 19:00 in ET. This should give that day 16:00 price.

            // Use TickType.TradeBar. That is in the daily CSV file. TickType.Quote file would contains Ask(Open/High/Low/Close) + Bid(Open/High/Low/Close), like a Quote from a Broker at trading realtime.
            HistoryRequest[] historyRequests = new[]
            {
                new HistoryRequest(startTimeUtc, endTimeUtc, typeof(TradeBar), symbol, Resolution.Daily, SecurityExchangeHours.AlwaysOpen(TimeZones.NewYork),
                    TimeZones.NewYork, null, false, false, DataNormalizationMode.Raw, QuantConnect.TickType.Trade)
                    // TimeZones.NewYork, null, false, false, DataNormalizationMode.Adjusted, QuantConnect.TickType.Trade)
            };

            NodaTime.DateTimeZone sliceTimeZone = TimeZones.NewYork; // "algorithm.TimeZone"

            System.Collections.Generic.List<Slice> result = FinDb.gFinDb.HistoryProvider.GetHistory(historyRequests, sliceTimeZone).ToList();
            Assert.NotEmpty(result);

            TradeBar firstBar = result[0].Bars.Values.First();
            TradeBar lastBar = result[^1].Bars.Values.Last();
            TradeBar lastButOneBar = result[^2].Bars.Values.Last();

            m_output.WriteLine($"First Bar Date: {firstBar.EndTime}, Price: {firstBar.Price}");
            m_output.WriteLine($"Last Bar Date: {lastBar.EndTime}, Price: {lastBar.Price}");

            // Validate the first available data
            Assert.Equal(new DateTime(2008, 01, 02), firstBar.EndTime.Date); // Expected: 2008-01-02
            Assert.InRange(firstBar.Price, 144.91m, 144.95m); // 144.9300

            // Validate the last available data
            Assert.Equal(new DateTime(2025, 01, 24), lastBar.EndTime.Date); // Expected: 2025-01-24 Friday, because 01-25 is Saturday
            Assert.InRange(lastBar.Price, 607.95m, 608m); // 607.9700

            // Check that the last 2 dates are not equal
            Assert.NotEqual(lastBar.EndTime.Date, lastButOneBar.EndTime.Date);

        }

        [Fact]
        public void HistoryProviderWeekendTest()
        {
            FinDb.gFinDb.Init_WT(FinDbRunningEnvironment.WindowsUnitTest);

            string ticker = "SPY";
            Symbol symbol = new(SecurityIdentifier.GenerateEquity(ticker, Market.USA, true, FinDb.gFinDb.MapFileProvider), ticker);

            DateTime weekendStart = new(2025, 01, 25, 8, 0, 0);  // Saturday
            DateTime weekendEnd = new(2025, 01, 26, 23, 59, 0);   // Sunday

            var historyRequests = new[]
            {
                new HistoryRequest(weekendStart, weekendEnd, typeof(TradeBar), symbol, Resolution.Daily, SecurityExchangeHours.AlwaysOpen(TimeZones.NewYork),
                    TimeZones.NewYork, null, false, false, DataNormalizationMode.Raw, QuantConnect.TickType.Trade)
            };

            NodaTime.DateTimeZone sliceTimeZone = TimeZones.NewYork;

            var result = FinDb.gFinDb.HistoryProvider.GetHistory(historyRequests, sliceTimeZone).ToList();

            m_output.WriteLine($"Weekend Test Data Count: {result.Count}");
            Assert.Empty(result); // No data should be returned for weekends
        }
    }
}
