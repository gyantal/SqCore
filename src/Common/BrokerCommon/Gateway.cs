using SqCommon;
using IBApi;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Utils = SqCommon.Utils;
using System.Text;

namespace BrokerCommon
{
    // GateWayId is not User. DC user has 2 Gateways: Main, DeBlanzac. But same user.
    public enum GatewayId { Unknown, Demo, GyantalMain, GyantalSecondary, GyantalPaper, CharmatMain, CharmatSecondary, CharmatPaper, CharmatWifeMain, CharmatWifeSecondary, CharmatWifePaper, DeBlanzacMain, DeBlanzacSecondary, TuMain, TuSecondary }
    public enum GatewayPort : int { None, Demo, GyantalMain = 7301, VbSrvGyantalSecondary = 7301, GyantalPaper, SqCoreSrvCharmatMain = 7303, CharmatSecondary = 7303, CharmatPaper, CharmatWifeMain, CharmatWifeSecondary, CharmatWifePaper, SqCoreSrvDeBlanzacMain = 7308, DeBlanzacSecondary, TuMain = 7304, TuSecondary = 7304 }
    public enum GatewayClientID : int
    {
        None,
        // used in SqLab, obsolete, delete them later
        // SqLabAtsToGa = 41,
        // SqLabAtsToDc = 42,
        // SqLabMtsToDc = 142,
        // SqLabMtsToDb = 144,
        LocalTws1 = 50,
        LocalTws2 = 51,

        // https://github.com/pilwon/node-ib/issues/27
        // https://groups.io/g/twsapi/topic/4044575
        // leave at least 3 ClientIDs between each of them, so if retry the connection with the next consecutive id
        // If the clientId is not ultimately freed up you will eventually hit the maximum number of TWS clients.
        // The only cure for that is to restart TWS.  There is no mechanism I'm aware of to close client connections from within TWS.
        SqCoreToGaProd = 60,
        SqCoreToGaDev1 = 63,    // Agy
        SqCoreToGaDev2 = 66,    // Balazs
        SqCoreToGaDev3 = 69,   // Laci
        SqCoreToGaDev4 = 72,    // Daya
        SqCoreToGaTest1 = 75,    // it can clash if many developers run the tests at the same time, but that is not likely.

        SqCoreToDcProd = 80,
        SqCoreToDcDev1 = 83,
        SqCoreToDcDev2 = 86,
        SqCoreToDcDev3 = 89,
        SqCoreToDcDev4 = 92,
        SqCoreToDcTest1 = 95,

        SqCoreToDbProd = 100,
        SqCoreToDbDev1 = 103,
        SqCoreToDbDev2 = 106,
        SqCoreToDbDev3 = 109,
        SqCoreToDbDev4 = 112,
        SqCoreToDbTest1 = 115
    }


    public class GatewayTradingPeriod {
        public GatewayId GatewayId { get; set; } = GatewayId.Unknown;
        public RelativeTimePeriod RelativeTimePeriod { get; set; } = new RelativeTimePeriod();
    }

    public static class GatewayExtensions
    {
        public static Dictionary<string, GatewayId> NavSymbol2GatewayId = new() { 
            {"GA.IM", GatewayId.GyantalMain}, {"DC.IM", GatewayId.CharmatMain}, {"DC.ID", GatewayId.DeBlanzacMain}};

        public static Dictionary<string, List<GatewayId>> NavSqSymbol2GatewayIds = new() { 
            {"N/GA.IM", new List<GatewayId>() {GatewayId.GyantalMain}}, 
            {"N/DC.IM", new List<GatewayId>() {GatewayId.CharmatMain}}, 
            {"N/DC.ID", new List<GatewayId>() {GatewayId.DeBlanzacMain}},
            {"N/DC", new List<GatewayId>() {GatewayId.CharmatMain, GatewayId.DeBlanzacMain}}};

