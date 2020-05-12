using Microsoft.AspNetCore.SignalR;
using System;
using System.Threading;
using System.Threading.Tasks;
using SqCommon;
using System.Linq;
using System.Collections.Generic;
using Microsoft.Extensions.Hosting;
using System.Text;
using System.Net;
using FinTechCommon;
using System.Diagnostics;

namespace SqCoreWeb
{
    enum ActivePage { Unknown, MarketHealth, CatalystSniffer, QuickfolioNews, TooltipSandpit, Docs }
    class DashboardClients {
        public string ConnectionId { get; set; } = String.Empty;
        public string SignalRUser { get; set; } = String.Empty; // a user could be connected on their desktop as well as their phone; uses the ClaimTypes.NameIdentifier from the ClaimsPrincipal
        public string UserEmail { get; set; } = String.Empty;
        public bool IsOnline = false;
        public ActivePage ActivePage = ActivePage.MarketHealth; // knowing which Tool is active can be useful. We might not send data to tools which never becomes active
    }

    // these members has to be C# properties, not simple data member tags. Otherwise SignalR will not serialize it to client.
    class HandshakeMessage {
        public String Email { get; set; } = String.Empty;
        public int AnyParam { get; set; } = 55;
    }

    // https://stackoverflow.com/questions/27299289/how-to-get-signalr-hub-context-in-a-asp-net-core
    public class DashboardPushHubKestrelBckgrndSrv : IHostedService, IDisposable
    {
        public static IHubContext<DashboardPushHub>? HubContext;

        public DashboardPushHubKestrelBckgrndSrv(IHubContext<DashboardPushHub> hubContext)
        {
            HubContext = hubContext;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            //TODO: your start logic, some timers, singletons, etc
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            //TODO: your stop logic
            return Task.CompletedTask;
        }

        public void Dispose()
        {
        }
    }

    public partial class DashboardPushHub : Hub
    {
        static List<DashboardClients> g_clients = new List<DashboardClients>();

        static DashboardPushHub()
        {
            // static ctor DashboardPushHub is only called at the time first instance is created, which is only when the first connection happens. It can be days after Kestrel webserver starts. 
            // But that is OK. At least if MarketDashboard is not used by users, it will not consume CPU resources.");
        }
        public static void EarlyInit()
        {
            MemDb.gMemDb.EvInitialized += new MemDb.MemDbEventHandler(EvMemDbInitialized);
            MemDb.gMemDb.EvHistoricalDataReloaded += new MemDb.MemDbEventHandler(EvMemDbHistoricalDataReloaded);
        }

        static void EvMemDbInitialized()
        {
            EvMemDbInitialized_mktHealth();
        }

        static void EvMemDbHistoricalDataReloaded()
        {
            EvMemDbHistoricalDataReloaded_mktHealth();
        }

        // UI responsiveness: webpage HTML,JS loads in 300-400msec. Then JS starts SignalR negotiation, 30ms on server + latency = 100ms. Then we send messages. Between the first SingalR connected message and RT/historical price: 250ms.
        // so Menu bar UI comes in 400ms, but the MarketHealth table appears another 400ms later. Fine.
        // Return from this function very quickly. Do not call any Clients.Caller.SendAsync(), because client will not notice that connection is Connected, and therefore cannot send extra messages until we return here
        // If we send here 30 (stockNews) messages over 30seconds in this, then Client cannot send any UI change messages until we return from here.
        public override Task OnConnectedAsync()
        {
            var userEmailClaim = this.Context?.User?.Claims?.FirstOrDefault(p => p.Type == @"http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress");
            var email = userEmailClaim?.Value ?? "unknown@gmail.com";
            string signalRuser = this.Context?.UserIdentifier?? "unknown";   // if user is not authed, UserIdentifier is null
            string connId = this.Context?.ConnectionId ?? String.Empty;
            Utils.Logger.Info($"OnConnectedAsync(), ConnectionID: {connId} with email '{email}'");

            Groups.AddToGroupAsync(this.Context?.ConnectionId, "EverybodyGroup");   // when we have a new price data, it is sent to all group members

            var client = new DashboardClients() { ConnectionId = connId, SignalRUser = signalRuser, UserEmail = email, IsOnline = true, ActivePage = ActivePage.MarketHealth };
            
            lock (g_clients)
                g_clients.Add(client);

            OnConnectedAsync_MktHealth();
            OnConnectedAsync_QuickfNews();

            var handshakeMsg = new HandshakeMessage() { Email = client.UserEmail };
            //Clients.Caller.SendCoreAsync("OnConnected", handshakeMsg);    // this sends an array of objects
            Clients.Caller.SendAsync("OnConnected", handshakeMsg);
            return base.OnConnectedAsync();
        }
        public override Task OnDisconnectedAsync(Exception exception)
        {
            var userEmailClaim = this.Context?.User?.Claims?.FirstOrDefault(p => p.Type == @"http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress");
            var email = userEmailClaim?.Value ?? String.Empty;
            string connId = this.Context?.ConnectionId ?? String.Empty;
            Utils.Logger.Info($"OnDisconnectedAsync(), ConnectionID: {connId} with email '{email}'");

            lock (g_clients)
            {
                
                int iClient = g_clients.FindIndex(r => r.ConnectionId == connId);
                if (iClient != -1)
                {
                    g_clients.RemoveAt(iClient);
                }
            }

            OnDisconnectedAsync_MktHealth(exception);
            OnDisconnectedAsync_QuickfNews(exception);

            return base.OnDisconnectedAsync(exception);
        }

        public static void ServerDiagnostic(StringBuilder p_sb)
        {
            p_sb.Append("<H2>DashboardPushHub Websockets</H2>");
 
            // The idea behind signalR clients is that it does not implement IEnumerable interface thus making it impossible to iterate over online users. Although Reflection can be used to get hidden info.
            // p_sb.Append($"#Websockets {DashboardPushHubBackgroundService.HubContext?.Clients.All.}, #alive Websockets{}");
            lock (g_clients)
            {
                p_sb.Append($"#WebSocket Clients: {g_clients.Count}: {String.Join(",", g_clients.Select(r => "'" + r.UserEmail + "'"))}<br>");
            }
            p_sb.Append($"mktSummaryTimerRunning: {m_rtMktSummaryTimerRunning}<br>");
        }
    }
}