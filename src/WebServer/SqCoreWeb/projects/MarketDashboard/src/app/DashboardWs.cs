using System;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FinTechCommon;
using Microsoft.AspNetCore.Http;
using SqCommon;

namespace SqCoreWeb;

// these members has to be C# properties, not simple data member tags. Otherwise it will not serialize to client.
class HandshakeMessage { // General params for the aggregate Dashboard. These params should be not specific to smaller tools, like HealthMonitor, CatalystSniffer, QuickfolioNews
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
            await webSocket.SendAsync(new ArraySegment<Byte>(encodedMsg, 0, encodedMsg.Length), WebSocketMessageType.Text, true, CancellationToken.None);    // takes 0.635ms

        // create a connectionID based on client IP + connectionTime; the userID is the email as each user must be authenticated by an email.
        var clientIP = WsUtils.GetRequestIPv6(context!);    // takes 0.346ms
        Utils.Logger.Info($"DashboardWs.OnConnectedAsync(), Connection from IP: {clientIP} with email '{email}'");  // takes 1.433ms
        User? user = MemDb.gMemDb.Users.FirstOrDefault(r => r.Email == email);
        if (user == null)
        {
            Utils.Logger.Error($"Error. UserEmail is not found among MemDb users '{email}'.");
            return;
        }

        DashboardClient client = new(clientIP, email, user, DateTime.UtcNow)
        {
            WsWebSocket = webSocket,
            WsHttpContext = context,
            ActivePage = activePage
        };
        client.OnConnectedWsAsync_DshbrdClient();

        // RtTimer runs in every 3-5 seconds and uses g_clients, so don't add client to g_clients too early, because RT would be sent there even before OnConnection is not ready.
        DashboardClient.AddToClients(client);
    }

    public static void OnWsReceiveAsync(HttpContext _, WebSocket webSocket, WebSocketReceiveResult? _1, string bufferStr)
    {
        DashboardClient? client = DashboardClient.FindClient(webSocket);
        if (client == null)
            return;

        var semicolonInd = bufferStr.IndexOf(':');
        string msgCode = bufferStr[..semicolonInd];
        string msgObjStr = bufferStr[(semicolonInd + 1)..];
        if (msgCode == "Dshbrd.BrowserWindowUnload")
        {
            DisposeClient(client);
            return;
        }

        bool isHandled = client.OnReceiveWsAsync_DshbrdClient(msgCode, msgObjStr);
        if (!isHandled)
        {
            // throw new Exception($"Unexpected websocket received msgCode '{msgCode}'");
            Utils.Logger.Error($"Unexpected websocket received msgCode '{msgCode}'");
        }
    }

    public static void OnWsClose(WebSocket webSocket)
    {
        DashboardClient? client = DashboardClient.FindClient(webSocket);
        DisposeClient(client);
    }

    public static void DisposeClient(DashboardClient? client)
    {
        // We might be called DisposeClient() twice. Once from "Dshbrd.BrowserWindowUnload" message. And once from the websocket.Close() event.
        // do all client resource disposal
        if (client != null)
            DashboardClient.RemoveFromClients(client);
    }
} // class