        // Need different IP for different platforms, and different GwClientID for different developers (if they happen to code and connect to IbGateways at the same time)
        public static (string HostIp, GatewayClientID GwClientID) GetHostIpAndGatewayClientID(GatewayId p_gatewayId)
        {
            switch (Utils.RunningPlatform())
            {
                case Platform.Linux:
                    return p_gatewayId switch
                    {
                        GatewayId.CharmatMain => (ServerIp.LocalhostLoopbackWithIP, GatewayClientID.SqCoreToDcProd),
                        GatewayId.DeBlanzacMain => (ServerIp.LocalhostLoopbackWithIP, GatewayClientID.SqCoreToDbProd),
                        GatewayId.GyantalMain => (ServerIp.AtsVirtualBrokerServerPublicIpForClients, GatewayClientID.SqCoreToGaProd),
                        _ => throw new NotImplementedException()
                    };

                case Platform.Windows:
                    // find out which user from the team and determine it accordingly. Or just check whether folders exists (but that takes HDD read, which is slow)
                    switch (Environment.UserName)   // Windows user name
                    {
                        case "gyantal":
                        case "gyant":
                            return p_gatewayId switch
                            {
                                GatewayId.CharmatMain => (ServerIp.SqCoreServerPublicIpForClients, GatewayClientID.SqCoreToDcDev1),
                                GatewayId.DeBlanzacMain => (ServerIp.SqCoreServerPublicIpForClients, GatewayClientID.SqCoreToDbDev1),
                                GatewayId.GyantalMain => (ServerIp.AtsVirtualBrokerServerPublicIpForClients, GatewayClientID.SqCoreToGaDev1),
                                _ => throw new NotImplementedException()
                            };
                        case "Balazs":
                            return p_gatewayId switch
                            {
                                GatewayId.CharmatMain => (ServerIp.SqCoreServerPublicIpForClients, GatewayClientID.SqCoreToDcDev2),
                                GatewayId.DeBlanzacMain => (ServerIp.SqCoreServerPublicIpForClients, GatewayClientID.SqCoreToDbDev2),
                                GatewayId.GyantalMain => (ServerIp.AtsVirtualBrokerServerPublicIpForClients, GatewayClientID.SqCoreToGaDev2),
                                _ => throw new NotImplementedException()
                            };
                        case "Laci":
                            return p_gatewayId switch
                            {
                                GatewayId.CharmatMain => (ServerIp.SqCoreServerPublicIpForClients, GatewayClientID.SqCoreToDcDev3),
                                GatewayId.DeBlanzacMain => (ServerIp.SqCoreServerPublicIpForClients, GatewayClientID.SqCoreToDbDev3),
                                GatewayId.GyantalMain => (ServerIp.AtsVirtualBrokerServerPublicIpForClients, GatewayClientID.SqCoreToGaDev3),
                                _ => throw new NotImplementedException()
                            };
                        case "vinci":
                            return p_gatewayId switch
                            {
                                GatewayId.CharmatMain => (ServerIp.SqCoreServerPublicIpForClients, GatewayClientID.SqCoreToDcDev4),
                                GatewayId.DeBlanzacMain => (ServerIp.SqCoreServerPublicIpForClients, GatewayClientID.SqCoreToDbDev4),
                                GatewayId.GyantalMain => (ServerIp.AtsVirtualBrokerServerPublicIpForClients, GatewayClientID.SqCoreToGaDev4),
                                _ => throw new NotImplementedException()
                            };
                        default:
                            throw new Exception("Windows user name is not recognized. Add your username and folder here!");
                    }
                default:
                    throw new Exception("RunningPlatform() is not recognized");
            }
        }


        // CriticalTrading times when IbGateway connections should be checked, and during which time disconnections are not allowed and generate HealthMonitor error
        // One Gateway can have many small critical periods
        public static List<GatewayTradingPeriod> CriticalTradingPeriods = new() {
            new GatewayTradingPeriod() {
                GatewayId = GatewayId.GyantalMain,
                RelativeTimePeriod = new RelativeTimePeriod() {    // for testing: from 20min before Open to 5min after Close
                    Start = new RelativeTime() { Base = RelativeTimeBase.BaseOnUsaMarketOpen, TimeOffset = TimeSpan.FromMinutes(-20) },
                    End = new RelativeTime() { Base = RelativeTimeBase.BaseOnUsaMarketClose, TimeOffset = TimeSpan.FromMinutes(5) } }
            },
            new GatewayTradingPeriod() {
                GatewayId = GatewayId.CharmatMain,
                RelativeTimePeriod = new RelativeTimePeriod() {    // realistic: 10min before Open to 35min after Open.
                    Start = new RelativeTime() { Base = RelativeTimeBase.BaseOnUsaMarketOpen, TimeOffset = TimeSpan.FromMinutes(-10) },
                    End = new RelativeTime() { Base = RelativeTimeBase.BaseOnUsaMarketOpen, TimeOffset = TimeSpan.FromMinutes(35) } }
            },
            new GatewayTradingPeriod() {
                GatewayId = GatewayId.CharmatMain,
                RelativeTimePeriod = new RelativeTimePeriod() {    // realistic: 1h before Close to 5min after Close. Simulations run 30 min before Close.
                    Start = new RelativeTime() { Base = RelativeTimeBase.BaseOnUsaMarketClose, TimeOffset = TimeSpan.FromMinutes(-60) },
                    End = new RelativeTime() { Base = RelativeTimeBase.BaseOnUsaMarketClose, TimeOffset = TimeSpan.FromMinutes(5) } }
            }
         };


        public static string ToShortFriendlyString(this GatewayId me)
        {
            switch (me)
            {
                case GatewayId.Unknown:
                    return "None";
                case GatewayId.GyantalMain:
                case GatewayId.GyantalSecondary:
                    return "G";
                case GatewayId.CharmatMain:
                case GatewayId.CharmatSecondary:
                    return "DC";
                case GatewayId.TuMain:
                case GatewayId.TuSecondary:
                    return "T";
                case GatewayId.DeBlanzacMain:
                case GatewayId.DeBlanzacSecondary:
                    return "DB";
                default:
                    return "ERR";
            }
        }
    }

