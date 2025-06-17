using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Fin.MemDb;
using Microsoft.AspNetCore.Http;
using SqCommon;

namespace SqCoreWeb;
class HandshakeMessageLlmAssist
{
    public string Email { get; set; } = string.Empty;
    public int UserId { get; set; } = 0;
}
public class LlmAssistWs
{
    public static async Task OnWsConnectedAsync(HttpContext context, WebSocket webSocket)
    {
        Utils.Logger.Debug($"LlmAssistWs.OnConnectedAsync()) BEGIN");
        var userEmailClaim = context?.User?.Claims?.FirstOrDefault(p => p.Type == @"http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress");
        string? email = userEmailClaim?.Value ?? "unknown@gmail.com";
        User[] users = MemDb.gMemDb.Users; // get the user data
        User? user = Array.Find(users, r => r.Email == email); // find the user

        // https://stackoverflow.com/questions/24450109/how-to-send-receive-messages-through-a-web-socket-on-windows-phone-8-using-the-c
        HandshakeMessageLlmAssist? msgObj = new HandshakeMessageLlmAssist() { Email = email, UserId = user!.Id };
        byte[] encodedMsg = Encoding.UTF8.GetBytes("OnConnected:" + Utils.CamelCaseSerialize(msgObj));
        if (webSocket.State == WebSocketState.Open)
            await webSocket.SendAsync(new ArraySegment<Byte>(encodedMsg, 0, encodedMsg.Length), WebSocketMessageType.Text, true, CancellationToken.None);
        LlmAssistClient.GetPromptsDataFromGSheet(webSocket); // Send prompts data to the client

        string clientIP = WsUtils.GetRequestIPv6(context!);
        Utils.Logger.Info($"LlmAssistWs.OnConnectedAsync(), Connection from IP: {clientIP} with email '{email}'");
        LlmAssistClient client = new(clientIP, email, user, DateTime.UtcNow)
        {
            WsWebSocket = webSocket,
        };
        LlmAssistClient.AddToClients(client); // add the client to the global list of clients
    }

    public static void OnWsReceiveAsync(/* HttpContext context, WebSocketReceiveResult? result, */ WebSocket webSocket,  string bufferStr)
    {
        LlmAssistClient? client = LlmAssistClient.FindClient(webSocket);
        if (client == null)
            return;

        int semicolonInd = bufferStr.IndexOf(':');
        string msgCode = bufferStr[..semicolonInd];
        string msgObjStr = bufferStr[(semicolonInd + 1)..];

        bool isHandled = client.OnReceiveWsAsync_LlmAssistClient(msgCode, msgObjStr);
        if (!isHandled)
        {
            // throw new Exception($"Unexpected websocket received msgCode '{msgCode}'");
            Utils.Logger.Error($"Unexpected websocket received msgCode '{msgCode}'");
        }
    }

    public static void OnWsClose(WebSocket webSocket)
    {
        LlmAssistClient? client = LlmAssistClient.FindClient(webSocket);
        DisposeClient(client);
    }

    public static void DisposeClient(LlmAssistClient? client)
    {
        if (client != null)
            LlmAssistClient.RemoveFromClients(client);
    }
}