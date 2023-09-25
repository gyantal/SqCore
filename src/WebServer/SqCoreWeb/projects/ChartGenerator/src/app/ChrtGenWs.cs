using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using Fin.MemDb;
using Microsoft.AspNetCore.Http;
using QuantConnect;
using QuantConnect.Parameters;
using SqCommon;

namespace SqCoreWeb;

// these members has to be C# properties, not simple data member tags. Otherwise it will not serialize to client.

public class ChrtGenPrtfItems // common for both folders and portfolios
{
    public int Id { get; set; } = 1;
    public string Name { get; set; } = string.Empty;
}
class HandshakeMessageChrtGen
{
    public string Email { get; set; } = string.Empty;
    public int AnyParam { get; set; } = 75;
    public List<PortfolioJs> PrtfsToClient { get; set; } = new();
    public List<FolderJs> FldrsToClient { get; set; } = new();
}

class ChrtGenPrtfRunResultJs : ChrtGenPrtfItems // ChartGenerator doesn't need the Portfolio Positions data
{
    public PortfolioRunResultStatistics Pstat { get; set; } = new();
    public ChartData ChrtData { get; set; } = new();
}

class BmrkHistory
{
    public string SqTicker { get; set; } = string.Empty;
    public PriceHistoryJs HistPrices { get; set; } = new();
}

class ChrtGenBacktestResult
{
    public List<ChrtGenPrtfRunResultJs> PfRunResults { get; set; } = new();
    public List<BmrkHistory> BmrkHistories { get; set; } = new();
    public List<SqLog> Logs { get; set; } = new();
    public int ServerBacktestTimeMs { get; set; } = -1;
}