    public class VirtualOrder
    {
        public int OrderId { get; set; }
        public DateTime SubmitTime { get; set; }
        public double EstimatedUsdSize { get; set; }
        public List<int>? RealOrderIds { get; set; }     // sometimes, VirtualOrder is MinFilterSkipped, so there is no RealOrders. Other times one virtual order is executed with many real orders. Like gradually decreasing the Limit price because we were only half-filled
        public OrderExecution OrderExecution { get; set; }      // MKT, MOC
        public OrderStatus OrderStatus { get; set; }
    }

    public class RealOrder
    {
        public int OrderId { get; set; }
        public List<int> VirtualOrderIds { get; set; }  // sometimes one real order has many virtual orders. If one order is Short 10 SPY, other order is long 7 SPY, than the one real order will be short 3 SPY
    
        public RealOrder(int orderId, List<int> virtualOrderIds)
        {
            OrderId = orderId;
            VirtualOrderIds = virtualOrderIds;
        }
    }

    public partial class Gateway
    {
        public GatewayId GatewayId { get;  }
        public string VbAccountsList { get; set; } = string.Empty;
        public string Host { get; set; } = string.Empty;
        public int SocketPort { get; set; }
        public int SuggestedIbConnectionClientID { get; set; } = (int)GatewayClientID.None;
        public int RealIbConnectionClientID { get; set; } = (int)GatewayClientID.None;
        public bool IsConnected
        {
            get
            {
                return BrokerWrapper.IsConnected();
            }
        }

        public IBrokerWrapper BrokerWrapper { get; set; }   // Need a wrapper around the concrete broker:  IB, TS or others. Only very few Gateways are defined. Better to not let it be ? null, and check at every execution. Define a mockup YF-broker and initiate object with that.

        int m_virtualOrderIDseed = 1000;
        protected int GetUniqueVirtualOrderID
        {
            get { return Interlocked.Increment(ref m_virtualOrderIDseed); }  // Increment gives back the incremented value, not the old value
        }
        public List<VirtualOrder> VirtualOrders { get; set; } = new List<VirtualOrder>();
        public List<RealOrder> RealOrders { get; set; } = new List<RealOrder>();


        double m_allowedMinUsdTransactionValue = 500.0; // increased from $200 to $500
        public double AllowedMinUsdTransactionValue
        {
            get { return m_allowedMinUsdTransactionValue; }
            set { m_allowedMinUsdTransactionValue = value; }
        }

        double m_IbAccountMaxTradeValueInCurrency = 20000.0;   // default $20K for Agy (because sometimes we play double leverage), so expect 10K trades max. But there will be an email warning at 20K/1.5=13.3K. So if trade size is 13.3K => email is sent to warn to increase it.
        public double IbAccountMaxTradeValueInCurrency
        {
            get { return m_IbAccountMaxTradeValueInCurrency; }
            set { m_IbAccountMaxTradeValueInCurrency = value; }
        }

        double m_IbAccountMaxEstimatedValueSumRecentlyAllowed = 50000.0;  // default $50K for Agy
        public double IbAccountMaxEstimatedValueSumRecentlyAllowed
        {
            get { return m_IbAccountMaxEstimatedValueSumRecentlyAllowed; }
            set { m_IbAccountMaxEstimatedValueSumRecentlyAllowed = value; }
        }

        int m_maxNOrdersRecentlyAllowed = 20;
        public int MaxNOrdersRecentlyAllowed
        {
            get { return m_maxNOrdersRecentlyAllowed; }
            set { m_maxNOrdersRecentlyAllowed = value; }
        }

        public Gateway(GatewayId p_gatewayUser, double p_accountMaxTradeValueInCurrency = Double.NaN, double p_accountMaxEstimatedValueSumRecentlyAllowed = Double.NaN)   // gateWayUser will be fixed. We don't allow to change it later.
        {
            GatewayId = p_gatewayUser;
            if (!Double.IsNaN(p_accountMaxTradeValueInCurrency))
                m_IbAccountMaxTradeValueInCurrency = p_accountMaxTradeValueInCurrency;
            if (!Double.IsNaN(p_accountMaxEstimatedValueSumRecentlyAllowed))
                m_IbAccountMaxEstimatedValueSumRecentlyAllowed = p_accountMaxEstimatedValueSumRecentlyAllowed;

            BrokerWrapper = new BrokerWrapperYF();  // just a mock-up
        }

