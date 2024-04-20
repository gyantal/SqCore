using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Fin.Base;
using Fin.MemDb;
using Microsoft.AspNetCore.Http;
using SqCommon;

namespace SqCoreWeb;

class HandshakeMessagePrtfViewer
{
    public string Email { get; set; } = string.Empty;
    public int AnyParam { get; set; } = 75;
    public PortfolioJs PrtfToClient { get; set; } = new();
}

class FundamentalData
{
    public string? CompanyReference_ShortName { get; set; }
    public int CompanyProfile_SharesOutstanding { get; set; }
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
        string pfIdStr = queryStr[(idStartInd + 1)..];
        if (!int.TryParse(pfIdStr, out int pfId))
            throw new Exception($"OnWsConnectedAsync(), cannot find pfId {pfIdStr}");
        // https://stackoverflow.com/questions/24450109/how-to-send-receive-messages-through-a-web-socket-on-windows-phone-8-using-the-c
        var msgObj = new HandshakeMessagePrtfViewer() { Email = email, PrtfToClient = UiUtils.GetPortfolioJs(pfId) };
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

    public static void OnWsReceiveAsync(/* HttpContext context, WebSocketReceiveResult? result, */ WebSocket webSocket, string bufferStr)
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
            case "GetFundamentalData":
                Utils.Logger.Info($"PrtfVwrWs.OnWsReceiveAsync(): GetFundamentalData: '{msgObjStr}'");
                PortfVwrGetFundamentalData(webSocket, msgObjStr);
                break;
            case "GetTradesHist":
                Utils.Logger.Info($"PrtfVwrWs.OnWsReceiveAsync(): GetTradesHist: '{msgObjStr}'");
                PortfVwrGetPortfolioTradesHistory(webSocket, msgObjStr);
                break;
            case "InsertOrUpdateTrade":
                Utils.Logger.Info($"PrtfVwrWs.OnWsReceiveAsync(): InsertOrUpdateTrade: '{msgObjStr}'");
                PortfVwrInsertOrUpdateTrade(webSocket, msgObjStr);
                break;
            case "DeleteTrade":
                Utils.Logger.Info($"PrtfVwrWs.OnWsReceiveAsync(): DeleteTrade: '{msgObjStr}'");
                PortfVwrDeleteTrade(webSocket, msgObjStr);
                break;
            default:
                Utils.Logger.Info($"PrtfVwrWs.OnWsReceiveAsync(): Unrecognized message from client, {msgCode},{msgObjStr}");
                break;
        }
    }

    private static void PortfVwrGetFundamentalData(WebSocket webSocket, string p_msg) // p_msg - "?tickers=SPY,VNQ,USD&Date=" or "?tickers=SPY,VNQ,USD&Date=2022-01-01"
    {
        int tickersStartInd = p_msg.IndexOf("=");
        if (tickersStartInd == -1)
            return;

        int dateStartInd = p_msg.IndexOf("=", tickersStartInd + 1);
        if (dateStartInd == -1)
            return;

        string dateStr = p_msg[(dateStartInd + 1)..]; // Extract the date string from the message(p_msg)
        DateTime date = DateTime.Now;
        if (dateStr.Length > 0)
            date = Utils.Str2DateTimeUtc(dateStr);
        string tickersStr = p_msg.Substring(tickersStartInd + 1, dateStartInd - tickersStartInd - "&Date=".Length);
        List<string> tickers = new (); // tickers list to get fundamentalData
        foreach (var ticker in tickersStr.Split(','))
        {
            if(ticker.Trim() != "USD") // Filter out "USD" ticker as it's a currency and won't have fundamental data
                tickers.Add(ticker);
        }
        List<FundamentalProperty> propertyNames = new() { FundamentalProperty.CompanyReference_ShortName, FundamentalProperty.CompanyProfile_SharesOutstanding }; // Define the fundamental properties to fetch
        string fundamentalDataStr = FinDb.GetFundamentalDataStr(tickers, date, propertyNames); // Fetch fundamental data from the database
        // Deserialize the JSON data into a Dictionary<string, FundamentalData>
        Dictionary<string, FundamentalData>? fundamentalData = JsonSerializer.Deserialize<Dictionary<string, FundamentalData>>(fundamentalDataStr, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        byte[] encodedMsg = Encoding.UTF8.GetBytes("PrtfVwr.PrtfTickersFundamentalData:" + Utils.CamelCaseSerialize(fundamentalData));
        if (webSocket!.State == WebSocketState.Open)
            webSocket.SendAsync(new ArraySegment<Byte>(encodedMsg, 0, encodedMsg.Length), WebSocketMessageType.Text, true, CancellationToken.None);
    }

    private static void PortfVwrInsertOrUpdateTrade(WebSocket webSocket, string p_msg) // p_msg - pfId:21:{"id":-1,"time":"2024-02-22T10:32:20.680Z","action":0,"assetType":7,"symbol":"META","underlyingSymbol":null,"quantity":0,"price":0,"currency":0,"commission":0,"exchangeId":-1,"connectedTrades":null}
    {
        int prtfIdStartInd = p_msg.IndexOf(":");
        if (prtfIdStartInd == -1)
            return;

        int trdObjStartInd = p_msg.IndexOf(":", prtfIdStartInd + 1);
        if (trdObjStartInd == -1)
            return;

        string prtfIdStr = p_msg.Substring(prtfIdStartInd + 1, trdObjStartInd - prtfIdStartInd - 1);
        if (!int.TryParse(prtfIdStr, out int pfId))
            throw new Exception($"PortfVwrInsertOrUpdateTrade(), cannot find pfId {prtfIdStr}");
        // Try to get the Portfolio from the MemDb using the extracted ID
        MemDb.gMemDb.Portfolios.TryGetValue(pfId, out Portfolio? pf);
        if (pf == null)
            return;

        string tradeObjStr = p_msg[(trdObjStartInd + 1)..]; // extract the Trade object string from p_msg
        Trade? trade = JsonSerializer.Deserialize<Trade>(tradeObjStr, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }); // Deserialize the trade string into a Trade object
        if (trade == null)
            return;

        int tradeHistId;
        if (pf.TradeHistoryId == -1) // non-existing tradeHistory
        {
            List<Trade> trades = new();
            tradeHistId = MemDb.gMemDb.InsertPortfolioTradeHistory(trades);
            pf.TradeHistoryId = tradeHistId;
            // Save this updated pf.TradeHistoryId into the RedisDb.
            MemDb.gMemDb.AddOrEditPortfolio(pf.Id, pf.User, pf.Name, pf.ParentFolderId, pf.Currency, pf.Type, pf.Algorithm, pf.AlgorithmParam, pf.SharedAccess, pf.Note, pf.TradeHistoryId, out Portfolio? p_newItem);
        }
        else // existing tradeHistory
            tradeHistId = pf.TradeHistoryId;

        if (trade.Id == -1)
            MemDb.gMemDb.InsertPortfolioTrade(tradeHistId, trade); // Insert the trade into the portfolio's trade history in Db
        else
            MemDb.gMemDb.UpdatePortfolioTrade(tradeHistId, trade.Id, trade); // update the trade into the portfolio's trade history in Db

        string? errMsg = MemDb.gMemDb.GetPortfolioRunResults(pfId, null, null, out PrtfRunResult prtfRunResultJs);

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

        PortfVwrGetPortfolioTradesHistory(webSocket, prtfIdStr); // After DB insert, force sending the whole TradeHistory to Client. Client shouldn't assume that DB insert was successful.
    }

    private static void PortfVwrDeleteTrade(WebSocket webSocket, string p_msg) // p_msg - pfId:21,tradeId:1
    {
        int prtfIdStartInd = p_msg.IndexOf(":");
        if (prtfIdStartInd == -1)
            return;

        int trdIdStartInd = p_msg.IndexOf(":", prtfIdStartInd + 1);
        if (trdIdStartInd == -1)
            return;

        string prtfIdStr = p_msg.Substring(prtfIdStartInd + 1, trdIdStartInd - prtfIdStartInd - ",tradeId:".Length);
        if (!int.TryParse(prtfIdStr, out int pfId))
            throw new Exception($"PortfVwrDeleteTrade(), cannot find pfId {prtfIdStr}");

        string tradeIdStr = p_msg[(trdIdStartInd + 1)..];
        if (!int.TryParse(tradeIdStr, out int tradeId))
            throw new Exception($"PortfVwrDeleteTrade(), cannot find tradeId {tradeIdStr}");

        MemDb.gMemDb.Portfolios.TryGetValue(pfId, out Portfolio? pf);
        if (pf == null)
            return;
        bool isDeleteTradeSuccess = MemDb.gMemDb.DeletePortfolioTrade(pf.TradeHistoryId, tradeId);
        if (isDeleteTradeSuccess)
        {
            List<Trade>? tradeHistory = MemDb.gMemDb.GetPortfolioTradeHistoryToList(pf.TradeHistoryId, null, null);
            if (tradeHistory?.Count == 0) // if trade history count is zero , we have to remove the tradeHistory and edit the portfolio by assinging pf.TradeHistoryId = -1
            {
                MemDb.gMemDb.DeletePortfolioTradeHistory(pf.TradeHistoryId); // Delete trade History from portfolio, otherwise it will keep the empty list of tradeHistory
                MemDb.gMemDb.AddOrEditPortfolio(pf.Id, pf.User, pf.Name, pf.ParentFolderId, pf.Currency, pf.Type, pf.Algorithm, pf.AlgorithmParam, pf.SharedAccess, pf.Note, pf.TradeHistoryId = -1, out Portfolio? p_newItem); // Update portfolio with no trade history
            }
        }
        else
            throw new Exception($"DeletePortfolioTrade(), cannot find tradeHistoryId {pf.ParentFolderId}");
        PortfVwrGetPortfolioTradesHistory(webSocket, prtfIdStr); // After DB Delete, force sending the whole TradeHistory to Client. Client shouldn't assume that DB Delete was successful.
    }

    // Here we get the p_msg in 2 forms
    // 1. when onConnected it comes as p_msg ="?pid=12".
    // 2. when user sends Historical Position Dates ?pid=12&Date=2022-01-01
    public static void PortfVwrGetPortfolioRunResults(WebSocket webSocket, string p_msg) // p_msg ="?pid=12" or ?pid=12&Date=2022-01-01
    {
        // forcedStartDate and forcedEndDate are determined by specifed algorithm, if null (ex: please refer SqPctAllocation.cs file)
        DateTime? p_forcedStartDate = null;
        DateTime? p_forcedEndDate = null;

        int idStartInd = p_msg.IndexOf("pid=");
        if (idStartInd == -1)
            return;
        idStartInd += "pid=".Length;
        int idEndInd = p_msg.IndexOf('&', idStartInd);
        int idLength = idEndInd == -1 ? p_msg.Length - idStartInd : idEndInd - idStartInd;

        string idStr = p_msg.Substring(idStartInd, idLength);
        if (!int.TryParse(idStr, out int id))
            throw new Exception($"PortfVwrGetPortfolioRunResults(), cannot find id {idStr}");

        // Check if p_msg contains "Date" to determine its format
        if (p_msg.Contains("Date")) // p_msg = "?pid=12&Date=2022-01-01"
        {
            int dateInd = p_msg.IndexOf("&Date=");
            if (dateInd == -1)
                return;
            string endDtStr = p_msg[(dateInd + "&Date=".Length)..];
            p_forcedEndDate = Utils.Str2DateTimeUtc(endDtStr);
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

    public static void PortfVwrGetPortfolioTradesHistory(WebSocket webSocket, string p_msg)
    {
        if (!int.TryParse(p_msg, out int id))
            throw new Exception($"PortfVwrGetPortfolioTradesHistory(), cannot find id {p_msg}");

        if (MemDb.gMemDb.Portfolios.TryGetValue(id, out Portfolio? pf))
        {
            IEnumerable<Trade> tradesHist = MemDb.gMemDb.GetPortfolioTradeHistory(pf.TradeHistoryId, null, null); // Retrieve trades history
            byte[] encodedMsg = Encoding.UTF8.GetBytes("PrtfVwr.TradesHist:" + Utils.CamelCaseSerialize(tradesHist));
            if (webSocket!.State == WebSocketState.Open)
                webSocket.SendAsync(new ArraySegment<Byte>(encodedMsg, 0, encodedMsg.Length), WebSocketMessageType.Text, true, CancellationToken.None);
        }
    }
}