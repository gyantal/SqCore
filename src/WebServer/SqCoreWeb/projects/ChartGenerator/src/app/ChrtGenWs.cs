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

// these members has to be C# properties, not simple data member tags. Otherwise it will not serialize to client.
class HandshakeMessageChrtGen
{ // General params for the aggregate Dashboard. These params should be not specific to smaller tools, like HealthMonitor, CatalystSniffer, QuickfolioNews
    public string Email { get; set; } = string.Empty;
    public int AnyParam { get; set; } = 75;
}

public class ChrtGenWs
{
    public static async Task OnWsConnectedAsync(HttpContext context, WebSocket webSocket)
    {
        Utils.Logger.Debug($"ChrtGenWs.OnConnectedAsync()) BEGIN");
        // context.Request comes as: 'wss://' + document.location.hostname + '/ws/chrtgen?t=bav'
        var userEmailClaim = context?.User?.Claims?.FirstOrDefault(p => p.Type == @"http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress");
        var email = userEmailClaim?.Value ?? "unknown@gmail.com";

        // https://stackoverflow.com/questions/24450109/how-to-send-receive-messages-through-a-web-socket-on-windows-phone-8-using-the-c
        var msgObj = new HandshakeMessageChrtGen() { Email = email };
        byte[] encodedMsg = Encoding.UTF8.GetBytes("OnConnected:" + Utils.CamelCaseSerialize(msgObj));
        if (webSocket.State == WebSocketState.Open)
            await webSocket.SendAsync(new ArraySegment<Byte>(encodedMsg, 0, encodedMsg.Length), WebSocketMessageType.Text, true, CancellationToken.None);    // takes 0.635ms
    }

    public static void OnWsReceiveAsync(/* HttpContext context, WebSocketReceiveResult? result, */ WebSocket webSocket,  string bufferStr)
    {
        _ = webSocket; // StyleCop SA1313 ParameterNamesMustBeginWithLowerCaseLetter. They won't fix. Recommended solution for unused parameters, instead of the discard (_1) parameters

        var semicolonInd = bufferStr.IndexOf(':');
        string msgCode = bufferStr[..semicolonInd];
        string msgObjStr = bufferStr[(semicolonInd + 1)..];

        switch (msgCode)
        {
            case "RunBacktest":
                Utils.Logger.Info($"ChrtGen.OnWsReceiveAsync(): RunBacktest: '{msgObjStr}'");
                break;
            default:
                Utils.Logger.Info($"ChrtGen.OnWsReceiveAsync(): Unrecognized message from client, {msgCode},{msgObjStr}");
                break;
        }
    }

    public static void OnWsClose(WebSocket webSocket)
    {
        _ = webSocket; // StyleCop SA1313 ParameterNamesMustBeginWithLowerCaseLetter. They won't fix. Recommended solution for unused parameters, instead of the discard (_1) parameters
    }
}