        public bool Reconnect()
        {
            int nMaxRetry = 3;
            int nConnectionRetry = 0;
            do
            {
                try
                {
                    // (Sometimes, but not always) if we stop abruptly the Debugging mode, then next time the client application tries to connect to TWS with the same id, 
                    // the connection is rejected, because TWS didn't notice that the old connection was broken. TWS promised to fix this and eventually close the broken connection.
                    // Error is something like this: Unable connect as the client id is already in use. Retry with a unique client id.
                    // Other times, it is perfectly OK to abruptly stop the client with Debug-termination. When I wanted I couldn't reproduce the error.
                    // because we left at least 3 ClientIDs between each of them, so we retry the connection with the next consecutive id
                    // If the clientId is not ultimately freed up you will eventually hit the maximum number of TWS clients.
                    // The only cure for that is to restart TWS.  There is no mechanism I'm aware of to close client connections from within TWS.
                    RealIbConnectionClientID = SuggestedIbConnectionClientID + nConnectionRetry;
                    nConnectionRetry++;
                    Console.WriteLine($"Connecting to IB {GatewayId} on {Host}:{SocketPort} with ClientID:{(int)RealIbConnectionClientID}...");
                    IBrokerWrapper ibWrapper;
                    if (Utils.RunningPlatform() == Platform.Linux)
                    {
                        ibWrapper = new BrokerWrapperIb(AccSumArrived, AccSumEnd, AccPosArrived, AccPosEnd);      // recreate IB wrapper at every reConnection. Safer this way.
                    }
                    else
                    {
                        bool isPreferIbAlltime = true;  // isPreferIbAlltime is used in general for functionality (RT price) development, but !isPreferIbAlltime is better for strategy developing (maybe)
                        if (isPreferIbAlltime || Utils.IsInRegularUsaTradingHoursNow())
                            ibWrapper = new BrokerWrapperIb(AccSumArrived, AccSumEnd, AccPosArrived, AccPosEnd);    // when isPreferIbAlltime or when !isPreferIbAlltime, but USA market is open
                        else
                            ibWrapper = new BrokerWrapperIb(AccSumArrived, AccSumEnd, AccPosArrived, AccPosEnd);
                            // ibWrapper = new BrokerWrapperYF();     // Before market open, or After market close. Simulated real time price is needed to determine current portfolio $size.
                    }
                    if (!ibWrapper.Connect(GatewayId, Host, SocketPort, RealIbConnectionClientID))
                    {
                        Utils.Logger.Info($"No connection to IB {GatewayId} on {Host}:{SocketPort}. Trials: {nConnectionRetry}/{nMaxRetry}");
                        if (nConnectionRetry == nMaxRetry)
                            SqConsole.WriteLine($"No connection to IB {GatewayId}. Trials: {nConnectionRetry}/{nMaxRetry}");
                        continue;
                    }
                    // we are connected succesfully
                    string str1 = ibWrapper!.IbAccountsList;
                    string str2 = VbAccountsList;
                    string msg = $"Expected IbAccount {VbAccountsList} is not found: { ibWrapper.IbAccountsList}.";
                    StrongAssert.Equal(str1, str2, Severity.ThrowException, msg);

                    // after this line, we are really connected
                    BrokerWrapper = ibWrapper;

                    string warnMessage = (ibWrapper is BrokerWrapperIb) ? "" : "!!!WARNING. Fake Broker (YF!). ";
                    Utils.Logger.Info($"{warnMessage}Gateway {ibWrapper} is connected. User {GatewayId} acc {VbAccountsList}.");
                    SqConsole.WriteLine($"{warnMessage}Gateway {GatewayId} acc {VbAccountsList} connected.");
                    
                    // TEMP here just for double checking that connection is right... Can be removed later, because we don't want to hold up the connections of other IBservers.
                    // List<BrAccSum>? accSums = GetAccountSums(); // takes 144ms
                    // if (accSums != null)
                    // {
                    //     string navStr = accSums.First(r => r.Tag == AccountSummaryTags.NetLiquidation).Value;
                    //     Utils.Logger.Info($"Gateway {GatewayId}'s NAV: {navStr}");
                    //     Console.WriteLine($"Gateway {GatewayId}'s NAV: {navStr}");
                    // }
                    return true;
                }
                catch (Exception e)
                {
                    //If IBGateways doesn't connect: Retry the connection about 3 times, before Exception. So, so this problem is an Expected problem if another try to reconnect solves it.
                    Utils.Logger.Info(e, $"Exception in ReconnectToGateway()-user:{GatewayId}: nRetry:{nConnectionRetry} : Msg:{e.Message}");
                    if (nConnectionRetry >= nMaxRetry)
                    {
                        Utils.Logger.Info("GatewaysWatcher:ReconnectToGateway(). This gateway failed after many retries. We could send HealthMonitor message here, but better at a higher level if the second Gateway fails too.");
                        //HealthMonitorMessage.SendException($"ReConnectToGateway Thread: nMaxRetry: {nMaxRetry}", e, HealthMonitorMessageID.ReportErrorFromVirtualBroker);  // the higher level ReconnectToGateways() will send the Error to HealthMonitor
                        return false;
                    }
                    else
                    {
                        // if we do retry, wait 10 seconds here. Maybe IB Gateway is will reconnect later
                        Thread.Sleep(10000);
                    }
                }
            } while (nConnectionRetry < nMaxRetry);
            return false;
        }

