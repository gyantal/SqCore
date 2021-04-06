using System;
using Xunit;

namespace SqCoreWeb.tests   // create test project name with '*.test' of the main project, so test namespace will be in the sub-namespace of the main: "dotnet new xunit -n SqCoreWeb.tests". using SqCoreWeb is not required then.
{
    public class UnitTestMain
    {
        [Fact]
        public void TestHello()
        {
            Console.WriteLine("Hello Test");
        }

        [Fact]
        public void Test_WebsitesMonitor_ParseSpglobalDateStr()
        {
            Console.WriteLine("Hello Test2");
            DateTime dt;
            dt = WebsitesMonitorExecution.ParseSpglobalDateStr("Apr 5, 2021 5:15 PM");
            dt = WebsitesMonitorExecution.ParseSpglobalDateStr("Apr 6, 2021 11:00 AM");
            Assert.Throws<FormatException>(() => WebsitesMonitorExecution.ParseSpglobalDateStr("blablabla"));
        }
    }
}
