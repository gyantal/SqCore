using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using FinTechCommon;
using Microsoft.AspNetCore.Http;

namespace SqCoreWeb
{
    public enum ActivePage { Unknown, MarketHealth, CatalystSniffer, QuickfolioNews, TooltipSandpit, Docs }

    public partial class DashboardClient {

        public string ClientIP { get; set; } = String.Empty;    // Remote Client IP is the same for WebSocket and SignalR
        public string UserEmail { get; set; } = String.Empty;

        public WebSocket? WsWebSocket { get; set; } = null; // this pointer uniquely identifies the WebSocket as it is not released until websocket is dead
        public HttpContext? WsHttpContext { get; set; } = null;
        public DateTime WsConnectionTime { get; set; } = DateTime.MinValue; // signalR connection can find the native Websocket connection based on this; If email is also the same.
        public string WsConnectionId // calculated field: a debugger friendly way of identifying the same websocket, in case WebSocket pointer is not good enough
        {
            get { return this.ClientIP + " at " + WsConnectionTime.ToString("MM'-'dd'T'HH':'mm':'ss"); }
        }

        public string SignalRConnectionId { get; set; } = String.Empty;
        public string SignalRUser { get; set; } = String.Empty; // a user could be connected on their desktop as well as their phone; uses the ClaimTypes.NameIdentifier from the ClaimsPrincipal
        public DateTime SignalRConnectionTime { get; set; } = DateTime.MinValue;


        public bool IsOnline = true;
        public ActivePage ActivePage = ActivePage.MarketHealth; // knowing which Tool is active can be useful. We might not send data to tools which never becomes active


        public static List<DashboardClient> g_clients = new List<DashboardClient>();


        static DashboardClient()
        {
            // static ctor DashboardPushHub is only called at the time first instance is created, which is only when the first connection happens. It can be days after Kestrel webserver starts. 
            // But that is OK. At least if MarketDashboard is not used by users, it will not consume CPU resources.");
        }
        public static void EarlyInit()
        {
            MemDb.gMemDb.EvAssetDataReloaded += new MemDb.MemDbEventHandler(EvMemDbAssetDataReloaded);
            MemDb.gMemDb.EvHistoricalDataReloaded += new MemDb.MemDbEventHandler(EvMemDbHistoricalDataReloaded);
        }

        static void EvMemDbAssetDataReloaded()
        {
            DashboardClient.g_clients.ForEach(client =>   // Notify all the connected clients.
            {
                client.EvMemDbAssetDataReloaded_mktHealth();
            });
            
        }

        static void EvMemDbHistoricalDataReloaded()
        {
            DashboardClient.g_clients.ForEach(client =>   // Notify all the connected clients.
            {
                client.EvMemDbHistoricalDataReloaded_mktHealth();
            });
        }

        public static void ServerDiagnostic(StringBuilder p_sb)
        {
            p_sb.Append("<H2>Dashboard Clients</H2>");
 
            // The idea behind signalR clients is that it does not implement IEnumerable interface thus making it impossible to iterate over online users. Although Reflection can be used to get hidden info.
            // p_sb.Append($"#Websockets {DashboardPushHubBackgroundService.HubContext?.Clients.All.}, #alive Websockets{}");
            lock (DashboardClient.g_clients)
            {
                p_sb.Append($"#Clients (using both WebSocket for MarketHealth + SignalR for QuickfolioNews): {DashboardClient.g_clients.Count}: {String.Join(",", DashboardClient.g_clients.Select(r => "'" + r.UserEmail + "'"))}<br>");
            }
            p_sb.Append($"mktSummaryTimerRunning: {m_rtMktSummaryTimerRunning}<br>");
        }

        public DashboardClient(string p_clientIP, string p_userEmail)
        {
            ClientIP = p_clientIP;
            UserEmail = p_userEmail;

            Ctor_mktHealth();
            Ctor_QuickfNews();
        }
    }

    // these members has to be C# properties, not simple data member tags. Otherwise SignalR will not serialize it to client.
    class HandshakeMessage {    // General params for the aggregate Dashboard. These params should be not specific to smaller tools, like HealthMonitor, CatalystSniffer, QuickfolioNews
        public String Email { get; set; } = String.Empty;
        public int AnyParam { get; set; } = 55;
    }

}
