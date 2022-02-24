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

    public partial class DashboardClient
    {
        public string ClientIP { get; set; } = string.Empty;    // Remote Client IP for WebSocket
        public string UserEmail { get; set; } = string.Empty;
        public DateTime ConnectionTime { get; set; } = DateTime.MinValue;
        public ActivePage ActivePage = ActivePage.Unknown; // knowing which Tool is active can be useful. We might not send data to tools which never becomes active

        public string ConnectionId // calculated field: a debugger friendly way of identifying the same websocket, in case WebSocket pointer is not good enough
        {
            get { return this.ClientIP + "@" + ConnectionTime.ToString("MM'-'dd'T'HH':'mm':'ss"); }
        }

        public WebSocket? WsWebSocket { get; set; } = null; // this pointer uniquely identifies the WebSocket as it is not released until websocket is dead
        public HttpContext? WsHttpContext { get; set; } = null;

        public static readonly Dictionary<string, ActivePage> c_urlParam2ActivePage = new() { 
            {"mh", ActivePage.MarketHealth}, {"bav", ActivePage.BrAccViewer}, {"cs", ActivePage.CatalystSniffer}, {"qn", ActivePage.QuickfolioNews}};

        public static List<DashboardClient> g_clients = new();
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
            p_sb.Append($"DashboardClient.g_clients (#{DashboardClient.g_clients.Count}): ");
            p_sb.AppendLongListByLine(DashboardClient.g_clients.Select(r => $"'{r.UserEmail}/{r.ConnectionId}'").ToArray(), ",", 3, "<br>");
            p_sb.Append($"<br>rtDashboardTimerRunning: {m_rtDashboardTimerRunning}<br>");
        }

        public DashboardClient(string p_clientIP, string p_userEmail, DateTime p_connectionTime)
        {
            ClientIP = p_clientIP;
            UserEmail = p_userEmail;
            ConnectionTime = p_connectionTime;

            Ctor_MktHealth();
            Ctor_BrAccViewer();
            Ctor_QuickfNews();
            Ctor_QuickfNews2();
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

        public static void RemoveFromClients(DashboardClient p_client)
        {
            // 'beforeunload' will be fired if the user submits a form, clicks a link, closes the window (or tab), or goes to a new page using the address bar, search box, or a bookmark.
            // server removes this client from DashboardClient.g_clients list

            // !Warning: Multithreaded Warning: The Modifier (Writer) thread should be careful, and Copy and Pointer-Swap when Edit/Remove is done.
            List<DashboardClient> clonedClients = new(DashboardClient.g_clients);
            clonedClients.Remove(p_client);
            DashboardClient.g_clients = clonedClients;
        }
    }
}
