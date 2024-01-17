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
    public List<PortfolioJs> PrtfsToClient { get; set; } = new();
    public List<FolderJs> FldrsToClient { get; set; } = new();
}

public class PrtfVwrWs
{
    public static async Task OnWsConnectedAsync(HttpContext context, WebSocket webSocket)
    {
        Utils.Logger.Debug($"PrtfVwrWs.OnConnectedAsync()) BEGIN");
        // context.Request comes as: 'wss://' + document.location.hostname + '/ws/prtfvwr?id=1,2'
        string? queryStr = context.Request.QueryString.Value;
        // RunBacktests(queryStr, webSocket);
        var userEmailClaim = context?.User?.Claims?.FirstOrDefault(p => p.Type == @"http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress");
        var email = userEmailClaim?.Value ?? "unknown@gmail.com";
        User[] users = MemDb.gMemDb.Users; // get the user data
        User? user = Array.Find(users, r => r.Email == email); // find the user

        // https://stackoverflow.com/questions/24450109/how-to-send-receive-messages-through-a-web-socket-on-windows-phone-8-using-the-c
        var msgObj = new HandshakeMessagePrtfViewer() { Email = email, FldrsToClient = UiUtils.GetPortfMgrFolders(user!), PrtfsToClient = UiUtils.GetPortfMgrPortfolios(user!) };
        byte[] encodedMsg = Encoding.UTF8.GetBytes("OnConnected:" + Utils.CamelCaseSerialize(msgObj));
        if (webSocket.State == WebSocketState.Open)
            await webSocket.SendAsync(new ArraySegment<Byte>(encodedMsg, 0, encodedMsg.Length), WebSocketMessageType.Text, true, CancellationToken.None); // takes 0.635ms
        if(queryStr != null)
            PrtfVwrSendPortfolioRunResults(queryStr, webSocket);
    }

    public static void OnWsClose(WebSocket webSocket)
    {
        _ = webSocket; // StyleCop SA1313 ParameterNamesMustBeginWithLowerCaseLetter. They won't fix. Recommended solution for unused parameters, instead of the discard (_1) parameters
    }

    public static void PrtfVwrSendPortfolioRunResults(string p_msg, WebSocket webSocket) // This method is similar to PortMgrSendPortfolioRunResults() - Need to discuss with George
    {
        // Step1: Processing the message to extract the Id
        int idStartInd = p_msg.IndexOf("=");
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
            errMsg = prtf!.GetPortfolioRunResult(SqResult.SqSimple, out PortfolioRunResultStatistics stat, out List<ChartPoint> pv, out List<PortfolioPosition> prtfPos, out ChartResolution chartResolution);
            if (errMsg == null)
            {
                // Step3: Filling the ChartPoint Dates and Values to a list. A very condensed format. Dates are separated into its ChartDate List.
                // Instead of the longer [{"ChartDate": 1641013200, "Value": 101665}, {"ChartDate": 1641013200, "Value": 101665}, {"ChartDate": 1641013200, "Value": 101665}]
                // we send a shorter: { ChartDate: [1641013200, 1641013200, 1641013200], Value: [101665, 101665, 101665] }
                ChartData chartVal = new();
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
                    Sharpe = stat.Sharpe,
                    CagrSharpe = stat.CagrSharpe,
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
                    ChrtData = chartVal,
                    PrtfPoss = prtfPoss
                };

                // Step7: Sending the pfRunResults data to client
                if (pfRunResult != null)
                {
                    byte[] encodedMsg = Encoding.UTF8.GetBytes("PrtfVwr.PrtfRunResult:" + Utils.CamelCaseSerialize(pfRunResult));
                    if (webSocket!.State == WebSocketState.Open)
                        webSocket.SendAsync(new ArraySegment<Byte>(encodedMsg, 0, encodedMsg.Length), WebSocketMessageType.Text, true, CancellationToken.None);
                }
            }
            _ = chartResolution; // To avoid the compiler Warning "Unnecessary assigment of a value" for unusued variables.
        }

        if (errMsg != null)
        {
            byte[] encodedMsg = Encoding.UTF8.GetBytes("PrtfVwr.ErrorToUser:" + errMsg);
            if (webSocket!.State == WebSocketState.Open)
                webSocket.SendAsync(new ArraySegment<Byte>(encodedMsg, 0, encodedMsg.Length), WebSocketMessageType.Text, true, CancellationToken.None);
        }
    }
}