public class ChrtGenWs
{
    public static async Task OnWsConnectedAsync(HttpContext context, WebSocket webSocket)
    {
        Utils.Logger.Debug($"ChrtGenWs.OnConnectedAsync()) BEGIN");
        // context.Request comes as: 'wss://' + document.location.hostname + '/ws/chrtgen?pids=1,2&bmrks=QQQ,SPY'
        string? queryStr = context.Request.QueryString.Value;
        RunBacktests(queryStr, webSocket);
        var userEmailClaim = context?.User?.Claims?.FirstOrDefault(p => p.Type == @"http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress");
        var email = userEmailClaim?.Value ?? "unknown@gmail.com";
        User[] users = MemDb.gMemDb.Users; // get the user data
        User? p_user = Array.Find(users, r => r.Email == email); // find the user

        // https://stackoverflow.com/questions/24450109/how-to-send-receive-messages-through-a-web-socket-on-windows-phone-8-using-the-c
        var msgObj = new HandshakeMessageChrtGen() { Email = email, FldrsToClient = UiUtils.GetPortfMgrFolders(p_user!), PrtfsToClient = UiUtils.GetPortfMgrPortfolios(p_user!) };
        byte[] encodedMsg = Encoding.UTF8.GetBytes("OnConnected:" + Utils.CamelCaseSerialize(msgObj));
        if (webSocket.State == WebSocketState.Open)
            await webSocket.SendAsync(new ArraySegment<Byte>(encodedMsg, 0, encodedMsg.Length), WebSocketMessageType.Text, true, CancellationToken.None); // takes 0.635ms
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
                RunBacktests(msgObjStr, webSocket);
                break;
            default:
                Utils.Logger.Info($"ChrtGen.OnWsReceiveAsync(): Unrecognized message from client, {msgCode},{msgObjStr}");
                break;
        }
    }

    public static void RunBacktests(string? p_msg, WebSocket webSocket) // msg: ?pids=1,2&bmrks=QQQ,SPY
    {
        Stopwatch stopwatch = Stopwatch.StartNew(); // Stopwatch to capture the start time
        ChrtGenBacktestResult chrtGenBacktestResult = new();
        List<SqLog> sqLogs = new();
        if (string.IsNullOrEmpty(p_msg))
            sqLogs.Add(new SqLog { SqLogLevel = SqLogLevel.Warn, Message = $"The msg from the client is null" });

        // Step 1: generate the Portfolios. Can run in a multithreaded way.
        NameValueCollection query = HttpUtility.ParseQueryString(p_msg!); // Parse the query string from the input message
        string? pidsStr = query.Get("pids"); // Get the value of the "pids" parameter from the query string
        if (string.IsNullOrEmpty(pidsStr))
            sqLogs.Add(new SqLog { SqLogLevel = SqLogLevel.Info, Message = $"The pidsStr from the client is null. We process the benchmarks further." });

        List<Portfolio> lsPrtf = new(); // Create a new list to store the portfolios
        foreach (string pidStr in pidsStr!.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            if (MemDb.gMemDb.Portfolios.TryGetValue(Convert.ToInt32(pidStr), out Portfolio? prtf))
            {
                 Console.WriteLine($"Portfolio Name: '{prtf.Name}'");
                 lsPrtf.Add(prtf);
            }
        }

        DateTime minStartDate = DateTime.Today; // initialize currentDate to the Today's Date
        List<ChrtGenPrtfRunResultJs> chrtGenPrtfRunResultJs = new();
        // Step 2: Filling the chrtGenPrtfRunResultJs to a list.
        for (int i = 0; i < lsPrtf.Count; i++)
        {
            string? errMsg = lsPrtf[i].GetPortfolioRunResult(SqResult.SqPvOnly, out PortfolioRunResultStatistics stat, out List<ChartPoint> pv, out List<PortfolioPosition> prtfPos, out ChartResolution chartResolution);
            if (errMsg != null)
                sqLogs.Add(new SqLog { SqLogLevel = SqLogLevel.Error, Message = errMsg });
            ChartData chartVal = new();
            PortfolioRunResultStatistics pStat = new();

            if (errMsg == null)
            {
                // Step 3: Filling the ChartPoint Dates and Values to a list. A very condensed format. Dates are separated into its ChartDate List.
                // Instead of the longer [{"ChartDate": 1641013200, "Value": 101665}, {"ChartDate": 1641013200, "Value": 101665}, {"ChartDate": 1641013200, "Value": 101665}]
                // we send a shorter: { ChartDate: [1641013200, 1641013200, 1641013200], Value: [101665, 101665, 101665] }
                chartVal.ChartResolution = chartResolution;
                foreach (var item in pv)
                {
                    DateTime itemDate = DateTimeOffset.FromUnixTimeSeconds(item.x).DateTime.Date;
                    if (itemDate < minStartDate)
                        minStartDate = itemDate; // MinStart Date of the portfolio's
                    chartVal.Dates.Add(item.x);
                    chartVal.Values.Add((int)item.y);
                }

                // Step 4: Filling the Stats data
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
            }
            _ = prtfPos; // To avoid the compiler Warning "Unnecessary assigment of a value" for unusued variables.
            // Step 5: Filling the data in chrtGenPrtfRunResultJs
            chrtGenPrtfRunResultJs.Add(new ChrtGenPrtfRunResultJs { Id = lsPrtf[i].Id, Name = lsPrtf[i].Name, Pstat = pStat, ChrtData = chartVal });
        }

        // BENCHMARK: Processing the message to extract the benchmark tickers
        string? bmrksStr = query.Get("bmrks");
        if (string.IsNullOrEmpty(bmrksStr))
            sqLogs.Add(new SqLog { SqLogLevel = SqLogLevel.Info, Message = $"The bmrksStr from the client is null. We process the pidStr further." });

        if(minStartDate == DateTime.Today) // Default date (2020-01-01) if minStartdate == today
            minStartDate = new DateTime(1900, 01, 01); // we are giving mindate as (1900-01-01) so that it gets all the data available if its only processing the benchmarks
        List<BmrkHistory> bmrkHistories = new();
        foreach (string bmrkTicker in bmrksStr!.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            string? errMsg = Portfolio.GetBmrksHistoricalResults(bmrkTicker, minStartDate, out PriceHistoryJs histPrcs);
            if (errMsg == null)
                bmrkHistories.Add(new BmrkHistory { SqTicker = bmrkTicker, HistPrices = histPrcs });
            else
                sqLogs.Add(new SqLog { SqLogLevel = SqLogLevel.Warn, Message = $"The Benchmark Tickers {bmrkTicker} not found in DB. ErrMsg {errMsg}" });
        }

        // Step 6: send back the result
        stopwatch.Stop(); // Stopwatch to capture the end time
        chrtGenBacktestResult.PfRunResults = chrtGenPrtfRunResultJs; // Set the portfolio run results in the backtest result object
        chrtGenBacktestResult.BmrkHistories = bmrkHistories;
        chrtGenBacktestResult.Logs = sqLogs;
        chrtGenBacktestResult.ServerBacktestTimeMs = (int)stopwatch.ElapsedMilliseconds; // Set the server backtest time in milliseconds

        byte[] encodedMsg = Encoding.UTF8.GetBytes("BacktestResults:" + Utils.CamelCaseSerialize(chrtGenBacktestResult));
        if (webSocket!.State == WebSocketState.Open)
            webSocket.SendAsync(new ArraySegment<Byte>(encodedMsg, 0, encodedMsg.Length), WebSocketMessageType.Text, true, CancellationToken.None);
    }

    public static void OnWsClose(WebSocket webSocket)
    {
        _ = webSocket; // StyleCop SA1313 ParameterNamesMustBeginWithLowerCaseLetter. They won't fix. Recommended solution for unused parameters, instead of the discard (_1) parameters
    }

    public static List<ChrtGenPrtfItems> ChrtGenGetPortfolios()
    {
        Dictionary<int, Portfolio>.ValueCollection prtfs = MemDb.gMemDb.Portfolios.Values;
        List<ChrtGenPrtfItems> prtfToClient = new();

        foreach (Portfolio pf in prtfs)
        {
            ChrtGenPrtfItems pfJs = new() { Id = pf.Id, Name = pf.Name };
            prtfToClient.Add(pfJs);
        }
        return prtfToClient;
    }

    public static List<ChrtGenPrtfItems> ChrtGenGetFolders()
    {
        Dictionary<int, PortfolioFolder>.ValueCollection prtfFldrs = MemDb.gMemDb.PortfolioFolders.Values;
        List<ChrtGenPrtfItems> fldrToClient = new();

        foreach (PortfolioFolder fld in prtfFldrs)
        {
            ChrtGenPrtfItems fldJs = new() { Id = fld.Id, Name = fld.Name };
            fldrToClient.Add(fldJs);
        }
        return fldrToClient;
    }
}