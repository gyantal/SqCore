using IBApi;
using SqCommon;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Utils = SqCommon.Utils;

namespace BrokerCommon
{
    public class SavedState : PersistedState   // data to persist between restarts of the vBroker process: settings that was set up by client, or OptionCrawler tickerList left to crawl
    {
        public bool IsSendErrorEmailAtGracefulShutdown { get; set; } = true;   // switch this off before deployment, and switch it on after deployment; make functionality on the WebSite
    }

    
    // this is the Trading Risk Manager Agent. The gateway for trading.
    public partial class BrokersWatcher
    {
        public static BrokersWatcher gWatcher = new BrokersWatcher();   // Singleton pattern
        const double cReconnectTimerFrequencyMinutes = 15; 
        System.Threading.Timer? m_reconnectTimer = null;
        SavedState m_persistedState = new SavedState();
        List<Gateway> m_gateways = new List<Gateway>();

        Gateway? m_mainGateway = null;  // m_mainGateway can be null, if we Debug WebSite code and no gateway is attached at all

        bool m_isSupportPreStreamRealtimePrices;
        public SavedState PersistedState
        {
            get
            {
                return m_persistedState;
            }

            set
            {
                m_persistedState = value;
            }
        }

        public void Init()
        {
            Utils.Logger.Info("***GatewaysWatcher:Init()");

            string isSupportPreStreamRealtimePricesStr = Utils.Configuration["SupportPreStreamRealtimePrices"];
            Console.WriteLine($"SupportPreStreamRealtimePrices: {isSupportPreStreamRealtimePricesStr ?? "False"}");
            m_isSupportPreStreamRealtimePrices = isSupportPreStreamRealtimePricesStr != null && isSupportPreStreamRealtimePricesStr.ToUpper() == "TRUE";

            // For succesful remote connection, check the following:
            // 1. Remote SqCore connection: "sudo ufw allow 7303/7308/7301"  (check "sudo ufw status") 
            // 2. in Amazon AWS: allow All TCP traffic to the developer machine IP only. (Don't allow 7303 port in general to the public. Unsafe, because there is no username/pwd check at connection to port 7303)
            // 3. in IB TWS: Configure/Api/Settings/Trusted IPs: insert public IP of Windows machine (Google: what is my IP)
            // <optional> 4. in Windows PowerShell: Test-NetConnection 34.251.1.119 -Port 7303  (it should say Success). Then you might have to restart the Linux server, because IB TWS started the connection and is confused

            if (Utils.RunningPlatform() == Platform.Windows)    // Windows Debug: Gyantal is local port, Charmat, DeBlanzac is remote port.
            {
                // Option1: m_mainGateway can be null, if we Debug "WebSite"-related code and no gateway is attached at all (for speed)
                // m_gateways = new List<Gateway>();
                // m_mainGateway = null;

                //  Option2: only 1 gateway1 is attached to local TWS
                // Gateway gateway1 = new Gateway(GatewayId.GyantalMain, p_accountMaxTradeValueInCurrency: 100000 /* UberVXX is 12K, 2xleveraged=24K, double=48K*/, p_accountMaxEstimatedValueSumRecentlyAllowed: 160000) { VbAccountsList = "U407941", Host = ServerIp.LocalhostLoopbackWithIP, SocketPort = (int)GatewayPort.GyantalMain, BrokerConnectionClientID = GatewayClientID.LocalTws1 };
                // m_gateways = new List<Gateway>() {gateway1};
                // m_mainGateway = gateway1;

                //  Option3: all gateways are attached to remote or local servers. To Debug vBroker trading 
                Gateway gateway1 = new Gateway(GatewayId.CharmatMain, p_accountMaxTradeValueInCurrency: 600000, p_accountMaxEstimatedValueSumRecentlyAllowed: 10) { VbAccountsList = "U988767", Host = ServerIp.SqCoreServerPublicIpForClients, SocketPort = (int)GatewayPort.SqCoreSrvCharmatMain, BrokerConnectionClientID = GatewayClientID.SqCoreToDcDev1 };
                Gateway gateway2 = new Gateway(GatewayId.DeBlanzacMain, p_accountMaxTradeValueInCurrency: 1.0 /* don't trade here */, p_accountMaxEstimatedValueSumRecentlyAllowed: 10) { VbAccountsList = "U1146158", Host = ServerIp.SqCoreServerPublicIpForClients, SocketPort = (int)GatewayPort.SqCoreSrvDeBlanzacMain, BrokerConnectionClientID = GatewayClientID.SqCoreToDbDev1 };
                Gateway gateway3 = new Gateway(GatewayId.GyantalMain, p_accountMaxTradeValueInCurrency: 100000 /* UberVXX is 12K, 2xleveraged=24K, double=48K*/, p_accountMaxEstimatedValueSumRecentlyAllowed: 160000) { VbAccountsList = "U407941", Host = ServerIp.AtsVirtualBrokerServerPublicIpForClients, SocketPort = (int)GatewayPort.VbSrvGyantalSecondary, BrokerConnectionClientID = GatewayClientID.SqCoreToGaDev1 };
                m_gateways = new List<Gateway>() {gateway1, gateway2, gateway3};
                m_mainGateway = gateway1;
            }
            else    // Linux Production: Gyantal is remote port (on VBrokerServer), Charmat, DeBlanzac is local port.
            {
                Gateway gateway1 = new Gateway(GatewayId.CharmatMain, p_accountMaxTradeValueInCurrency: 600000, p_accountMaxEstimatedValueSumRecentlyAllowed: 10) { VbAccountsList = "U988767", Host = ServerIp.LocalhostLoopbackWithIP, SocketPort = (int)GatewayPort.SqCoreSrvCharmatMain, BrokerConnectionClientID = GatewayClientID.SqCoreToDcProd };
                Gateway gateway2 = new Gateway(GatewayId.DeBlanzacMain, p_accountMaxTradeValueInCurrency: 1.0 /* don't trade here */, p_accountMaxEstimatedValueSumRecentlyAllowed: 10) { VbAccountsList = "U1146158", Host = ServerIp.LocalhostLoopbackWithIP, SocketPort = (int)GatewayPort.SqCoreSrvDeBlanzacMain, BrokerConnectionClientID = GatewayClientID.SqCoreToDbProd };
                Gateway gateway3 = new Gateway(GatewayId.GyantalMain, p_accountMaxTradeValueInCurrency: 100000 /* UberVXX is 12K, 2xleveraged=24K, double=48K*/, p_accountMaxEstimatedValueSumRecentlyAllowed: 160000) { VbAccountsList = "U407941", Host = ServerIp.AtsVirtualBrokerServerPublicIpForClients, SocketPort = (int)GatewayPort.VbSrvGyantalSecondary, BrokerConnectionClientID = GatewayClientID.SqCoreToGaProd };
                m_gateways = new List<Gateway>() {gateway1, gateway2, gateway3};
                m_mainGateway = gateway1;   // CharmatMain
            }

            m_reconnectTimer = new System.Threading.Timer(new TimerCallback(ReconnectToGatewaysTimer_Elapsed), null, TimeSpan.Zero, TimeSpan.FromMinutes(cReconnectTimerFrequencyMinutes));
        }

