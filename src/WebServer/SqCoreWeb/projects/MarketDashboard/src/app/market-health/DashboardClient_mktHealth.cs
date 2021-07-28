using System;
using System.Threading;
using SqCommon;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;
using FinTechCommon;
using System.Text.Json.Serialization;
using System.Net.WebSockets;
using Microsoft.Extensions.Primitives;
using System.Threading.Tasks;

namespace SqCoreWeb
{
    class HandshakeMktHealth {    //Initial params
        public List<AssetJs> MarketSummaryAssets { get; set; } = new List<AssetJs>();
        public List<AssetJs> SelectableNavAssets { get; set; } = new List<AssetJs>();

    }
 

    // The knowledge 'WHEN to send what' should be programmed on the server. When server senses that there is an update, then it broadcast to clients. 
    // Do not implement the 'intelligence' of WHEN to change data on the client. It can be too complicated, like knowing if there was a holiday, a half-trading day, etc. 
    // Clients should be slim programmed. They should only care, that IF they receive a new data, then Refresh.
    public partial class DashboardClient
    {
        // one global static real-time price Timer serves all clients. For efficiency.
        static Timer m_rtMktSummaryTimer = new System.Threading.Timer(new TimerCallback(RtMktSummaryTimer_Elapsed), null, TimeSpan.FromMilliseconds(-1.0), TimeSpan.FromMilliseconds(-1.0));
        static bool m_rtMktSummaryTimerRunning = false;
        static object m_rtMktSummaryTimerLock = new Object();

        static int m_rtMktSummaryTimerFrequencyMs = 3000;    // as a demo go with 3sec, later change it to 5sec, do decrease server load.

        public static TimeSpan c_initialSleepIfNotActiveToolMh = TimeSpan.FromMilliseconds(5000);


        string m_lastLookbackPeriodStr = "YTD";


        // try to convert to use these fields. At least on the server side.
        // If we store asset pointers (Stock, Nav) if the MemDb reloads, we should reload these pointers from the new MemDb. That adds extra code complexity.
        // However, for fast execution, it is still better to keep asset pointers, instead of keeping the asset's SqTicker and always find them again and again in MemDb.
        BrokerNav? m_mkthSelectedNavAsset = null;   // remember which NAV is selected, so we can send RT data
        List<string> c_marketSummarySqTickersDefault = new List<string>() { "S/QQQ", "S/SPY", "S/GLD", "S/TLT", "S/VXX", "S/UNG", "S/USO"};
        List<string> c_marketSummarySqTickersDc = new List<string>() { "S/QQQ", "S/SPY", "S/GLD", "S/TLT", "S/VXX", "S/UNG", "S/USO"};   // at the moment DC uses the same as default
        List<Asset> m_marketSummaryAssets = new List<Asset>();      // remember, so we can send RT data

        void Ctor_MktHealth()
        {
        }

        void EvMemDbAssetDataReloaded_MktHealth()
        {
            // have to refresh Asset pointers in memory, such as m_marketSummaryAssets, m_mkthSelectedNavAsset
            // have to resend the HandShake message Asset Id to SqTicker associations. Have to resend everything.
        }


        void EvMemDbHistoricalDataReloaded_MktHealth()
        {
            Utils.Logger.Info("EvMemDbHistoricalDataReloaded_mktHealth() START");

            IEnumerable<AssetHistStatJs> periodStatToClient = GetLookbackStat(m_lastLookbackPeriodStr);     // reset lookback to to YTD. Because of BrokerNAV, lookback period stat is user specific.
            Utils.Logger.Info("EvMemDbHistoricalDataReloaded_mktHealth(). Processing client:" + UserEmail);
            byte[] encodedMsg = Encoding.UTF8.GetBytes("MktHlth.NonRtStat:" + Utils.CamelCaseSerialize(periodStatToClient));
            if (WsWebSocket == null)
                Utils.Logger.Info("Warning (TODO)!: Mystery how client.WsWebSocket can be null? Investigate!) ");
            if (WsWebSocket!.State == WebSocketState.Open)
                WsWebSocket.SendAsync(new ArraySegment<Byte>(encodedMsg, 0, encodedMsg.Length), WebSocketMessageType.Text, true, CancellationToken.None);    //  takes 0.635ms
        }