        public void Disconnect()
        {
            if (BrokerWrapper == null)
                return;
            BrokerWrapper.Disconnect();
        }

        public void ServerDiagnostic(StringBuilder p_sb)
        {
            p_sb.AppendLine($"GatewayId: {GatewayId}, IsConnected: {IsConnected}<br>");
        }

        // PlayTransactionsViaBroker(). Consider these use-cases:
        // ******* 1. Problem: if VBroker1 is LONG IWM, VBroker2 is Short IWM at MOC, it cancels out each other(with Minfilter), there is no real life trade, which is OK.
        // However, there were 2 virtual transactions, that Gateway should give back, because VBroker1 and VBroker2 wants to write something to the Portfolio in the DB.
        //+Solution:
        //    - PlayTransactionsViaBroker() always gives back the prices (except in simulation mode); (even if minFilter was applied), Because we need it for the DB. If minfilter was applied, it gives back Volume = 0; showing it was not executed
        //    - PlayTransactionsViaBroker() signals whether an portfolioItem was executed or not (small are not executed) (in a flag or somewhere); it signals by changing the Volume to 0; (minFilter or maxFilter was applied)
        //    - even if an item is not executed, the BrokerTask can write them into the Portfolios (it is his own decision)
        //        + IF	there is at last 1 sub-strategy, in which this is significant amount enough (non-negligible) (we need per strategy minFilter values too; call it significancy; so don't confuse with TradeMin)
        //        + IF it is written into at least 1 sub-strategy portfolio, then All other sub-strategy portfolios are updated with the 'minor/manor' trades
        //        + invariant: keep real/simulated trade values equal; means: if +1 was traded, the aggregate written into DB should be 1 as well
        //        for example; there is 51 substrategy. One shorts 101 IWM, other 50 buys 2 IWM each. The aggregateIntent is short 1. The aggregateReal is 0 (minFilter skip).
        //        We have to update the virtual portfolios: but the aggregateSimulated should be 0 too. (that is the invariant)


        // ******* 2. LimitOrder execution policy:
        //- Determine targetPrice = MidPrice = (Ask+Bid)/2 
        //- convert it to 2 decimals (the smallest unit is 1 penny in the Exchange)
        //- send the order to the Broker with this targetPrice;
        //> Repeat from here
        //    - wait 10 seconds
        //    - cancel the order
        //    - wait some seconds until the Cancelation is confirmed
        //        - sometimes the cancellation fails. In that case, probably the original order were executed; handle it as completed;
        //        after calling cancelOrder(...) you must wait for it to be acknowledged i.e. orderStatus(...) returns with status=="Cancelled" or perhaps status=="Filled" and go from there. 

        //    if Repeat = 5, don't chase it further
        //        break;

        //    Method 1: (implemented)
        //    If the TargetPrice is < 100
        //        - add an extra penny to the target price (if it is a Buy order or Cover order)
        //        - OR subtract an extra penny to the target price (if it is a Sell or Short order)

        //    If the targetPrice is in [100.. 200]
        //        - add an extra 0.02 to the target price (if it is a Buy order or Cover order)
        //        - OR subtract an extra 0.02 to the target price (if it is a Sell or Short order)

        //    If the targetPrice is in [200.. ]
        //        - add an extra 0.05 to the target price (if it is a Buy order or Cover order)
        //        - OR subtract an extra 0.05 to the target price (if it is a Sell or Short order)

        //    Method 2: (Laszlo's idea: not yet implemented)
        //    - query the CurrentMidPrice (askBidprice) again (not based on previus) and set the targetPrice = BidPrice 
        //    this one is good in case of the Exploding stocks; that moves up 1% every 10 seconds (adding 1 penny cannot catch them)

        //    - insert the new order with the new targetPrice
        //> Repeat unti here


        // 3. ***** MOC orders: before Placing them, check current Open MOC orders
        // We can subscribe to all the current OrdersInfo. For MOC orders for example, before PlaceOrder() we should check that if there is already an MOC order then we Modify that (or Cancel&Recreate)

        // 4. *****  Glitch protections:
        //+ check the MaxTradeSize of the bxml file. : MaxTradeUsdValue
        //+ check the number of Trades in the last 10 minutes < 20; After that, stop everything.
        //+ check the $volume of those trades based on last day price (SQ DB SQL usage).; it should be < 1$ Mil in the last 10 minutes

