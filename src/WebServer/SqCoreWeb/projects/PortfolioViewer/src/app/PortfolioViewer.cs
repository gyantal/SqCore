using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Fin.MemDb;
using Microsoft.AspNetCore.Http;
using QuantConnect;
using QuantConnect.Parameters;
using SqCommon;

namespace SqCoreWeb;

class HandshakeMessagePrtfViewer
{
    public string Email { get; set; } = string.Empty;
    public int AnyParam { get; set; } = 75;
    public PortfolioJs PrtfToClient { get; set; } = new();
}

public class PrtfVwrWs
{
    public static async Task OnWsConnectedAsync(HttpContext context, WebSocket webSocket)
    {
        Utils.Logger.Debug($"PrtfVwrWs.OnConnectedAsync()) BEGIN");
        // context.Request comes as: 'wss://' + document.location.hostname + '/ws/prtfvwr?id=1'
        string? queryStr = context.Request.QueryString.Value;
        var userEmailClaim = context?.User?.Claims?.FirstOrDefault(p => p.Type == @"http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress");
        var email = userEmailClaim?.Value ?? "unknown@gmail.com";
        User[] users = MemDb.gMemDb.Users; // get the user data
        User? user = Array.Find(users, r => r.Email == email); // find the user

        // Processing the query string to extract the Id
        int idStartInd = queryStr!.IndexOf("=");
        if (idStartInd == -1)
            return;
        int id = Convert.ToInt32(queryStr[(idStartInd + 1)..]);
        // https://stackoverflow.com/questions/24450109/how-to-send-receive-messages-through-a-web-socket-on-windows-phone-8-using-the-c
        var msgObj = new HandshakeMessagePrtfViewer() { Email = email, PrtfToClient = UiUtils.GetPortfolio(id) };
        byte[] encodedMsg = Encoding.UTF8.GetBytes("OnConnected:" + Utils.CamelCaseSerialize(msgObj));
        if (webSocket.State == WebSocketState.Open)
            await webSocket.SendAsync(new ArraySegment<Byte>(encodedMsg, 0, encodedMsg.Length), WebSocketMessageType.Text, true, CancellationToken.None); // takes 0.635ms
        if (queryStr != null)
            PortfVwrGetPortfolioRunResults(webSocket, queryStr);
    }

    public static void OnWsClose(WebSocket webSocket)
    {
        _ = webSocket; // StyleCop SA1313 ParameterNamesMustBeginWithLowerCaseLetter. They won't fix. Recommended solution for unused parameters, instead of the discard (_1) parameters
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
                Utils.Logger.Info($"PrtfVwrWs.OnWsReceiveAsync(): RunBacktest: '{msgObjStr}'");
                PortfVwrGetPortfolioRunResults(webSocket, msgObjStr);
                break;
            default:
                Utils.Logger.Info($"PrtfVwrWs.OnWsReceiveAsync(): Unrecognized message from client, {msgCode},{msgObjStr}");
                break;
        }
    }

    // Here we get the p_msg in 2 forms
    // 1. when onConnected it comes as p_msg ="?pid=12".
    // 2. when user sends Historical Position Dates ?pid=12,Date:2022-01-01
    public static void PortfVwrGetPortfolioRunResults(WebSocket webSocket, string p_msg) // p_msg ="?pid=12" or ?pid=12,Date:2022-01-01
    {
        // forcedStartDate and forcedEndDate are determined by specifed algorithm, if null (ex: please refer SqPctAllocation.cs file)
        DateTime? p_forcedStartDate = null;
        DateTime? p_forcedEndDate = null;
        int id;
         // Check if p_msg contains "Date" to determine its format
        if (p_msg.Contains("Date")) // p_msg = "?pid=12,Date:2022-01-01"
        {
            int idStartInd = p_msg.IndexOf("=");
            int dateInd = idStartInd == -1 ? -1 : p_msg.IndexOf(":", idStartInd + 1);
            if (dateInd == -1)
                return;
            id = Convert.ToInt32(p_msg.Substring(idStartInd + 1, dateInd - idStartInd - ",Date:".Length));
            string endDtStr = p_msg[(dateInd + 1)..];
            p_forcedStartDate = DateTime.MinValue;
            p_forcedEndDate = Utils.Str2DateTimeUtc(endDtStr);
        }
        else // p_msg = "?pid=12"
        {
            int idStartInd = p_msg.IndexOf("=");
            if (idStartInd == -1)
                return;
            id = Convert.ToInt32(p_msg[(idStartInd + 1)..]);
        }
        string? errMsg = MemDb.gMemDb.GetPortfolioRunResults(id, p_forcedStartDate, p_forcedEndDate, out PrtfRunResult prtfRunResultJs);
        // Send portfolio run result if available
        if (errMsg == null)
        {
            byte[] encodedMsg = Encoding.UTF8.GetBytes("PrtfVwr.PrtfRunResult:" + Utils.CamelCaseSerialize(prtfRunResultJs));
            if (webSocket!.State == WebSocketState.Open)
                webSocket.SendAsync(new ArraySegment<Byte>(encodedMsg, 0, encodedMsg.Length), WebSocketMessageType.Text, true, CancellationToken.None);
        }
        // Send error message if available
        if (errMsg != null)
        {
            byte[] encodedMsg = Encoding.UTF8.GetBytes("PrtfVwr.ErrorToUser:" + errMsg);
            if (webSocket!.State == WebSocketState.Open)
                webSocket.SendAsync(new ArraySegment<Byte>(encodedMsg, 0, encodedMsg.Length), WebSocketMessageType.Text, true, CancellationToken.None);
        }
    }
}