        // Return from this function very quickly. Do not call any Clients.Caller.SendAsync(), because client will not notice that connection is Connected, and therefore cannot send extra messages until we return here
        public void OnConnectedWsAsync_MktHealth(bool p_isThisActiveToolAtConnectionInit, User p_user)
        {
            Task.Run(() =>  // running parallel on a ThreadPool thread
            {
                Thread.CurrentThread.IsBackground = true;  //  thread will be killed when all foreground threads have died, the thread will not keep the application alive.

                // Assuming this tool is not the main Tab page on the client, we delay sending all the data, to avoid making the network and client too busy an unresponsive
                if (!p_isThisActiveToolAtConnectionInit)
                    Thread.Sleep(c_initialSleepIfNotActiveToolMh);

                List<BrokerNav> selectableNavs = p_user.GetAllVisibleBrokerNavsOrdered();
                m_mkthSelectedNavAsset = selectableNavs.FirstOrDefault();

                List<string> marketSummarySqTickers = (p_user.Username == "drcharmat") ? c_marketSummarySqTickersDc : c_marketSummarySqTickersDefault;
                m_marketSummaryAssets = marketSummarySqTickers.Select(r => MemDb.gMemDb.AssetsCache.GetAsset(r)).ToList();

                HandshakeMktHealth handshake = GetHandshakeMktHlth(selectableNavs);
                byte[] encodedMsg = Encoding.UTF8.GetBytes("MktHlth.Handshake:" + Utils.CamelCaseSerialize(handshake));
                if (WsWebSocket!.State == WebSocketState.Open)
                    WsWebSocket.SendAsync(new ArraySegment<Byte>(encodedMsg, 0, encodedMsg.Length), WebSocketMessageType.Text, true, CancellationToken.None);

                // for both the first and the second client, we get RT prices from MemDb immediately and send it back to this Client only.

                // 1. Send the Historical data first. SendAsync() is non-blocking. GetLastRtPrice() can be blocking
                SendHistoricalWs();
                // 2. Send RT price later, because GetLastRtPrice() might block the thread, if it is the first client.
                SendRealtimeWs();

                lock (m_rtMktSummaryTimerLock)
                {
                    if (!m_rtMktSummaryTimerRunning)
                    {
                        Utils.Logger.Info("OnConnectedAsync_MktHealth(). Starting m_rtMktSummaryTimer.");
                        m_rtMktSummaryTimerRunning = true;
                        m_rtMktSummaryTimer.Change(TimeSpan.FromMilliseconds(m_rtMktSummaryTimerFrequencyMs), TimeSpan.FromMilliseconds(-1.0));    // runs only once. To avoid that it runs parallel, if first one doesn't finish
                    }
                }
            });
        }

        private void SendHistoricalWs()
        {
            IEnumerable<AssetHistStatJs> periodStatToClient = GetLookbackStat(m_lastLookbackPeriodStr);
            byte[] encodedMsg = Encoding.UTF8.GetBytes("MktHlth.NonRtStat:" + Utils.CamelCaseSerialize(periodStatToClient));
            if (WsWebSocket!.State == WebSocketState.Open)
                WsWebSocket.SendAsync(new ArraySegment<Byte>(encodedMsg, 0, encodedMsg.Length), WebSocketMessageType.Text, true, CancellationToken.None);    //  takes 0.635ms
        }

        private void SendRealtimeWs()
        {
            IEnumerable<AssetRtJs> rtMktSummaryToClient = GetRtStat();
            byte[] encodedMsg = Encoding.UTF8.GetBytes("MktHlth.RtStat:" + Utils.CamelCaseSerialize(rtMktSummaryToClient));
            if (WsWebSocket!.State == WebSocketState.Open)
                WsWebSocket.SendAsync(new ArraySegment<Byte>(encodedMsg, 0, encodedMsg.Length), WebSocketMessageType.Text, true, CancellationToken.None);    //  takes 0.635ms
        }

