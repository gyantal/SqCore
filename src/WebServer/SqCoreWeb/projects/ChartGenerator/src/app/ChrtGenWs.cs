using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
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
                HistoricalBmrksResults(msgObjStr, webSocket);
                break;
            default:
                Utils.Logger.Info($"ChrtGen.OnWsReceiveAsync(): Unrecognized message from client, {msgCode},{msgObjStr}");
                break;
        }
    }

    public static void BacktestResults(string? p_msg, WebSocket webSocket) // msg: ?pids=1,2&bmrks=QQQ,SPY
    {
        string? errMsg = null;
        if (p_msg == null)
            errMsg = $"Error. msg from the client is null";

        // Step1: Processing the message to extract the Id
        NameValueCollection query = HttpUtility.ParseQueryString(p_msg!);
        string? pidsStr = query.Get("pids");
        if (pidsStr == null)
            errMsg = $"Error. pidsStr from the client is null"; // we should send the user an error message

        List<Portfolio> lsPrtf = new();
        foreach (string pidStr in pidsStr!.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            int pid = Convert.ToInt32(pidStr);
            if (MemDb.gMemDb.Portfolios.TryGetValue(pid, out Portfolio? prtf))
            {
                 Console.WriteLine($"Portfolio Name: '{prtf.Name}'");
                 lsPrtf.Add(prtf);
            }
            else
                errMsg = $"Error. Portfolio id {pid} not found in DB";
        }

        if (errMsg == null)
        {
            List<PrtfRunResultJs> pfRunResults = new();
            for (int i = 0; i < lsPrtf.Count; i++)
            {
                errMsg = lsPrtf[i].GetPortfolioRunResult(out PortfolioRunResultStatistics stat, out List<ChartPoint> pv, out List<PortfolioPosition> prtfPos);
                ChartPointValues chartVal = new();
                PortfolioRunResultStatistics pStat = new();
                List<PortfolioPosition> prtfPoss = new();

                if (errMsg == null)
                {
                    // Step3: Filling the ChartPoint Dates and Values to a list. A very condensed format. Dates are separated into its ChartDate List.
                    // Instead of the longer [{"ChartDate": 1641013200, "Value": 101665}, {"ChartDate": 1641013200, "Value": 101665}, {"ChartDate": 1641013200, "Value": 101665}]
                    // we send a shorter: { ChartDate: [1641013200, 1641013200, 1641013200], Value: [101665, 101665, 101665] }
                    foreach (var item in pv)
                    {
                        chartVal.Dates.Add(item.x);
                        chartVal.Values.Add((int)item.y);
                    }

                    // Step4: Filling the Stats data
                    pStat.StartPortfolioValue = stat.StartPortfolioValue;
                    pStat.EndPortfolioValue = stat.EndPortfolioValue;
                    pStat.TotalReturn = stat.TotalReturn;
                    pStat.CAGR = stat.CAGR;
                    pStat.MaxDD = stat.MaxDD;
                    pStat.SharpeRatio = stat.SharpeRatio;
                    pStat.StDev = stat.StDev;
                    pStat.Ulcer = stat.Ulcer;
                    pStat.TradingDays = stat.TradingDays;
                    pStat.NTrades = stat.NTrades;
                    pStat.WinRate = stat.WinRate;
                    pStat.LossRate = stat.LossRate;
                    pStat.Sortino = stat.Sortino;
                    pStat.Turnover = stat.Turnover;
                    pStat.LongShortRatio = stat.LongShortRatio;
                    pStat.Fees = stat.Fees;
                    pStat.BenchmarkCAGR = stat.BenchmarkCAGR;
                    pStat.BenchmarkMaxDD = stat.BenchmarkMaxDD;
                    pStat.CorrelationWithBenchmark = stat.CorrelationWithBenchmark;

                    // Step5: Filling the PrtfPoss data
                    foreach (var item in prtfPos)
                    {
                        prtfPoss.Add(new PortfolioPosition { SqTicker = item.SqTicker, Quantity = item.Quantity, AvgPrice = item.AvgPrice, LastPrice = item.LastPrice });
                    }
                }
                // Step6: Filling the Stats, ChartPoint vals and prtfPoss in pfRunResults
                pfRunResults.Add(new PrtfRunResultJs { Pstat = pStat, Chart = chartVal, PrtfPoss = prtfPoss });
            }

            // Step7: Sending the pfRunResults data to client
            if (pfRunResults != null)
                {
                    byte[] encodedMsg = Encoding.UTF8.GetBytes("PrtfRunResults:" + Utils.CamelCaseSerialize(pfRunResults));
                    if (webSocket!.State == WebSocketState.Open)
                        webSocket.SendAsync(new ArraySegment<Byte>(encodedMsg, 0, encodedMsg.Length), WebSocketMessageType.Text, true, CancellationToken.None);
                }
        }

        if (errMsg != null)
        {
            byte[] encodedMsg = Encoding.UTF8.GetBytes("ErrorToUser:" + errMsg);
            if (webSocket!.State == WebSocketState.Open)
                webSocket.SendAsync(new ArraySegment<Byte>(encodedMsg, 0, encodedMsg.Length), WebSocketMessageType.Text, true, CancellationToken.None);
        }
    }

// temporaryly writing as a separate function, we will integrate once the method is finalized
    public static void HistoricalBmrksResults(string? p_msg, WebSocket webSocket) // msg: ?pids=1,2&bmrks=QQQ,SPY
    {
        string? errMsg = null;
        if (p_msg == null)
            errMsg = $"Error. msg from the client is null";

        NameValueCollection query = HttpUtility.ParseQueryString(p_msg!);
        // Step1: Processing the message to extract the benchmark tickers
        string? bmrksStr = query.Get("bmrks");
        if (bmrksStr == null)
            errMsg = $"Error. bmrksStr from the client is null";

        List<HistoricalPrice> histPrices = new();
        if(errMsg == null)
        {
            foreach (string bmrkStr in bmrksStr!.Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                errMsg = Portfolio.GetBmrksHistoricalResults(bmrkStr, out List<HistoricalPrice> histPrcs);
                if(errMsg == null)
                {
                    for (int i = 0; i < histPrcs.Count; i++)
                        histPrices.Add(new HistoricalPrice { SqTicker = "S" + histPrcs[i].SqTicker, Date = histPrcs[i].Date, High = histPrcs[i].High, Low = histPrcs[i].Low, Open = histPrcs[i].Open, Close = histPrcs[i].Close, Price = histPrcs[i].Price });
                }
                else
                    errMsg = $"Error. Benchmark Tickers {bmrkStr} not found in DB";
            }

            if (histPrices != null)
                {
                    byte[] encodedMsg = Encoding.UTF8.GetBytes("BenchmarkResults:" + Utils.CamelCaseSerialize(histPrices));
                    if (webSocket!.State == WebSocketState.Open)
                        webSocket.SendAsync(new ArraySegment<Byte>(encodedMsg, 0, encodedMsg.Length), WebSocketMessageType.Text, true, CancellationToken.None);
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