        private void ReconnectToGatewaysTimer_Elapsed(object? p_stateObj)   // Timer is coming on a ThreadPool thread
        {
            Utils.Logger.Info("GatewaysWatcher:ReconnectToGatewaysTimer_Elapsed() BEGIN");
            try
            {
                bool isMainGatewayConnectedBefore = m_mainGateway != null && m_mainGateway.IsConnected;

                // IB API is not async. Thread waits until the connection is established.
                // Task.Run() uses threads from the thread pool, so it executes those connections parallel in the background. Then wait for them.
                var reconnectTasks = m_gateways.Where(l => !l.IsConnected).Select(r => Task.Run(() => r.Reconnect()));
                Task.WhenAll(reconnectTasks).TurnAsyncToSyncTask(); // "await Task.WhenAll()" has to be waited properly

                Utils.Logger.Info("GatewaysWatcher:ReconnectToGateways() reconnectTasks ended.");
                foreach (var gateway in m_gateways)
                {
                    Utils.Logger.Info($"GatewayId: '{gateway.GatewayId}' IsConnected: {gateway.IsConnected}");
                }

                bool isMainGatewayConnectedNow = m_mainGateway != null && m_mainGateway.IsConnected;
                if (!isMainGatewayConnectedBefore && isMainGatewayConnectedNow)   // if this is the first time mainGateway connected after being dead
                    MainGatewayJustConnected();
            }
            catch (Exception e)
            {
                Utils.Logger.Info("GatewaysWatcher:TryReconnectToGateways() in catching exception (it is expected on MTS that TWS is not running, so it cannot connect): " + e.ToStringWithShortenedStackTrace(400));
            }


            // Without all the IB connections (isAllConnected), we can choose to crash the App, but we do NOT do that, because we may be able to recover them later. 
            // It is a strategic (safety vs. conveniency) decision: in that case if not all IBGW is connected, (it can be an 'expected error'), VBroker runs further and try connecting every 10 min.
            // on ManualTrader server failed connection is expected. Don't send Error. However, on AutoTraderServer, it is unexpected (at the moment), because IBGateways and VBrokers restarts every day.
            var notConnectedGateways = String.Join(",", m_gateways.Where(l => !l.IsConnected).Select(r => r.GatewayId + "/"));
            if (!String.IsNullOrEmpty(notConnectedGateways))
            {
                if (IgnoreErrorsBasedOnMarketTradingTime(offsetToOpenMin: -60))
                    return; // skip processing the error further. Don't send it to HealthMonitor.
                HealthMonitorMessage.SendAsync($"Gateways are not connected. Not connected gateways {notConnectedGateways}", HealthMonitorMessageID.SqCoreWebCsError).TurnAsyncToSyncTask();
            }
            Utils.Logger.Info("GatewaysWatcher:ReconnectToGatewaysTimer_Elapsed() END");
        }