        public bool OnReceiveWsAsync_MktHealth(WebSocketReceiveResult? wsResult, string msgCode, string msgObjStr)
        {
            switch (msgCode)
            {
                case "MktHlth.ChangeLookback":
                    Utils.Logger.Info("OnReceiveWsAsync_MktHealth(): changeLookback");
                    m_lastLookbackPeriodStr = msgObjStr;
                    SendHistoricalWs();
                    return true;
                case "MktHlth.ChangeNav":
                    Utils.Logger.Info($"OnReceiveWsAsync_MktHealth(): changeNav to '{msgObjStr}'"); // DC.IM
                    string sqTicker = "N/" + msgObjStr; // turn DC.IM to N/DC.IM
                    var navAsset = MemDb.gMemDb.AssetsCache.GetAsset(sqTicker);
                    this.m_mkthSelectedNavAsset = navAsset as BrokerNav;

                    SendHistoricalWs();
                    SendRealtimeWs();
                    return true;
                default:
                    return false;
            }
        }

        private IEnumerable<AssetHistStatJs> GetLookbackStat(string p_lookbackStr)
        {
            List<Asset> allAssets = m_marketSummaryAssets.ToList();   // duplicate the asset pointers. Don't add navAsset to m_marketSummaryAssets
            if (m_mkthSelectedNavAsset != null)
                allAssets.Add(m_mkthSelectedNavAsset);

            DateTime todayET = Utils.ConvertTimeFromUtcToEt(DateTime.UtcNow).Date;  // the default is YTD. Leave it as it is used frequently: by default server sends this to client at Open. Or at EvMemDbHistoricalDataReloaded_mktHealth()
            DateOnly lookbackStartInc = new DateOnly(todayET.Year - 1, 12, 31);  // YTD relative to 31st December, last year
            DateOnly lookbackEndExcl = todayET;
            if (p_lookbackStr.StartsWith("Date:"))  // Browser client never send anything, but "Date:" inputs. Format: "Date:2019-11-11...2020-11-10"
            {
                lookbackStartInc = Utils.FastParseYYYYMMDD(new StringSegment(p_lookbackStr, "Date:".Length, 10));
                lookbackEndExcl = Utils.FastParseYYYYMMDD(new StringSegment(p_lookbackStr, "Date:".Length + 13, 10));
            }
            // else if (p_lookbackStr.EndsWith("y"))
            // {
            //     if (Int32.TryParse(p_lookbackStr.Substring(0, p_lookbackStr.Length - 1), out int nYears))
            //         lookbackStartInc = todayET.AddYears(-1 * nYears);
            // }
            // else if (p_lookbackStr.EndsWith("m"))
            // {
            //     if (Int32.TryParse(p_lookbackStr.Substring(0, p_lookbackStr.Length - 1), out int nMonths))
            //         lookbackStartInc = todayET.AddMonths(-1 * nMonths);
            // }
            // else if (p_lookbackStr.EndsWith("w"))
            // {
            //     if (Int32.TryParse(p_lookbackStr.Substring(0, p_lookbackStr.Length - 1), out int nWeeks))
            //         lookbackStartInc = todayET.AddDays(-7 * nWeeks);
            // }

            IEnumerable<AssetHist> assetHists = MemDb.gMemDb.GetSdaHistCloses(allAssets, lookbackStartInc, lookbackEndExcl, false, true);
            IEnumerable<AssetHistStatJs> lookbackStatToClient = assetHists.Select(r =>
            {
                var rtStock = new AssetHistStatJs()
                {
                    AssetId = r.Asset.AssetId,
                    SqTicker = r.Asset.SqTicker,
                    PeriodStartDate = r.PeriodStartDate,    // it may be not the 'asked' start date if asset has less price history
                    PeriodEndDate = r.PeriodEndDate,        // by default it is the date of yesterday, but the user can change it
                    PeriodStart = r.Stat?.PeriodStart ?? Double.NaN,
                    PeriodEnd = r.Stat?.PeriodEnd ?? Double.NaN,
                    PeriodHigh = r.Stat?.PeriodHigh ?? Double.NaN,
                    PeriodLow = r.Stat?.PeriodLow ?? Double.NaN,
                    PeriodMaxDD = r.Stat?.PeriodMaxDD ?? Double.NaN,
                    PeriodMaxDU = r.Stat?.PeriodMaxDU ?? Double.NaN
                };
                return rtStock;
            });
            return lookbackStatToClient;
        }

