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

            var handshakeMsg = new HandshakeMessage() { Email = client.UserEmail };
            //Clients.Caller.SendCoreAsync("OnConnected", handshakeMsg);    // this sends an array of objects
            Clients.Caller.SendAsync("OnConnected", handshakeMsg);

            OnConnectedAsync_MktHealth();
            OnConnectedAsync_QuickfNews();

            // Production (Linux server) shows it takes 350ms for SignalR connection (start()) to be established, and another 350ms when server sends back data to client
            // Benchmarks on the browser client side:
            // Websocket connection start in OnInit: 15ms
            // Websocket connection ready: 366ms    // after _hubConnection.start() returns
            // Websocket Email arrived: 782ms       // sending back user data an the "OnConnected" message.
            // It would be useful if we can send back user data in the start() connection establishment. That would save 350ms round-trip. 
            // The latency is 30ms, so a roundtrip should be maximum 70ms for the Connection established.
            // 2020-07: SignalR is not capable of sending data back in Connection. Maybe later they implement it. Or we might choose a better (faster) WebSocket TypeScript API later.
            // the problem is that even vanilla JavaScript WebSocket is not designed for that https://javascript.info/websocket
            // So, the problem is that somehow the SignalR websocket implementation is slow. 
            // If it is slow on the JS side, than I can replace SignalR with Vanilla JS. SignalR can do too many things (LongPolling) that we don't need.
            // If it is slow on the C# side, I have to use something other than SignalR in Kestrel server.

            // even with "skipNegotiation: true", there are 2 messages in F12/Network with the WebSocket connection.
            // One is a "{"protocol":"json","version":1}", another is an empty message. And these 2 messages are ABOVE the initial JS WebSocket() handshake.
            // So, that is 3 round-trips. 1: JS.WebSocket connection (ws.open())  2: SignalR start() that communicates protocol. 3: SignalR start() again that returns "".
            // If I do Vanilla WebSocket, it can be reduced to 1 roundtrip only, about 100ms, not 3 round-trips, with 300ms.

            // Exactly the same problem as this guy complains in 2019: "average connection time hovers around 900msec to 1200 msec."
            // https://stackoverflow.com/questions/59328941/asp-net-core-signalr-websocket-connection-time-more-then-1-second

            // "waitForPageLoad: false" is not there in the latest code any more, but my testing shows that is the problem.
            // https://github.com/dotnet/aspnetcore/blob/master/src/SignalR/clients/ts/signalr/src/IHttpConnectionOptions.ts
            // If WindowLoaded is very fast (80ms), then SignalR connection arrives in 95ms, however if WindowLoaded is 350ms, SignalR.start() returns only after 370ms


            // However, it is in namespace Microsoft.AspNetCore.SignalR, so it is the official AspNetCore solution.
            // Probably the best to wait until they optimize it. Maybe in the next version of NetCore.
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