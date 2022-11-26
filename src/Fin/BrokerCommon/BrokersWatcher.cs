using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using IBApi;
using SqCommon;
using Utils = SqCommon.Utils;

namespace Fin.BrokerCommon;

public class SavedState : PersistedState // data to persist between restarts of the vBroker process: settings that was set up by client, or OptionCrawler tickerList left to crawl
{
    public bool IsSendErrorEmailAtGracefulShutdown { get; set; } = true;   // switch this off before deployment, and switch it on after deployment; make functionality on the WebSite
}

// this is the Trading Risk Manager Agent. The gateway for trading.
public partial class BrokersWatcher : IDisposable
{
    public static readonly BrokersWatcher gWatcher = new();   // Singleton pattern
    const double cReconnectTimerFrequencyMinutes = 15;
    System.Threading.Timer? m_reconnectTimer = null;
    SavedState m_persistedState = new();
    List<Gateway> m_gateways = new();

    Gateway? m_mainGateway = null;  // m_mainGateway can be null, if we Debug WebSite code and no gateway is attached at all

    bool m_isSupportPreStreamRealtimePrices;
    private bool disposedValue;

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

        string isSupportPreStreamRealtimePricesStr = Utils.Configuration["SupportPreStreamRealtimePrices"] ?? "False";
        Console.WriteLine($"IntBr:SupportPreStreamRealtimePrices: {isSupportPreStreamRealtimePricesStr}");
        m_isSupportPreStreamRealtimePrices = isSupportPreStreamRealtimePricesStr.ToUpper() == "TRUE";

        // For succesful remote connection, check the following:
        // 1. Remote SqCore connection: "sudo ufw allow 7303/7308/7301"  (check "sudo ufw status")
        // 2. in Amazon AWS: allow All TCP traffic to the developer machine IP only. (Don't allow 7303 port in general to the public. Unsafe, because there is no username/pwd check at connection to port 7303)
        // 3. in IB TWS: Configure/Api/Settings/Trusted IPs: insert public IP of Windows machine (Google: what is my IP)
        // <optional> 4. in Windows PowerShell: Test-NetConnection 34.251.1.119 -Port 7303  (it should say Success). Then you might have to restart the Linux server, because IB TWS started the connection and is confused

        // Option1: m_mainGateway can be null, if we Debug "WebSite"-related code and no gateway is attached at all (for speed)
        // m_gateways = new List<Gateway>();
        // m_mainGateway = null;

        // Option2: only 1 gateway1 is attached to local TWS
        // Gateway gateway1 = new Gateway(GatewayId.GyantalMain, p_accountMaxTradeValueInCurrency: 100000 /* UberVXX is 12K, 2xleveraged=24K, double=48K*/, p_accountMaxEstimatedValueSumRecentlyAllowed: 160000) { VbAccountsList = "U407941", Host = ServerIp.LocalhostLoopbackWithIP, SocketPort = (int)GatewayPort.GyantalMain, BrokerConnectionClientID = GatewayClientID.LocalTws1 };
        // m_gateways = new List<Gateway>() {gateway1};
        // m_mainGateway = gateway1;

