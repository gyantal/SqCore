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
using Microsoft.AspNetCore.Http;
using System.Net.WebSockets;
using System.Text.Json;

namespace SqCoreWeb
{
    // these members has to be C# properties, not simple data member tags. Otherwise it will not serialize to client.
    class HandshakeMessage {    // General params for the aggregate Dashboard. These params should be not specific to smaller tools, like HealthMonitor, CatalystSniffer, QuickfolioNews
        public string Email { get; set; } = string.Empty;
        public int AnyParam { get; set; } = 55;
    }

    public class DashboardWs
    {
        public static async Task OnWsConnectedAsync(HttpContext context, WebSocket webSocket)
        {
            Utils.Logger.Debug($"DashboardWs.OnConnectedAsync()) BEGIN");
            // context.Request comes as: 'wss://' + document.location.hostname + '/ws/dashboard?t=bav'
            var userEmailClaim = context?.User?.Claims?.FirstOrDefault(p => p.Type == @"http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress");
            var email = userEmailClaim?.Value ?? "unknown@gmail.com";

            string? activeToolAtConnectionInit = context!.Request.Query["t"];   // if "t" is not found, the empty StringValues casted to string as null
            if (activeToolAtConnectionInit == null)
                activeToolAtConnectionInit = "mh";
            if (!DashboardClient.c_urlParam2ActivePage.TryGetValue(activeToolAtConnectionInit, out ActivePage activePage))
                activePage = ActivePage.Unknown;

            // https://stackoverflow.com/questions/24450109/how-to-send-receive-messages-through-a-web-socket-on-windows-phone-8-using-the-c
            var msgObj = new HandshakeMessage() { Email = email };
            byte[] encodedMsg = Encoding.UTF8.GetBytes("OnConnected:" + Utils.CamelCaseSerialize(msgObj));
            if (webSocket.State == WebSocketState.Open)
                await webSocket.SendAsync(new ArraySegment<Byte>(encodedMsg, 0, encodedMsg.Length), WebSocketMessageType.Text, true, CancellationToken.None);    //  takes 0.635ms

            // create a connectionID based on client IP + connectionTime; the userID is the email as each user must be authenticated by an email.
            var clientIP = WsUtils.GetRequestIPv6(context!);    // takes 0.346ms
            Utils.Logger.Info($"DashboardWs.OnConnectedAsync(), Connection from IP: {clientIP} with email '{email}'");  // takes 1.433ms
            var thisConnectionTime = DateTime.UtcNow;
            DashboardClient? client = new(clientIP, email);
            client.WsConnectionTime = thisConnectionTime; // used by the other (secondary) connection to decide whether to create a new g_clients item.
            client.WsWebSocket = webSocket;
            client.WsHttpContext = context;
            client.ActivePage = activePage;

            User? user = MemDb.gMemDb.Users.FirstOrDefault(r => r.Email == client!.UserEmail);
            if (user == null)
            {
                Utils.Logger.Error($"Error. UserEmail is not found among MemDb users '{client!.UserEmail}'.");
                return;
            }

            ManualResetEvent waitHandleMkthConnect = new(false);
            ManualResetEvent waitHandleBrAccConnect = new(false);

            client!.OnConnectedWsAsync_DshbrdClient(activePage == ActivePage.MarketHealth, user, waitHandleMkthConnect);  // runs in a separate thread for being faster
            client!.OnConnectedWsAsync_MktHealth(activePage == ActivePage.MarketHealth, user, waitHandleMkthConnect);  // runs in a separate thread for being faster
            client!.OnConnectedWsAsync_BrAccViewer(activePage == ActivePage.BrAccViewer, user, waitHandleBrAccConnect); // runs in a separate thread for being faster
            client!.OnConnectedWsAsync_QckflNews(activePage == ActivePage.QuickfolioNews);
            client!.OnConnectedWsAsync_QckflNews2(activePage == ActivePage.QuickfolioNews);
            Utils.Logger.Info("OnConnectedAsync() 4");

            // have to wait until the tools initialize themselves to know what assets need RT prices
            bool sucessfullWait = ManualResetEvent.WaitAll(new WaitHandle[] { waitHandleMkthConnect }, 10 * 1000);
            Utils.Logger.Info("OnConnectedAsync() 5");
            if (!sucessfullWait)
                Utils.Logger.Warn("OnConnectedAsync():ManualResetEvent.WaitAll() timeout.");

            client!.OnConnectedWsAsync_Rt();    // immediately send SPY realtime price. It can be used in 3+2 places: BrAccViewer:MarketBar, HistoricalChart, UserAssetList, MktHlth, CatalystSniffer (so, don't send it 5 times. Client will decide what to do with RT price)

            Utils.Logger.Info("OnConnectedAsync() 6");
            // RtTimer runs in every 3-5 seconds and uses g_clients, so don't add client to g_clients too early, because RT would be sent there even before OnConnection is not ready.
            lock (DashboardClient.g_clients)    // lock assures that there are no 2 threads that is Adding at the same time on Cloned g_glients.
            {
                // !Warning: Multithreaded Warning: The Modifier (Writer) thread should be careful, and Copy and Pointer-Swap when Edit/Remove is done.
                List<DashboardClient> clonedClients = new(DashboardClient.g_clients); // adding new item to clone assures that no enumerating reader threads will throw exception.
                clonedClients.Add(client);
                DashboardClient.g_clients = clonedClients;
            }
        }

        public static void OnWsReceiveAsync(HttpContext context, WebSocket webSocket, WebSocketReceiveResult? wsResult, string bufferStr)
        {
            DashboardClient? client = DashboardClient.g_clients.Find(r => r.WsWebSocket == webSocket);
            if (client == null)
                return;

            var semicolonInd = bufferStr.IndexOf(':');
            string msgCode = bufferStr.Substring(0, semicolonInd);
            string msgObjStr = bufferStr.Substring(semicolonInd + 1);
            if (msgCode == "Dshbrd.BrowserWindowUnload")
            {
                DisposeClient(client);
                return;
            }

            bool isHandled = client.OnReceiveWsAsync_DshbrdClient(wsResult, msgCode, msgObjStr);
            if (!isHandled)
                isHandled = client.OnReceiveWsAsync_MktHealth(wsResult, msgCode, msgObjStr);
            if (!isHandled)
                isHandled = client.OnReceiveWsAsync_BrAccViewer(wsResult, msgCode, msgObjStr);
            if (!isHandled)
                isHandled = client.OnReceiveWsAsync_QckflNews(wsResult, msgCode, msgObjStr);
            if (!isHandled)
                isHandled = client.OnReceiveWsAsync_QckflNews2(wsResult, msgCode, msgObjStr);
            if (!isHandled)
            {
                // throw new Exception($"Unexpected websocket received msgCode '{msgCode}'");
                Utils.Logger.Error($"Unexpected websocket received msgCode '{msgCode}'");
            }
        }

        public static void OnWsClose(HttpContext context, WebSocket webSocket, WebSocketReceiveResult? wsResult)
        {
            DashboardClient? client = DashboardClient.g_clients.Find(r => r.WsWebSocket == webSocket);
            DisposeClient(client);
        }

        public static void DisposeClient(DashboardClient? client)
        {
            // We might be called DisposeClient() twice. Once from "Dshbrd.BrowserWindowUnload" message. And once from the websocket.Close() event.
            // do all client resource disposal
            if (client != null)
                DashboardClient.RemoveFromClients(client);
        }

    }   // class
}