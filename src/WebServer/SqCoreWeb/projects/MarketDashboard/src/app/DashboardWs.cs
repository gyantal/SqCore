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
    public partial class DashboardWs
    {
        public static async Task OnConnectedAsync(HttpContext context, WebSocket webSocket)
        {
            var userEmailClaim = context?.User?.Claims?.FirstOrDefault(p => p.Type == @"http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress");
            var email = userEmailClaim?.Value ?? "unknown@gmail.com";

            // https://stackoverflow.com/questions/24450109/how-to-send-receive-messages-through-a-web-socket-on-windows-phone-8-using-the-c
            var msgObj = new HandshakeMessage() { Email = email };
            byte[] encodedMsg = Encoding.UTF8.GetBytes("OnConnected:" + Utils.CamelCaseSerialize(msgObj));
            if (webSocket.State == WebSocketState.Open)
                await webSocket.SendAsync(new ArraySegment<Byte>(encodedMsg, 0, encodedMsg.Length), WebSocketMessageType.Text, true, CancellationToken.None);    //  takes 0.635ms

            // create a connectionID based on client IP + connectionTime; the userID is the email as each user must be authenticated by an email.
            var clientIP = WsUtils.GetRequestIPv6(context!);    // takes 0.346ms
            Utils.Logger.Info($"DashboardWs.OnConnectedAsync(), Connection from IP: {clientIP} with email '{email}'");  // takes 1.433ms
            var thisConnectionTime = DateTime.UtcNow;
            DashboardClient? client = null;
            lock (DashboardClient.g_clients)    // find client from the same IP, assuming connection in the last 2000ms
            {
                // client = DashboardClient.g_clients.Find(r => r.ClientIP == clientIP && (thisConnectionTime - r.WsConnectionTime).TotalMilliseconds < 2000);
                // if (client == null)
                // {
                client = new DashboardClient(clientIP, email);
                DashboardClient.g_clients.Add(client);  // takes 0.004ms
                //}
                client.WsConnectionTime = thisConnectionTime; // used by the other (secondary) connection to decide whether to create a new g_clients item.
                client.WsWebSocket = webSocket;
                client.WsHttpContext = context;
            }

            client!.OnConnectedWsAsync_MktHealth();
            client!.OnConnectedWsAsync_QckflNews();
        }

        public static void OnReceiveAsync(HttpContext context, WebSocket webSocket, WebSocketReceiveResult? wsResult, string bufferStr)
        {
            DashboardClient? client = null;
            lock (DashboardClient.g_clients)    // find client from the same IP, assuming connection in the last 1000ms
            {
                client = DashboardClient.g_clients.Find(r => r.WsWebSocket == webSocket);
            }
            if (client != null)
            {
                var semicolonInd = bufferStr.IndexOf(':');
                string msgCode = bufferStr.Substring(0, semicolonInd);
                string msgObjStr = bufferStr.Substring(semicolonInd + 1);

                bool isHandled = client.OnReceiveWsAsync_MktHealth(wsResult, msgCode, msgObjStr);
                if (!isHandled)
                    isHandled = client.OnReceiveWsAsync_QckflNews(wsResult, msgCode, msgObjStr);
                if (!isHandled)
                {
                    // throw new Exception($"Unexpected websocket received msgCode '{msgCode}'");
                    Utils.Logger.Error($"Unexpected websocket received msgCode '{msgCode}'");
                }
            }
        }
    }   // class
}