        // Option3: all gateways are attached to remote or local servers. To Debug vBroker trading
        var (hostIpCm, gwClientIdCm) = GatewayExtensions.GetHostIpAndGatewayClientID(GatewayId.CharmatMain);
        var (hostIpDm, gwClientIdDm) = GatewayExtensions.GetHostIpAndGatewayClientID(GatewayId.DeBlanzacMain);
        var (hostIpGm, gwClientIdGm) = GatewayExtensions.GetHostIpAndGatewayClientID(GatewayId.GyantalMain);
        Gateway gateway1 = new(GatewayId.CharmatMain, p_accountMaxTradeValueInCurrency: 600000, p_accountMaxEstimatedValueSumRecentlyAllowed: 10) { VbAccountsList = "U988767", Host = hostIpCm, SocketPort = (int)GatewayPort.CharmatMain, SuggestedIbConnectionClientID = (int)gwClientIdCm };
        Gateway gateway2 = new(GatewayId.DeBlanzacMain, p_accountMaxTradeValueInCurrency: 1.0 /* don't trade here */, p_accountMaxEstimatedValueSumRecentlyAllowed: 10) { VbAccountsList = "U1146158", Host = hostIpDm, SocketPort = (int)GatewayPort.DeBlanzacMain, SuggestedIbConnectionClientID = (int)gwClientIdDm };
        Gateway gateway3 = new(GatewayId.GyantalMain, p_accountMaxTradeValueInCurrency: 100000 /* UberVXX is 12K, 2xleveraged=24K, double=48K*/, p_accountMaxEstimatedValueSumRecentlyAllowed: 160000) { VbAccountsList = "U407941", Host = hostIpGm, SocketPort = (int)GatewayPort.GyantalMain, SuggestedIbConnectionClientID = (int)gwClientIdGm };
        m_gateways = new List<Gateway>() { gateway1, gateway2, gateway3 };
        m_mainGateway = gateway1;
    }

    public bool GatewayReconnect(GatewayId p_gatewayId)
    {
        var gateway = m_gateways.FirstOrDefault(r => r.GatewayId == p_gatewayId);
        if (gateway == null)
            return false;
        if (gateway.IsConnected)
            return true;

        bool isOK = gateway.Reconnect();
        bool connectedNow = gateway.IsConnected; // better to double check this way. It will call the IbWrapper.IsConnected again to double check.
        Utils.Logger.Info($"GatewayId: '{gateway.GatewayId}' IsConnected: {connectedNow}");

        if (gateway == m_mainGateway && connectedNow) // if this is the first time mainGateway connected after being dead
            MainGatewayJustConnected();

        return connectedNow;
    }

    public bool IsGatewayConnected(GatewayId p_gatewayId)
    {
        var gateway = m_gateways.FirstOrDefault(r => r.GatewayId == p_gatewayId);
        if (gateway == null)
            return false;
        return gateway.IsConnected;
    }

    public void ScheduleReconnectTimer()
    {
        m_reconnectTimer = new System.Threading.Timer(new TimerCallback(ReconnectToGatewaysTimer_Elapsed), null, TimeSpan.FromMinutes(cReconnectTimerFrequencyMinutes), TimeSpan.FromMinutes(cReconnectTimerFrequencyMinutes));
    }

    private void ReconnectToGatewaysTimer_Elapsed(object? p_stateObj) // Timer is coming on a ThreadPool thread
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
            if (!isMainGatewayConnectedBefore && isMainGatewayConnectedNow) // if this is the first time mainGateway connected after being dead
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
            if (IgnoreErrorsBasedOnMarketTradingTime(offsetToOpenMin: -60)) // ignore errors only before 8:30, instead of 9:30 OpenTime
                return; // skip processing the error further. Don't send it to HealthMonitor.

            // It can happen if somebody manually closed TWS on MTS and restarted it.
            // But don't ignore for all gateways. It can be important for 'some' gateways, because SqCore server can do live trading.

            // Also in the future: check IsCriticalTradingTime() usage AND
            // write a service that runs at every CriticalTradingTime starts, and checks that the TradeableGatewayIds are connected
            // and That should send the HealthMonitor warning, not this
            HealthMonitorMessage.SendAsync($"ReconnectToGatewaysTimer() tried to connect to not connected gateways (3x, 10sec sleep). Still not connected gateways {notConnectedGateways}", HealthMonitorMessageID.SqCoreWebCsError).TurnAsyncToSyncTask();
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
            m_mainGateway.BrokerWrapper.ReqMktDataStream(VBrokerUtils.MakeStockContract("VXX"));
            m_mainGateway.BrokerWrapper.ReqMktDataStream(VBrokerUtils.MakeStockContract("SVXY"));
            // m_mainGateway.BrokerWrapper.ReqMktDataStream(new Contract() { Symbol = "SPY", SecType = "STK", Currency = "USD", Exchange = "SMART" }); // for TotM forecast, but it is not needed just yet

            // for HarryLong
            m_mainGateway.BrokerWrapper.ReqMktDataStream(VBrokerUtils.MakeStockContract("TQQQ"));
            m_mainGateway.BrokerWrapper.ReqMktDataStream(VBrokerUtils.MakeStockContract("TMV"));
            m_mainGateway.BrokerWrapper.ReqMktDataStream(VBrokerUtils.MakeStockContract("VXZ"));
            m_mainGateway.BrokerWrapper.ReqMktDataStream(VBrokerUtils.MakeStockContract("SCO"));  // 2020-04-02: use SCO (2x); instead of short USO (1x), short UWT (-3x) was used, but it was delisted, because it went to penny stock
            m_mainGateway.BrokerWrapper.ReqMktDataStream(VBrokerUtils.MakeStockContract("UNG"));

            m_mainGateway.BrokerWrapper.ReqMktDataStream(VBrokerUtils.MakeStockContract("TMF")); // Can be commented out: TMF (3x) is for Agy,
            m_mainGateway.BrokerWrapper.ReqMktDataStream(VBrokerUtils.MakeStockContract("USO")); // Can be commented out: Agy uses partial SCO, partial USO for diversifying and because SCO is not a good tracker of USO

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
        Dispose(disposing: true);   // dispose m_reconnectTimer, so timer callback is not called any more
        foreach (var gateway in m_gateways)
        {
            gateway.Exit();
        }
        // PersistedState.Save();
        // StopTcpMessageListener();
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
        DateTime timeUtc = DateTime.UtcNow;
        DateTime timeEt = Utils.ConvertTimeFromUtcToEt(timeUtc);
        if (timeEt.DayOfWeek == DayOfWeek.Saturday || timeEt.DayOfWeek == DayOfWeek.Sunday) // if it is the weekend => no Error
            return true;

        TimeSpan timeTodayEt = timeEt - timeEt.Date;
        // The NYSE and NYSE MKT are open from Monday through Friday 9:30 a.m. to 4:00 p.m. ET.
        // "Gateways are not connected" errors handled with more strictness. We expect that there is a connection to IBGateway at least 1 hour before open. At 8:30.
        if (timeTodayEt.TotalMinutes < 9 * 60 + 29 + offsetToOpenMin) // ignore errors before 9:30.
            return true;   // if it is not Approximately around market hours => no Error

        if (timeTodayEt.TotalMinutes > 16 * 60 + offsetToCloseMin) // IB: not executed shorting trades are cancelled 30min after market close. Monitor errors only until that.
            return true;   // if it is not Approximately around market hours => no Error

        // TODO: <not too important> you can skip holiday days too later; and use real trading hours, which sometimes are shortened, before or after holidays.
        return false;
    }

    public static bool IsCriticalTradingTime(GatewayId p_gatewayId, DateTime p_timeUtc)
    {
        DateTime timeEt = Utils.ConvertTimeFromUtcToEt(p_timeUtc);
        if (timeEt.DayOfWeek == DayOfWeek.Saturday || timeEt.DayOfWeek == DayOfWeek.Sunday) // quick check: if it is the weekend => not critical time.
            return false;

        bool isMarketHoursValid = Utils.DetermineUsaMarketTradingHours(p_timeUtc, out bool isMarketTradingDay, out DateTime marketOpenTimeUtc, out DateTime marketCloseTimeUtc, TimeSpan.FromDays(3));
        if (isMarketHoursValid && !isMarketTradingDay)
            return false;

        foreach (var critTradingRange in GatewayExtensions.CriticalTradingPeriods)
        {
            if (critTradingRange.GatewayId != p_gatewayId)
                continue;

            if (!isMarketHoursValid)
                return true;    // Caution: if DetermineUsaMarketTradingHours() failed, better to report that we are in a critical period.

            DateTime critPeriodStartUtc = DateTime.MinValue; // Caution:
            if (critTradingRange.RelativeTimePeriod.Start.Base == RelativeTimeBase.BaseOnUsaMarketOpen)
                critPeriodStartUtc = marketOpenTimeUtc + critTradingRange.RelativeTimePeriod.Start.TimeOffset;
            else if (critTradingRange.RelativeTimePeriod.Start.Base == RelativeTimeBase.BaseOnUsaMarketClose)
                critPeriodStartUtc = marketCloseTimeUtc + critTradingRange.RelativeTimePeriod.Start.TimeOffset;

            DateTime critPeriodEndUtc = DateTime.MaxValue;
            if (critTradingRange.RelativeTimePeriod.End.Base == RelativeTimeBase.BaseOnUsaMarketOpen)
                critPeriodEndUtc = marketOpenTimeUtc + critTradingRange.RelativeTimePeriod.End.TimeOffset;
            else if (critTradingRange.RelativeTimePeriod.End.Base == RelativeTimeBase.BaseOnUsaMarketClose)
                critPeriodEndUtc = marketCloseTimeUtc + critTradingRange.RelativeTimePeriod.End.TimeOffset;

            if (critPeriodStartUtc <= p_timeUtc && p_timeUtc <= critPeriodEndUtc) // if p_timeUtc is between [Start, End]
                return true;
        }

        return false;
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
        m_mainGateway?.BrokerWrapper.GetAlreadyStreamedPrice(p_contract, ref rtPrices);
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

    protected virtual void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            if (disposing)
            {
                // TODO: dispose managed state (managed objects)
                m_reconnectTimer?.Dispose();
                m_reconnectTimer = null;
                m_mainGateway?.Dispose();
                m_mainGateway = null;
            }

            // TODO: free unmanaged resources (unmanaged objects) and override finalizer
            // TODO: set large fields to null
            disposedValue = true;
        }
    }

    // // TODO: override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
    // ~BrokersWatcher()
    // {
    //     // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
    //     Dispose(disposing: false);
    // }

    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}