        private void MainGatewayJustConnected()
        {
            if (m_isSupportPreStreamRealtimePrices && m_mainGateway != null)
            {
                // getting prices of SPY (has dividend, but liquid) or VXX (no dividend, but less liquids) is always a must. An Agent would always look that price. So, subscribe to that on the MainGateway
                // see what is possible to call: "g:\temp\_programmingTemp\TWS API_972.12(2016-02-26)\samples\CSharp\IBSamples\IBSamples.sln" 

                // for NeuralSniffer
                // 2020-06: NeuralSniffer is not traded at the moment.
                // m_mainGateway.BrokerWrapper.ReqMktDataStream(VBrokerUtils.ParseSqTickerToContract("^RUT"));
                // m_mainGateway.BrokerWrapper.ReqMktDataStream(VBrokerUtils.ParseSqTickerToContract("UWM"));
                // m_mainGateway.BrokerWrapper.ReqMktDataStream(VBrokerUtils.ParseSqTickerToContract("TWM"));

                // for UberVXX
                m_mainGateway.BrokerWrapper.ReqMktDataStream(VBrokerUtils.ParseSqTickerToContract("VXX"));
                m_mainGateway.BrokerWrapper.ReqMktDataStream(VBrokerUtils.ParseSqTickerToContract("SVXY"));
                //m_mainGateway.BrokerWrapper.ReqMktDataStream(new Contract() { Symbol = "SPY", SecType = "STK", Currency = "USD", Exchange = "SMART" }); // for TotM forecast, but it is not needed just yet

                // for HarryLong
                m_mainGateway.BrokerWrapper.ReqMktDataStream(VBrokerUtils.ParseSqTickerToContract("TQQQ"));
                m_mainGateway.BrokerWrapper.ReqMktDataStream(VBrokerUtils.ParseSqTickerToContract("TMV"));
                m_mainGateway.BrokerWrapper.ReqMktDataStream(VBrokerUtils.ParseSqTickerToContract("VXZ"));
                m_mainGateway.BrokerWrapper.ReqMktDataStream(VBrokerUtils.ParseSqTickerToContract("SCO"));  // 2020-04-02: use SCO (2x); instead of short USO (1x), short UWT (-3x) was used, but it was delisted, because it went to penny stock
                m_mainGateway.BrokerWrapper.ReqMktDataStream(VBrokerUtils.ParseSqTickerToContract("UNG"));

                m_mainGateway.BrokerWrapper.ReqMktDataStream(VBrokerUtils.ParseSqTickerToContract("TMF")); // Can be commented out: TMF (3x) is for Agy,
                m_mainGateway.BrokerWrapper.ReqMktDataStream(VBrokerUtils.ParseSqTickerToContract("USO")); // Can be commented out: Agy uses partial SCO, partial USO for diversifying and because SCO is not a good tracker of USO

                // for TAA, but it is only temporary. We will not stream this unnecessary data all day long, as TAA can take its time. It only trades MOC. Extra 2-3 seconds doesn't matter.
                // "TLT"+ "MDY","ILF","FEZ","EEM","EPP","VNQ","IBB"  +  "MVV", "URE", "BIB"
                // 2020-06: TAA is not traded at the moment.
                // m_mainGateway.BrokerWrapper.ReqMktDataStream(VBrokerUtils.ParseSqTickerToContract("TLT"));
                // m_mainGateway.BrokerWrapper.ReqMktDataStream(VBrokerUtils.ParseSqTickerToContract("MDY"));
                // m_mainGateway.BrokerWrapper.ReqMktDataStream(VBrokerUtils.ParseSqTickerToContract("ILF"));
                // m_mainGateway.BrokerWrapper.ReqMktDataStream(VBrokerUtils.ParseSqTickerToContract("FEZ"));
                // m_mainGateway.BrokerWrapper.ReqMktDataStream(VBrokerUtils.ParseSqTickerToContract("EEM"));
                // m_mainGateway.BrokerWrapper.ReqMktDataStream(VBrokerUtils.ParseSqTickerToContract("EPP"));
                // m_mainGateway.BrokerWrapper.ReqMktDataStream(VBrokerUtils.ParseSqTickerToContract("VNQ"));
                // m_mainGateway.BrokerWrapper.ReqMktDataStream(VBrokerUtils.ParseSqTickerToContract("IBB"));
                // m_mainGateway.BrokerWrapper.ReqMktDataStream(VBrokerUtils.ParseSqTickerToContract("MVV"));
                // m_mainGateway.BrokerWrapper.ReqMktDataStream(VBrokerUtils.ParseSqTickerToContract("URE"));
                // m_mainGateway.BrokerWrapper.ReqMktDataStream(VBrokerUtils.ParseSqTickerToContract("BIB"));
            }
        }

       
        // at graceful shutdown, it is called
        public void Exit()
        {
            foreach (var gateway in m_gateways)
            {
                gateway.Disconnect();
            }

            //PersistedState.Save();
            //StopTcpMessageListener();
        }