        internal int PlaceOrder(double p_portfolioMaxTradeValueInCurrency, double p_portfolioMinTradeValueInCurrency, Contract p_contract, TransactionType p_transactionType, double p_volume, OrderExecution p_orderExecution, OrderTimeInForce p_orderTif, double? p_limitPrice, double? p_stopPrice, double p_estimatedPrice, bool p_isSimulatedTrades, double p_oldVolume, StringBuilder p_detailedReportSb)
        {
            // 1. Glitch protections
            int virtualOrderID = GetUniqueVirtualOrderID;
            if (double.IsNaN(p_estimatedPrice) || Math.Abs(p_estimatedPrice) < 0.000)
            {   // we want HealthMonitor to be notified with StrongAssert, but continue with other orders
                Utils.Logger.Error("PlaceOrder(): 'double.IsNaN(p_estimatedPrice) || Math.Abs(p_estimatedPrice) < 0.001' failed. Not Safe to do the trade without estimating its value. Crash here.");
                return virtualOrderID;      // try to continue with other PlaceOrders()
            }
            double estimatedTransactionValue = p_volume * p_estimatedPrice;
            double maxAllowedTradeValue = IbAccountMaxTradeValueInCurrency;
            if (p_portfolioMaxTradeValueInCurrency < maxAllowedTradeValue)
                maxAllowedTradeValue = p_portfolioMaxTradeValueInCurrency;
            if (estimatedTransactionValue > maxAllowedTradeValue)  // if maxFilter is breached, safer to not trade at all. Even if it is an MOC order and can be grouped with other orders. Safer this way.
            {
                string errStr = $"Warning. MaxFilter is breached. Transaction is MaxFilter (IbAccountMax: ${IbAccountMaxTradeValueInCurrency:N0}, PortfolioMax: ${p_portfolioMaxTradeValueInCurrency:N0}) skipped: {p_contract.Symbol} {p_volume:N0}: $ {estimatedTransactionValue:N0}";
                ColorConsole.WriteLine(ConsoleColor.Red, true, errStr);
                Utils.Logger.Warn(errStr);
                return virtualOrderID;
            }
            if (estimatedTransactionValue > (maxAllowedTradeValue/1.5))
            {
                //>When simulated order is 50% range of MaxTradeable, send a warning email to administrator to modify it in VBroker and redeploy. It is not likely that a  strategy will move 50% per day.
                string warnStr = $"Warning. Trade will commence, but MaxFilter is 50% away to be breached. Modify MaxTradeValue params (IbAccount or Portfolio) in VBroker and redeploy. Transaction is MaxFilter (IbAccountMax: ${IbAccountMaxTradeValueInCurrency:N0}, PortfolioMax: ${p_portfolioMaxTradeValueInCurrency:N0}). Trade: {p_contract.Symbol} {p_volume:N0}: $ {estimatedTransactionValue:N0}";
                StrongAssert.Fail(Severity.NoException, warnStr);
            }

            if (estimatedTransactionValue < p_portfolioMinTradeValueInCurrency)
            {
                Utils.Logger.Info("p_portfolioMinTradeValueInCurrency usage is not implemented yet.");
            }

            DateTime utcNow = DateTime.UtcNow;
            int nLatestOrders = 0;
            double estimatedUsdSizeSumRecently = 0.0;
            lock (VirtualOrders)   // safety checks
            {
                foreach (var latestOrder in VirtualOrders.Where(r => (utcNow - r.SubmitTime).TotalMinutes < 10.0))
                {
                    nLatestOrders++;
                    estimatedUsdSizeSumRecently += latestOrder.EstimatedUsdSize;
                }
            }
            if (nLatestOrders >= m_maxNOrdersRecentlyAllowed)
            {
                Utils.Logger.Error($"nLatestOrders >= m_maxNOrdersRecentlyAllowed. This is for your protection. Transaction {p_contract.Symbol} is skipped. Set Settings to allow more.");
                return virtualOrderID;      // try to continue with other PlaceOrders()
            }
            if (estimatedUsdSizeSumRecently >= m_IbAccountMaxEstimatedValueSumRecentlyAllowed)
            {
                Utils.Logger.Error($"estimatedUsdSizeSum >= m_maxEstimatedUsdSumRecentlyAllowed. This is for your protection. Transaction {p_contract.Symbol} is skipped. Set Settings to allow more.");
                return virtualOrderID;      // try to continue with other PlaceOrders()
            }

            // 2. Execute different orderTypes MKT, MOC
            // vbOrderId and ibOrderID is different, because some Limit orders, or other tricky orders are executed in real life by many concrete IB orders
            // or there are two opposite vbOrder that cancels each other out at MOC, therefore there is no ibOrder at all
            double transactionOfOldVolumePct = (p_oldVolume == 0) ? 1.00 : p_volume / p_oldVolume;
            string logMsg = $"{this.GatewayId.ToShortFriendlyString()}: {(p_isSimulatedTrades ? "Simulated" : "Real")} {p_transactionType} {p_volume} ({transactionOfOldVolumePct*100:F2}%) {p_contract.Symbol} (${estimatedTransactionValue:F0})";
            ColorConsole.WriteLine(null, false, logMsg);
            Utils.Logger.Info(logMsg);
            p_detailedReportSb.AppendLine(logMsg);

            if (p_orderExecution == OrderExecution.Market)
            {
                // Perform minFilter skipped here. But use estimatedTransactionValue. We get the estimatedPrice from the Main Gateway
                if (estimatedTransactionValue < AllowedMinUsdTransactionValue)
                {
                    ColorConsole.WriteLine(null, false, $"Transaction is MinFilter (${AllowedMinUsdTransactionValue:F0}) skipped: {p_contract.Symbol} {p_volume:F0}");
                    Utils.Logger.Info($"Transaction is MinFilter (${AllowedMinUsdTransactionValue:F0}) skipped: {p_contract.Symbol} {p_volume:F0}");
                    lock (VirtualOrders)
                        VirtualOrders.Add(new VirtualOrder() { OrderId = virtualOrderID, SubmitTime = DateTime.UtcNow, EstimatedUsdSize = 0.0, OrderExecution = p_orderExecution, OrderStatus = OrderStatus.MinFilterSkipped });
                    return virtualOrderID;
                }

                int ibRealOrderID = BrokerWrapper.PlaceOrder(p_contract, p_transactionType, p_volume, p_orderExecution, p_orderTif, p_limitPrice, p_stopPrice, p_estimatedPrice, p_isSimulatedTrades);
                lock (VirtualOrders)
                    VirtualOrders.Add(new VirtualOrder() { OrderId = virtualOrderID, SubmitTime = DateTime.UtcNow, EstimatedUsdSize = estimatedTransactionValue, RealOrderIds = new List<int>() { ibRealOrderID }, OrderExecution = p_orderExecution, OrderStatus = OrderStatus.Submitted, });
                lock (RealOrders)
                    RealOrders.Add(new RealOrder(ibRealOrderID, new List<int>() { virtualOrderID }));
                
            } else if (p_orderExecution == OrderExecution.MarketOnClose)
            {
                // MOC orders need more consideration. Checking current MOC orders of the gateway, modifying it, etc. and 1 realOrders can be many VirtualOrders
                // (MinFilter orders may be played here if there is already a bigger order under Excetion. However, this 'nice' feature is not necessary.)

                // 1. Get a list of current orders from BrokerGateway
                // 2. If this ticker that I want to trade is in the list, don't create a new order, but modify that one.
                // 3. Otherwise, create a new Order

                // !!! Now, we only play UWM, TWM at MOC orders, so in real life, there will be no order Clash. Later, implement it properly. Now, assume that there is no other MOC order for this stock.
                // in the future implement MOC order in a general way. One RealOrder can mean many Virtual MOC orders.

                // Perform minFilter skipped here. But use estimatedTransactionValue. We get the estimatedPrice from the Main Gateway
                if (estimatedTransactionValue < AllowedMinUsdTransactionValue)
                {
                    Utils.Logger.Warn($"Transaction is MinFilter (${AllowedMinUsdTransactionValue:F0}) skipped: {p_contract.Symbol} {p_volume:F0}");
                    lock (VirtualOrders)
                        VirtualOrders.Add(new VirtualOrder() { OrderId = virtualOrderID, SubmitTime = DateTime.UtcNow, EstimatedUsdSize = 0.0, OrderExecution = p_orderExecution, OrderStatus = OrderStatus.MinFilterSkipped });
                    return virtualOrderID;
                }

                int ibRealOrderID = BrokerWrapper.PlaceOrder(p_contract, p_transactionType, p_volume, p_orderExecution, p_orderTif, p_limitPrice, p_stopPrice, p_estimatedPrice, p_isSimulatedTrades);
                lock (VirtualOrders)
                    VirtualOrders.Add(new VirtualOrder() { OrderId = virtualOrderID, SubmitTime = DateTime.UtcNow, EstimatedUsdSize = estimatedTransactionValue, RealOrderIds = new List<int>() { ibRealOrderID }, OrderExecution = p_orderExecution, OrderStatus = OrderStatus.Submitted, });
                lock (RealOrders)
                    RealOrders.Add(new RealOrder(ibRealOrderID, new List<int>() { virtualOrderID }));

            }
            else
            {
                Utils.Logger.Error("Only the Simple Market order is implemented. MOC orders need more consideration. Checking current MOC orders of the gateway, modifying it, etc.");
            }

            return virtualOrderID;
        }

