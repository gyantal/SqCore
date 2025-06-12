using System;
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
        LlmPromptAssistant.GetPromptsDataFromGSheet(webSocket); // Send prompts data to the client
    }

    public static void OnWsClose(WebSocket webSocket)
    {
        _ = webSocket; // StyleCop SA1313 ParameterNamesMustBeginWithLowerCaseLetter. They won't fix. Recommended solution for unused parameters, instead of the discard (_1) parameters
    }

    public static void OnWsReceiveAsync(/* HttpContext context, WebSocketReceiveResult? result, */ WebSocket webSocket, string bufferStr)
    {
        int semicolonInd = bufferStr.IndexOf(':');
        string msgCode = bufferStr[..semicolonInd];
        string msgObjStr = bufferStr[(semicolonInd + 1)..];
        switch (msgCode)
        {
            case "GetChatResponseLlm":
                Utils.Logger.Info($"LlmAssistWs.OnWsReceiveAsync(): GetChatResponseLlm: '{msgObjStr}'");
                LlmChat.GetChatResponseLlm(msgObjStr, webSocket);
                break;
            case "GetBasicChatResponseLlm":
                Utils.Logger.Info($"LlmAssistWs.OnWsReceiveAsync(): GetChatResponseLlm: '{msgObjStr}'");
                LlmBasicChat.GetChatResponseLlm(msgObjStr, webSocket);
                break;
            case "GetStockPrice":
                Utils.Logger.Info($"LlmAssistWs.OnWsReceiveAsync(): GetStockPrice: '{msgObjStr}'");
                LlmScan.GetStockPrice(msgObjStr, webSocket);
                break;
            case "GetTickerNews":
                Utils.Logger.Info($"LlmAssistWs.OnWsReceiveAsync(): GetTickerNews: '{msgObjStr}'");
                LlmScan.GetTickerNews(msgObjStr, webSocket);
                break;
            case "GetLlmAnswer":
                Utils.Logger.Info($"LlmAssistWs.OnWsReceiveAsync(): GetLlmAnswer: '{msgObjStr}'");
                LlmScan.GetLlmAnswer(msgObjStr, webSocket);
                break;
            default:
                Utils.Logger.Info($"LlmAssistWs.OnWsReceiveAsync(): Unrecognized message from client, {msgCode},{msgObjStr}");
                break;
        }
    }
}