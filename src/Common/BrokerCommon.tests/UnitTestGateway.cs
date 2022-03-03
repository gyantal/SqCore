using System;
using System.Collections.Generic;
using System.Linq;
using SqCommon;
using Xunit;
using IBApi;
using System.Threading;

namespace BrokerCommon.tests
{
    public class UnitTestGateway
    {
        // TODO:
        // 1. if a buy order is sent with estimated realtime of $1M+, then it is not allowed. BrokerWatcher.PlaceOrder($1M) should give an error, before placing the order.

        [Fact]   // we cannot run this parallel to the next Test, because we don't want to open many IbGateways
        public void TestGateway_ConnectToGA_CheckNav()
        {
            Console.WriteLine("TestGatewayConnectToGA()");
            Gateway gw = new(GatewayId.GyantalMain, p_accountMaxTradeValueInCurrency: 100000, p_accountMaxEstimatedValueSumRecentlyAllowed: 160000) 
                { VbAccountsList = "U407941", Host = ServerIp.AtsVirtualBrokerServerPublicIpForClients, SocketPort = (int)GatewayPort.GyantalMain, 
                SuggestedIbConnectionClientID = (int)GatewayClientID.SqCoreToGaTest1 };
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

        [Fact]
        public void TestGateway_ConnectToGA_CheckTickOptionComputation()
        {
            Console.WriteLine("TestGatewayConnectToGA()");
            Gateway gw = new(GatewayId.GyantalMain, p_accountMaxTradeValueInCurrency: 100000, p_accountMaxEstimatedValueSumRecentlyAllowed: 160000) 
                //{ VbAccountsList = "U407941", Host = ServerIp.AtsVirtualBrokerServerPublicIpForClients, SocketPort = (int)GatewayPort.VbSrvGyantalSecondary, 
                { VbAccountsList = "U407941", Host = ServerIp.LocalhostLoopbackWithIP, SocketPort = (int)GatewayPort.GyantalMain, 
                SuggestedIbConnectionClientID = (int)GatewayClientID.SqCoreToGaTest1 };
            gw.Reconnect();
            Assert.True(gw.IsConnected);
            List<BrAccSum>? accSums = gw.GetAccountSums();
            Assert.NotNull(accSums);
            if (accSums == null)
                return;


            List<BrAccPos>? poss = gw.GetAccountPoss(Array.Empty<string>());

            // Contract contractOwned = poss!.Where(r => r.Contract.SecType == "OPT" && r.Contract.Symbol == "UNG" && r.Contract.Strike == 18).FirstOrDefault()!.Contract;
            Contract contractOwned = poss!.Where(r => r.Contract.SecType == "OPT" && r.Contract.Symbol != "VIX").FirstOrDefault()!.Contract;
            contractOwned.Exchange = "SMART";   // Contract.Exchange cannot be left empty, so if it is empty (like with options), fill with SMART

            Contract contractOption = new() {
                // ConId = 522425461,   // ContractID is not necessary to get price data or Option greeks
                // LocalSymbol = "UNG   211203P00018000",
                //TradingClass = "UNG"
                Currency = "USD",
                Exchange = "SMART",
                LastTradeDateOrContractMonth = "20211203",
                Multiplier = "100",
                Right = "P",
                SecType = "OPT",
                Strike = 18,
                Symbol = "UNG"
            };

            Contract contractStock = new() {
                Currency = "USD",
                Exchange = "SMART",
                SecType = "STK",
                Symbol = "UNG",
            };

            bool isDeltaReceived = false;
            bool snapshot = true;   // Snapshot data works without ContractId. Snapshot also gives back Option Deltas

            int mktDataId = gw.BrokerWrapper.ReqMktDataStream(contractOwned, string.Empty, snapshot,  // "221" is the code for MarkPrice. If data is streamed continously and then we ask one snapshot of the same contract, snapshot returns currently, and stream also correctly continues later. As expected.
                (cb_mktDataId, cb_mktDataSubscr, cb_tickType, cb_price) =>  // MktDataArrived callback
                {
                    Console.WriteLine($"ReqMktData() CB: {TickType.getField(cb_tickType)}: {cb_price}");
                },
                (cb_mktDataId, cb_mktDataSubscr, cb_errorCode, cb_errorMsg) =>  // MktDataError callback
                {
                    Console.WriteLine($"Error in ReqMktDataStream(). {cb_mktDataSubscr.Contract.Symbol} : {cb_errorCode}: {cb_errorMsg}");
                },
                (cb_mktDataId, cb_mktDataSubscr, cb_field, cb_value) => // MktDataTickGeneric callback. (e.g. MarkPrice) Assume this is the last callback for snapshot data. (!Not true for OTC stocks, but we only use this for options) Note sometimes it is not called, only Ask,Bid is coming.
                {
                    Console.WriteLine($"TickGeneric in ReqMktDataStream(). {cb_mktDataSubscr.Contract.Symbol} : {TickType.getField(cb_field)}: {cb_value}");
                },
                (cb_mktDataId, cb_mktDataSubscr, cb_type) =>    // MktDataType callback
                {
                    Console.WriteLine($"MarketDataType in ReqMktDataStream(). {cb_mktDataSubscr.Contract.Symbol} : {cb_type}");
                },
                (cb_mktDataId, cb_mktDataSubscr, cb_field, cd_impVol, cb_delta, cb_undPrice) => // MktDataTickOptionComputation callback.
                {   // Tick Id:1222, Field: halted, Value: 0
                    // 2021-12-01: OTH: ARKK option. MktDataTickOptionComputation callback is not called at all. Even after waiting for 16 seconds. Maybe it is only called in RTH.
                    Console.WriteLine($"MktDataTickOptionComputation() CB: {TickType.getField(cb_field)}: Delta:{cb_delta}, UndPrice:{cb_undPrice}");
                    isDeltaReceived = true;
                });    // as Snapshot, not streaming data

            Thread.Sleep(15*1000);
            Assert.True(isDeltaReceived);

            gw.Disconnect();
        }
    }
}