        internal bool WaitOrder(int p_virtualOrderId, bool p_isSimulatedTrades)
        {
            VirtualOrder? virtualOrder = null;
            lock (VirtualOrders)
            {
                virtualOrder = VirtualOrders.FirstOrDefault(r => r.OrderId == p_virtualOrderId);
            }
            if (virtualOrder == null)
            {
                Utils.Logger.Error($"Virtual orderId {p_virtualOrderId} is not found.");
                return false;
            }

            if (virtualOrder.OrderStatus == OrderStatus.MinFilterSkipped || virtualOrder.OrderStatus == OrderStatus.MaxFilterSkipped)
                return true;    // virtualOrder.RealOrderIds == null, but OK. No error. Executed succesfully. Market or MOC orders

            if (virtualOrder.OrderExecution == OrderExecution.Market)       // MinFilter and MaxFilter is only properly handled with MKT order not with MOC order
            {
                int realOrderId = virtualOrder.RealOrderIds?[0] ?? -1;  // virtualOrder.RealOrderIds is only null if OrderStatus is MinFilterSkipped or MaxFilterSkipped, but that is handled before.
                BrokerWrapper.WaitOrder(realOrderId, p_isSimulatedTrades);
            }
            else if (virtualOrder.OrderExecution == OrderExecution.MarketOnClose)
            {
                // in the future implement MOC order in a general way. One RealOrder can mean many Virtual MOC orders.
                int realOrderId = virtualOrder.RealOrderIds?[0] ?? -1;
                BrokerWrapper.WaitOrder(realOrderId, p_isSimulatedTrades);
            }

            return false;
        }

