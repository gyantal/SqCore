using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using FinTechCommon;
using Microsoft.AspNetCore.Http;
using SqCommon;

namespace SqCoreWeb
{
    public enum ActivePage { Unknown, MarketHealth, BrAccViewer, CatalystSniffer, QuickfolioNews, TooltipSandpit, Docs }

    public partial class DashboardClient {

        public string ClientIP { get; set; } = string.Empty;    // Remote Client IP for WebSocket
        public string UserEmail { get; set; } = string.Empty;

        public WebSocket? WsWebSocket { get; set; } = null; // this pointer uniquely identifies the WebSocket as it is not released until websocket is dead
        public HttpContext? WsHttpContext { get; set; } = null;
        public DateTime WsConnectionTime { get; set; } = DateTime.MinValue;
        public string WsConnectionId // calculated field: a debugger friendly way of identifying the same websocket, in case WebSocket pointer is not good enough
        {
            get { return this.ClientIP + " at " + WsConnectionTime.ToString("MM'-'dd'T'HH':'mm':'ss"); }
        }
        public bool IsOnline = true;
        public ActivePage ActivePage = ActivePage.Unknown; // knowing which Tool is active can be useful. We might not send data to tools which never becomes active


        public static List<DashboardClient> g_clients = new List<DashboardClient>();
        public static readonly Dictionary<string, ActivePage> c_urlParam2ActivePage = new Dictionary<string, ActivePage>() { 
            {"mh", ActivePage.MarketHealth}, {"bav", ActivePage.BrAccViewer}, {"cs", ActivePage.CatalystSniffer}, {"qn", ActivePage.QuickfolioNews}};

        public static void PreInit()
        {
            MemDb.gMemDb.EvMemDbInitNoHistoryYet += new MemDb.MemDbEventHandler(OnEvMemDbInitNoHistoryYet);
            MemDb.gMemDb.EvFullMemDbDataReloaded += new MemDb.MemDbEventHandler(OnEvFullMemDbDataReloaded);
            MemDb.gMemDb.EvOnlyHistoricalDataReloaded += new MemDb.MemDbEventHandler(OnEvMemDbHistoricalDataReloaded);
        }

        static void OnEvMemDbInitNoHistoryYet()
        {
        }

        static void OnEvFullMemDbDataReloaded()
        {
            DashboardClient.g_clients.ForEach(client =>   // Notify all the connected clients.
            {
                client.EvMemDbAssetDataReloaded_MktHealth();
                client.EvMemDbAssetDataReloaded_BrAccViewer();
            });
        }

        static void OnEvMemDbHistoricalDataReloaded()
        {
            DashboardClient.g_clients.ForEach(client =>   // Notify all the connected clients.
            {
                client.EvMemDbHistoricalDataReloaded_MktHealth();
                client.EvMemDbHistoricalDataReloaded_BrAccViewer();
            });
        }

        public static void ServerDiagnostic(StringBuilder p_sb)
        {
            p_sb.Append("<H2>Dashboard Clients</H2>");
 
            lock (DashboardClient.g_clients)
                p_sb.Append($"#Clients (WebSocket): {DashboardClient.g_clients.Count}: {String.Join(",", DashboardClient.g_clients.Select(r => $"'{r.UserEmail}/{r.ClientIP}'"))}<br>");
            p_sb.Append($"rtDashboardTimerRunning: {m_rtDashboardTimerRunning}<br>");
        }

        public DashboardClient(string p_clientIP, string p_userEmail)
        {
            ClientIP = p_clientIP;
            UserEmail = p_userEmail;

            Ctor_MktHealth();
            Ctor_BrAccViewer();
            Ctor_QuickfNews();
        }

        // Return from this function very quickly. Do not call any Clients.Caller.SendAsync(), because client will not notice that connection is Connected, and therefore cannot send extra messages until we return here
        public void OnConnectedWsAsync_DshbrdClient(bool p_isThisActiveToolAtConnectionInit, User p_user, ManualResetEvent p_waitHandleRtPriceSending)
        {
            // Note: as client is not fully initialized yet, 'this.client' is not yet in DashboardClient.g_clients list.
        }

        public bool OnReceiveWsAsync_DshbrdClient(WebSocketReceiveResult? wsResult, string msgCode, string msgObjStr)
        {
            switch (msgCode)
            {
                case "Dshbrd.IsDshbrdOpenManyTimes":
                    Utils.Logger.Info("OnReceiveWsAsync__DshbrdClient(): IsDashboardOpenManyTimes");
                    SendIsDashboardOpenManyTimes();
                    return true;
                default:
                    return false;
            }
        }

        public void SendIsDashboardOpenManyTimes()    // If Dashboard is open in more than one tab or browser.
        {
            int nClientsWitSameUserAndIp = 0;
            foreach (var client in DashboardClient.g_clients)   // !Warning: Multithreaded Warning: This Reader code is fine. But potential problem if another thread removes clients from the List. The Modifier (Writer) thread should be careful, and Copy and Pointer-Swap when that Edit is taken.
            {
                if (client.UserEmail == UserEmail && client.ClientIP == ClientIP)
                    nClientsWitSameUserAndIp++;
                if (nClientsWitSameUserAndIp > 1)
                    break;
            }
            bool isDashboardOpenManyTimes = nClientsWitSameUserAndIp > 1;
            byte[] encodedMsg = Encoding.UTF8.GetBytes("Dshbrd.IsDshbrdOpenManyTimes:" + Utils.CamelCaseSerialize(isDashboardOpenManyTimes)); // => e.g. "Dshbrd.IsDshbrdOpenManyTimes:false"
            if (WsWebSocket!.State == WebSocketState.Open)
                WsWebSocket.SendAsync(new ArraySegment<Byte>(encodedMsg, 0, encodedMsg.Length), WebSocketMessageType.Text, true, CancellationToken.None);    //  takes 0.635ms
        }
    }
}
