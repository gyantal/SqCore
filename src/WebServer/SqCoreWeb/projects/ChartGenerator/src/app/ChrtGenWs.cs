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
        string? queryStr = context.Request.QueryString.Value;
        BacktestResults(queryStr, webSocket);
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
                BacktestResults(msgObjStr, webSocket);
                break;
            default:
                Utils.Logger.Info($"ChrtGen.OnWsReceiveAsync(): Unrecognized message from client, {msgCode},{msgObjStr}");
                break;
        }
    }

// Yet to Develop - Daya
    public static void BacktestResults(string? p_msg, WebSocket webSocket)
    {
        // Step1: Processing the message to extract the Id
        int idStartInd = p_msg!.IndexOf(":");
        if (idStartInd == -1)
            return;
        int id = Convert.ToInt32(p_msg[(idStartInd + 1)..]);

        // Step2: Getting the BackTestResults
        string? errMsg = null;
        if (MemDb.gMemDb.Portfolios.TryGetValue(id, out Portfolio? prtf))
            Console.WriteLine($"Portfolio Name: '{prtf.Name}'");
        else
            errMsg = $"Error. Portfolio id {id} not found in DB";

        if (errMsg == null)
        {
            errMsg = prtf!.GetPortfolioRunResult(out PortfolioRunResultStatistics stat, out List<ChartPoint> pv, out List<PortfolioPosition> prtfPos);
            if (errMsg == null)
            {
                // Step3: Filling the ChartPoint Dates and Values to a list. A very condensed format. Dates are separated into its ChartDate List.
                // Instead of the longer [{"ChartDate": 1641013200, "Value": 101665}, {"ChartDate": 1641013200, "Value": 101665}, {"ChartDate": 1641013200, "Value": 101665}]
                // we send a shorter: { ChartDate: [1641013200, 1641013200, 1641013200], Value: [101665, 101665, 101665] }
                ChartPointValues chartVal = new();
                foreach (var item in pv)
                {
                    chartVal.Dates.Add(item.x);
                    chartVal.Values.Add((int)item.y);
                }

                // Step4: Filling the Stats data
                PortfolioRunResultStatistics pStat = new()
                {
                    StartPortfolioValue = stat.StartPortfolioValue,
                    EndPortfolioValue = stat.EndPortfolioValue,
                    TotalReturn = stat.TotalReturn,
                    CAGR = stat.CAGR,
                    MaxDD = stat.MaxDD,
                    SharpeRatio = stat.SharpeRatio,
                    StDev = stat.StDev,
                    Ulcer = stat.Ulcer,
                    TradingDays = stat.TradingDays,
                    NTrades = stat.NTrades,
                    WinRate = stat.WinRate,
                    LossRate = stat.LossRate,
                    Sortino = stat.Sortino,
                    Turnover = stat.Turnover,
                    LongShortRatio = stat.LongShortRatio,
                    Fees = stat.Fees,
                    BenchmarkCAGR = stat.BenchmarkCAGR,
                    BenchmarkMaxDD = stat.BenchmarkMaxDD,
                    CorrelationWithBenchmark = stat.CorrelationWithBenchmark
                };

                // Step5: Filling the PrtfPoss data
                List<PortfolioPosition> prtfPoss = new();
                foreach (var item in prtfPos)
                {
                    prtfPoss.Add(new PortfolioPosition { SqTicker = item.SqTicker, Quantity = item.Quantity, AvgPrice = item.AvgPrice, LastPrice = item.LastPrice });
                }

                // Step6: Filling the Stats, ChartPoint vals and prtfPoss in pfRunResults
                PrtfRunResultJs pfRunResult = new()
                {
                    Pstat = pStat,
                    Chart = chartVal,
                    PrtfPoss = prtfPoss
                };

                // Step7: Sending the pfRunResults data to client
                if (pfRunResult != null)
                {
                    byte[] encodedMsg = Encoding.UTF8.GetBytes("PrtfRunResult:" + Utils.CamelCaseSerialize(pfRunResult));
                    if (webSocket!.State == WebSocketState.Open)
                        webSocket.SendAsync(new ArraySegment<Byte>(encodedMsg, 0, encodedMsg.Length), WebSocketMessageType.Text, true, CancellationToken.None);
                }
            }
        }

        if (errMsg != null)
        {
            byte[] encodedMsg = Encoding.UTF8.GetBytes("ErrorToUser:" + errMsg);
            if (webSocket!.State == WebSocketState.Open)
                webSocket.SendAsync(new ArraySegment<Byte>(encodedMsg, 0, encodedMsg.Length), WebSocketMessageType.Text, true, CancellationToken.None);
        }
    }

    public static void OnWsClose(WebSocket webSocket)
    {
        _ = webSocket; // StyleCop SA1313 ParameterNamesMustBeginWithLowerCaseLetter. They won't fix. Recommended solution for unused parameters, instead of the discard (_1) parameters
    }
}