        internal bool GetVirtualOrderExecutionInfo(int p_virtualOrderId, ref OrderStatus orderStatus, ref double executedVolume, ref double executedAvgPrice, ref DateTime p_executionTime, bool p_isSimulatedTrades)
        {
            VirtualOrder? virtualOrder = null;
            lock (VirtualOrders)
            {
                virtualOrder = VirtualOrders.FirstOrDefault(r => r.OrderId == p_virtualOrderId);
            }
            if (virtualOrder == null)
            {
                Utils.Logger.Error($"Virtual orderId {p_virtualOrderId} is not found.");
                return false;
            }

            if (virtualOrder.OrderExecution == OrderExecution.Market)
            {
                if (virtualOrder.OrderStatus == OrderStatus.MinFilterSkipped || virtualOrder.OrderStatus == OrderStatus.MaxFilterSkipped)
                {
                    orderStatus = virtualOrder.OrderStatus;
                    executedVolume = 0.0;
                    executedAvgPrice = Double.NaN;
                    p_executionTime = DateTime.UtcNow;
                    return true;    // correct, expected MinFilter Skip
                }

                int realOrderId = virtualOrder.RealOrderIds?[0] ?? -1;

                // the real order may or maybe Not finished yet. If it is not filled, we will set it as Error order
                OrderStatus realOrderStatus = OrderStatus.None;
                double realExecutedVolume = Double.NaN;
                double realAvgPrice = Double.NaN;
                DateTime realExecutionTime = DateTime.MinValue;
                if (!BrokerWrapper.GetRealOrderExecutionInfo(realOrderId, ref realOrderStatus, ref realExecutedVolume, ref realAvgPrice, ref realExecutionTime, p_isSimulatedTrades))
                    return false;

                if (realOrderStatus == OrderStatus.Filled)
                {
                    orderStatus = realOrderStatus;
                    executedVolume = realExecutedVolume;
                    executedAvgPrice = realAvgPrice;
                }
                else
                {
                    orderStatus = realOrderStatus;  // return the real order status. it doesn't hurt
                    executedVolume = Double.NaN;    // better not using partially filled info, or 0.0
                    executedAvgPrice = Double.NaN;
                }

                return true;
            }
            else if (virtualOrder.OrderExecution == OrderExecution.MarketOnClose)
            {
                // in the future implement MOC order in a general way. One RealOrder can mean many Virtual MOC orders.

                if (virtualOrder.OrderStatus == OrderStatus.MinFilterSkipped || virtualOrder.OrderStatus == OrderStatus.MaxFilterSkipped)
                {
                    orderStatus = virtualOrder.OrderStatus;
                    executedVolume = 0.0;
                    executedAvgPrice = Double.NaN;
                    p_executionTime = DateTime.UtcNow;
                    return true;    // correct, expected MinFilter Skip
                }

                int realOrderId = virtualOrder.RealOrderIds?[0] ?? -1;

                // the real order may or maybe Not finished yet. If it is not filled, we will set it as Error order
                OrderStatus realOrderStatus = OrderStatus.None;
                double realExecutedVolume = Double.NaN;
                double realAvgPrice = Double.NaN;
                DateTime realExecutionTime = DateTime.MinValue;
                if (!BrokerWrapper.GetRealOrderExecutionInfo(realOrderId, ref realOrderStatus, ref realExecutedVolume, ref realAvgPrice, ref realExecutionTime, p_isSimulatedTrades))
                    return false;

                if (realOrderStatus == OrderStatus.Filled)
                {
                    orderStatus = realOrderStatus;
                    executedVolume = realExecutedVolume;
                    executedAvgPrice = realAvgPrice;
                }
                else
                {
                    orderStatus = realOrderStatus;  // return the real order status. it doesn't hurt
                    executedVolume = Double.NaN;    // better not using partially filled info, or 0.0
                    executedAvgPrice = Double.NaN;
                }

                return true;

            }
            return false;
        }

      
    }

}
