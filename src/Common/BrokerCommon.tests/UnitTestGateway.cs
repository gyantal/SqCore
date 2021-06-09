using System;
using System.Collections.Generic;
using System.Linq;
using SqCommon;
using Xunit;

namespace BrokerCommon.tests
{
    public class UnitTestGateway
    {
        [Fact]
        public void TestGateway_ConnectToGA_CheckNav()
        {
            Console.WriteLine("TestGatewayConnectToGA()");
            Gateway gw = new Gateway(GatewayId.GyantalMain, p_accountMaxTradeValueInCurrency: 100000, p_accountMaxEstimatedValueSumRecentlyAllowed: 160000) { VbAccountsList = "U407941", Host = ServerIp.AtsVirtualBrokerServerPublicIpForClients, SocketPort = (int)GatewayPort.VbSrvGyantalSecondary, BrokerConnectionClientID = GatewayClientID.SqCoreToGaTest1 };
            gw.Reconnect();
            Assert.True(gw.IsConnected);

            List<AccSum>? accSums = gw.GetAccountSums();
            Assert.NotNull(accSums);
            if (accSums == null)
                return;

            string navStr = accSums.First(r => r.Tag == "NetLiquidation").Value;
            Assert.False(String.IsNullOrEmpty(navStr));

            Assert.True(Double.TryParse(navStr, out double nav));
            Assert.False(nav == 0); // if TryParse conversion fails, it returns 0
            Assert.False(double.IsNaN(nav));

            gw.Disconnect();
        }
    }
}
