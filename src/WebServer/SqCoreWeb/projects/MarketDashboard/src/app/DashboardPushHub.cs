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
        // UI responsiveness: webpage HTML,JS loads in 300-400msec. Then JS starts SignalR negotiation, 30ms on server + latency = 100ms. Then we send messages. Between the first SingalR connected message and RT/historical price: 250ms.
        // so Menu bar UI comes in 400ms, but the MarketHealth table appears another 400ms later. Fine.
        // Return from this function very quickly. Do not call any Clients.Caller.SendAsync(), because client will not notice that connection is Connected, and therefore cannot send extra messages until we return here
        // If we send here 30 (stockNews) messages over 30seconds in this, then Client cannot send any UI change messages until we return from here.
        public override Task OnConnectedAsync()
        {
            var userEmailClaim = this.Context?.User?.Claims?.FirstOrDefault(p => p.Type == @"http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress");
            var email = userEmailClaim?.Value ?? "unknown@gmail.com";

            string signalRuser = this.Context?.UserIdentifier ?? "unknown";   // random e.g. "113504229095244802529"
            string connId = this.Context?.ConnectionId ?? String.Empty; // random e.g. "5yHFtj689kolO7UDOx3a5g"
            Utils.Logger.Info($"DashboardPushHub.OnConnectedAsync(), ConnectionID: {connId} with email '{email}'");

            var thisConnectionTime = DateTime.UtcNow;
            var clientIP = WsUtils.GetRequestIPv6(this.Context?.GetHttpContext()!);
            DashboardClient? client = null;
            lock (DashboardClient.g_clients)    // find client from the same IP, assuming connection in the last 1000ms
            {
                client = DashboardClient.g_clients.Find(r => r.ClientIP == clientIP && (thisConnectionTime - r.WsConnectionTime).TotalMilliseconds < 1000);
                if (client == null)
                {
                    client = new DashboardClient() { ClientIP = clientIP, UserEmail = email, IsOnline = true, ActivePage = ActivePage.MarketHealth };
                    DashboardClient.g_clients.Add(client);
                }
            }
            client.SignalRConnectionId = connId;
            client.SignalRUser = signalRuser;
            client.SignalRConnectionTime = thisConnectionTime;

            Groups.AddToGroupAsync(this.Context?.ConnectionId, "EverybodyGroup");   // when we have a new price data, it is sent to all group members

            var handshakeMsg = new HandshakeMessage() { Email = client.UserEmail };
            Clients.Caller.SendAsync("OnConnected", handshakeMsg);  // it is not necessary to send anything here, but we send it for benchmarking SignalR vs. WebSockets purposes

            client!.OnConnectedSignalRAsync_MktHealth();
            client.OnConnectedSignalRAsync_QuickfNews();

            // Production (Linux server) shows it takes 350ms for SignalR connection (start()) to be established, and another 1-10ms when server sends back data to client
            // Benchmarks on the browser client side:
            // Websocket connection start in OnInit: 15ms
            // Websocket connection ready: 366ms    // after _hubConnection.start() returns
            // Websocket Email arrived: 367ms       // sending back user data an the "OnConnected" message.
            // 2020-07: SignalR is not capable of sending data back in Connection. Maybe later they implement it. Or we might choose a better (faster) WebSocket TypeScript API later.
            // the problem is that even vanilla JavaScript WebSocket is not designed for that https://javascript.info/websocket
            // So, the problem is that somehow the SignalR websocket implementation is slow. 
            // If it is slow on the JS side, than I can replace SignalR with Vanilla JS. SignalR can do too many things (LongPolling) that we don't need.
            // If it is slow on the C# side, I have to use something other than SignalR in Kestrel server.

            // even with "skipNegotiation: true", there are 2 messages in F12/Network with the SignalR.WebSocket connection.
            // One is a "{"protocol":"json","version":1}", another is an empty message. And these 2 messages are ABOVE the initial JS WebSocket() handshake.
            // So, that is 3 round-trips. 1: JS.WebSocket connection (ws.open())  2: SignalR start() that communicates protocol. 3: SignalR start() again that returns "".
            // If I do Vanilla WebSocket, it can be reduced to 1 roundtrip only, about 100ms, not 3 round-trips, with 300ms.

            // Exactly the same problem as this guy complains in 2019: "average connection time hovers around 900msec to 1200 msec."
            // https://stackoverflow.com/questions/59328941/asp-net-core-signalr-websocket-connection-time-more-then-1-second

            // "waitForPageLoad: false" is not there in the latest code any more, but my testing shows that is the problem.
            // https://github.com/dotnet/aspnetcore/blob/master/src/SignalR/clients/ts/signalr/src/IHttpConnectionOptions.ts
            // If WindowLoaded is very fast (80ms), then SignalR connection arrives in 95ms, however if WindowLoaded is 350ms, SignalR.start() returns only after 370ms

            return base.OnConnectedAsync();
        }
        public override Task OnDisconnectedAsync(Exception exception)
        {
            var userEmailClaim = this.Context?.User?.Claims?.FirstOrDefault(p => p.Type == @"http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress");
            var email = userEmailClaim?.Value ?? String.Empty;
            string connId = this.Context?.ConnectionId ?? String.Empty;
            Utils.Logger.Info($"OnDisconnectedAsync(), ConnectionID: {connId} with email '{email}'");

            DashboardClient? client = null;
            lock (DashboardClient.g_clients)
            {
                int iClient = DashboardClient.g_clients.FindIndex(r => r.SignalRConnectionId == connId);
                if (iClient != -1)
                {
                    client = DashboardClient.g_clients[iClient];
                    DashboardClient.g_clients.RemoveAt(iClient);
                }
            }

            if (client != null)
            {
                client.OnDisconnectedSignalRAsync_MktHealth(exception);
                client.OnDisconnectedSignalRAsync_QuickfNews(exception);
            }

            return base.OnDisconnectedAsync(exception);
        }

        // it is handled in the native WebSocket connection, not in SignalR, but it is left as an example how to receive message from browser client
        // this should go into some Market-health related class, not here, but fine for now.
        public IEnumerable<RtMktSumNonRtStat> ChangeLookback(string p_lookbackStr) // this._parentHubConnection.invoke('changeLookback', lookbackStr) comes here
        {
            // return GetLookbackStat(p_lookbackStr);
            throw new NotImplementedException();
        }
    }
}