        public void ServerDiagnostic(StringBuilder p_sb)
        {
            p_sb.Append("<H2>BrokersWatcher</H2>");
            foreach (Gateway gw in m_gateways)
            {
                gw.ServerDiagnostic(p_sb);
            }
     }

        // there are some weird IB errors that happen usually when IB server is down. 99% of the time it is at the weekend, or when pre or aftermarket. In this exceptional times, ignore errors.
        public static bool IgnoreErrorsBasedOnMarketTradingTime(int offsetToOpenMin = 0, int offsetToCloseMin = 40)
        {
            DateTime utcNow = DateTime.UtcNow;
            DateTime etNow = Utils.ConvertTimeFromUtcToEt(utcNow);
            if (etNow.DayOfWeek == DayOfWeek.Saturday || etNow.DayOfWeek == DayOfWeek.Sunday)   // if it is the weekend => no Error
                return true;

            TimeSpan timeTodayEt = etNow - etNow.Date;
            // The NYSE and NYSE MKT are open from Monday through Friday 9:30 a.m. to 4:00 p.m. ET.
            // "Gateways are not connected" errors handled with more strictness. We expect that there is a connection to IBGateway at least 1 hour before open. At 8:30.
            if (timeTodayEt.TotalMinutes < 9 * 60 + 29 + offsetToOpenMin) // ignore errors before 9:30. 
                return true;   // if it is not Approximately around market hours => no Error

            if (timeTodayEt.TotalMinutes > 16 * 60 + offsetToCloseMin)    // IB: not executed shorting trades are cancelled 30min after market close. Monitor errors only until that.
                return true;   // if it is not Approximately around market hours => no Error

            // TODO: <not too important> you can skip holiday days too later; and use real trading hours, which sometimes are shortened, before or after holidays.
            return false;
        }

