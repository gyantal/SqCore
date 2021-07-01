using System;
using System.Collections.Generic;
using System.Linq;
using SqCommon;
using Xunit;
using IBApi;

namespace BrokerCommon.tests
{
    public class UnitTestGateway
    {
        // TODO:
        // 1. if a buy order is sent with estimated realtime of $1M+, then it is not allowed. BrokerWatcher.PlaceOrder($1M) should give an error, before placing the order.

        [Fact]
        public void TestGateway_ConnectToGA_CheckNav()
        {
            Console.WriteLine("TestGatewayConnectToGA()");
            Gateway gw = new Gateway(GatewayId.GyantalMain, p_accountMaxTradeValueInCurrency: 100000, p_accountMaxEstimatedValueSumRecentlyAllowed: 160000) { VbAccountsList = "U407941", Host = ServerIp.AtsVirtualBrokerServerPublicIpForClients, SocketPort = (int)GatewayPort.VbSrvGyantalSecondary, BrokerConnectionClientID = GatewayClientID.SqCoreToGaTest1 };
            gw.Reconnect();
            Assert.True(gw.IsConnected);

            List<BrAccSum>? accSums = gw.GetAccountSums();
            Assert.NotNull(accSums);
            if (accSums == null)
                return;

            string navStr = accSums.First(r => r.Tag == AccountSummaryTags.NetLiquidation).Value;
            Assert.False(String.IsNullOrEmpty(navStr));

            Assert.True(Double.TryParse(navStr, out double nav));
            Assert.False(nav == 0); // if TryParse conversion fails, it returns 0
            Assert.False(double.IsNaN(nav));

            gw.Disconnect();
        }
    }
}