        private IEnumerable<AssetRtJs> GetRtStat()
        {
            var allAssetIds = m_marketSummaryAssets.Select(r => (uint)r.AssetId).ToList();
            if (m_mkthSelectedNavAsset != null)
                allAssetIds.Add(m_mkthSelectedNavAsset.AssetId);

            var lastValues = MemDb.gMemDb.GetLastRtValueWithUtc(allAssetIds.ToArray());
            return lastValues.Where(r => float.IsFinite(r.LastValue)).Select(r =>
            {
                var rtStock = new AssetRtJs()
                {
                    AssetId = r.SecdID,
                    Last = r.LastValue,
                    LastUtc = r.LastValueUtc
                };
                return rtStock;
            });
        }

        private HandshakeMktHealth GetHandshakeMktHlth(List<BrokerNav> p_selectableNavs)
        {
            //string selectableNavs = "GA.IM, DC, DC.IM, DC.IB";
            List<AssetJs> marketSummaryAssets = m_marketSummaryAssets.Select(r => new AssetJs() { AssetId = r.AssetId, SqTicker = r.SqTicker, Symbol = r.Symbol, Name = r.Name }).ToList();
            List<AssetJs> selectableNavAssets = p_selectableNavs.Select(r => new AssetJs() { AssetId = r.AssetId, SqTicker = r.SqTicker, Symbol = r.Symbol, Name = r.Name }).ToList();
            return new HandshakeMktHealth() { MarketSummaryAssets = marketSummaryAssets, SelectableNavAssets = selectableNavAssets };
        }

        public static void RtMktSummaryTimer_Elapsed(object? state)    // Timer is coming on a ThreadPool thread
        {
            try
            {
                Utils.Logger.Debug("RtMktSummaryTimer_Elapsed(). BEGIN");
                if (!m_rtMktSummaryTimerRunning)
                    return; // if it was disabled by another thread in the meantime, we should not waste resources to execute this.

                DashboardClient.g_clients.ForEach(client =>
                {
                    // to free up resources, send data only if either this is the active tool is this tool or if some seconds has been passed
                    // OnConnectedWsAsync() sleeps for a while if not active tool.
                    TimeSpan timeSinceConnect = DateTime.UtcNow - client.WsConnectionTime;
                    if (client.ActivePage != ActivePage.MarketHealth && timeSinceConnect < c_initialSleepIfNotActiveToolMh.Add(TimeSpan.FromMilliseconds(100)))
                        return;

                    IEnumerable<AssetRtJs> rtMktSummaryToClient = client.GetRtStat();
                    byte[] encodedMsg = Encoding.UTF8.GetBytes("MktHlth.RtStat:" + Utils.CamelCaseSerialize(rtMktSummaryToClient));
                    if (client.WsWebSocket == null)
                        Utils.Logger.Info("Warning (TODO)!: Mystery how client.WsWebSocket can be null? Investigate!) ");
                    if (client.WsWebSocket != null && (client.WsWebSocket.State == WebSocketState.Open))
                        client.WsWebSocket.SendAsync(new ArraySegment<Byte>(encodedMsg, 0, encodedMsg.Length), WebSocketMessageType.Text, true, CancellationToken.None);    //  takes 0.635ms
                
                });

                lock (m_rtMktSummaryTimerLock)
                {
                    if (m_rtMktSummaryTimerRunning)
                    {
                        m_rtMktSummaryTimer.Change(TimeSpan.FromMilliseconds(m_rtMktSummaryTimerFrequencyMs), TimeSpan.FromMilliseconds(-1.0));    // runs only once. To avoid that it runs parallel, if first one doesn't finish
                    }
                }
                // Utils.Logger.Info("RtMktSummaryTimer_Elapsed(). END");
            }
            catch (Exception e)
            {
                Utils.Logger.Error(e, "RtMktSummaryTimer_Elapsed() exception.");
                throw;
            }
        }
    }
}