        internal bool IsGatewayConnected(GatewayId p_ibGatewayIdToTrade)
        {
            var gateway = m_gateways.FirstOrDefault(r => r.GatewayId == p_ibGatewayIdToTrade);
            if (gateway == null)
                return false;
            return (gateway.IsConnected);
        }


        internal bool GetAlreadyStreamedPrice(Contract p_contract, ref Dictionary<int, PriceAndTime> p_quotes)
        {
            if (m_mainGateway == null || !m_mainGateway.IsConnected)
                return false;
            return m_mainGateway.BrokerWrapper.GetAlreadyStreamedPrice(p_contract, ref p_quotes);
        }

        internal bool ReqHistoricalData(DateTime p_endDateTime, int p_lookbackWindowSize, string p_whatToShow, Contract p_contract, out List<QuoteData>? p_quotes)
        {
            p_quotes = null;
            if (m_mainGateway == null || !m_mainGateway.IsConnected)
                return false;
            return m_mainGateway.BrokerWrapper.ReqHistoricalData(p_endDateTime, p_lookbackWindowSize, p_whatToShow, p_contract, out p_quotes);
        }

        internal int PlaceOrder(GatewayId p_gatewayIdToTrade, double p_portfolioMaxTradeValueInCurrency, double p_portfolioMinTradeValueInCurrency, 
            Contract p_contract, TransactionType p_transactionType, double p_volume, OrderExecution p_orderExecution, OrderTimeInForce p_orderTif, double? p_limitPrice, double? p_stopPrice, bool p_isSimulatedTrades, double p_oldVolume, StringBuilder p_detailedReportSb)
        {
            Gateway? userGateway = m_gateways.FirstOrDefault(r => r.GatewayId == p_gatewayIdToTrade);
            if (userGateway == null || !userGateway.IsConnected)
            {
                Utils.Logger.Error($"ERROR. PlacingOrder(). GatewayIdToTrade {p_gatewayIdToTrade} is not found among connected Gateways or it is not connected.");
                return -1;
            }

            var rtPrices = new Dictionary<int, PriceAndTime>() { { TickType.MID, new PriceAndTime() } };   // MID is the most honest price. LAST may happened 1 hours ago
            if (m_mainGateway != null)
                m_mainGateway.BrokerWrapper.GetAlreadyStreamedPrice(p_contract, ref rtPrices);
            int virtualOrderId = userGateway.PlaceOrder(p_portfolioMaxTradeValueInCurrency, p_portfolioMinTradeValueInCurrency, p_contract, p_transactionType, p_volume, p_orderExecution, p_orderTif, p_limitPrice, p_stopPrice, rtPrices[TickType.MID].Price, p_isSimulatedTrades, p_oldVolume, p_detailedReportSb);
            return virtualOrderId;
        }

        internal bool WaitOrder(GatewayId p_gatewayIdToTrade, int p_virtualOrderId, bool p_isSimulatedTrades)
        {
            Gateway? userGateway = m_gateways.FirstOrDefault(r => r.GatewayId == p_gatewayIdToTrade);
            if (userGateway == null || !userGateway.IsConnected)
                return false;

            return userGateway.WaitOrder(p_virtualOrderId, p_isSimulatedTrades);
        }

        internal bool GetVirtualOrderExecutionInfo(GatewayId p_gatewayIdToTrade, int p_virtualOrderId, ref OrderStatus orderStatus, ref double executedVolume, ref double executedAvgPrice, ref DateTime executionTime, bool p_isSimulatedTrades)
        {
             Gateway? userGateway = m_gateways.FirstOrDefault(r => r.GatewayId == p_gatewayIdToTrade);
            if (userGateway == null)
                return false;

            return userGateway.GetVirtualOrderExecutionInfo(p_virtualOrderId, ref orderStatus, ref executedVolume, ref executedAvgPrice, ref executionTime, p_isSimulatedTrades);
        }

        public string GetRealtimePriceService(string p_query)
        {
            if (m_mainGateway == null || !m_mainGateway.IsConnected)
                return string.Empty;
            return m_mainGateway.GetRealtimePriceService(p_query);
        }

        
    }
}
