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
using Fin.Base;
using Fin.MemDb;
using Microsoft.AspNetCore.Http;
using QuantConnect;
using QuantConnect.Algorithm.CSharp;
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

class ChrtGenPrtfRunResultJs : ChrtGenPrtfItems // ChartGenerator doesn't need the Portfolio Positions & Statistics data
{
    public ChartData ChrtData { get; set; } = new();
}

class BmrkHistory
{
    public string SqTicker { get; set; } = string.Empty;
    public ChartData ChrtData { get; set; } = new();
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
        User? user = Array.Find(users, r => r.Email == email); // find the user

        // https://stackoverflow.com/questions/24450109/how-to-send-receive-messages-through-a-web-socket-on-windows-phone-8-using-the-c
        var msgObj = new HandshakeMessageChrtGen() { Email = email, FldrsToClient = UiUtils.GetPortfMgrFolders(user!), PrtfsToClient = UiUtils.GetPortfMgrPortfolios(user!) };
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
        // forcedStartDate and forcedEndDate are determined by specifed algorithm, if null (ex: please refer SqPctAllocation.cs file)
        DateTime? p_forcedStartDate = null;
        DateTime? p_forcedEndDate = null;
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
            if (MemDb.gMemDb.Portfolios.TryGetValue(int.Parse(pidStr), out Portfolio? prtf))
            {
                 Console.WriteLine($"Portfolio Name: '{prtf.Name}'");
                 lsPrtf.Add(prtf);
            }
        }

        DateTime minPortfoliosStartDate = DateTime.Today; // initialize currentDate to the Today's Date
        List<ChrtGenPrtfRunResultJs> chrtGenPrtfRunResultJs = new();
        // Step 2: Filling the chrtGenPrtfRunResultJs to a list.
        for (int i = 0; i < lsPrtf.Count; i++)
        {
            string? errMsg = lsPrtf[i].GetPortfolioRunResult(true, SqResultStat.NoStat, p_forcedStartDate, p_forcedEndDate, out PortfolioRunResultStatistics stat, out List<DateValue> pv, out List<PortfolioPosition> prtfPos, out ChartResolution chartResolution, out sqLogs);
            if (errMsg != null)
                sqLogs.Add(new SqLog { SqLogLevel = SqLogLevel.Error, Message = errMsg });
            ChartData chartVal = new();

            if (errMsg == null)
            {
                // Step 3: Filling the ChartPoint Dates and Values to a list. A very condensed format. Dates are separated into its ChartDate List.
                // Instead of the longer [{"ChartDate": 1641013200, "Value": 101665}, {"ChartDate": 1641013200, "Value": 101665}, {"ChartDate": 1641013200, "Value": 101665}]
                // we send a shorter: { ChartDate: [1641013200, 1641013200, 1641013200], Value: [101665, 101665, 101665] }

                // Portfolios DateFormat Processing based on ChartResolution
                chartVal.ChartResolution = chartResolution;
                DateTime startDate = DateTime.MinValue;
                DateTimeFormat dateTimeFormat;
                if (chartResolution == ChartResolution.Daily)
                {
                    dateTimeFormat = DateTimeFormat.DaysFromADate;
                    startDate = pv[0].Date.Date;
                    chartVal.DateTimeFormat = "DaysFrom" + startDate.ToYYYYMMDD(); // the standard choice in Production. It results in less data to be sent. Date strings will be only numbers such as 0,1,2,3,4,5,8 (skipping weekends)

                    // dateTimeFormat = DateTimeFormat.YYYYMMDD;
                    // chartVal.DateTimeFormat = "YYYYMMDD"; // YYYYMMDD is a better choice if we debug data sending. (to see it in the TXT message. Or to easily convert it to CSV in Excel)
                }
                else
                {
                    dateTimeFormat = DateTimeFormat.SecSince1970;
                    chartVal.DateTimeFormat = "SecSince1970"; // if it is higher resolution than daily, then we use per second resolution for data
                }

                foreach (DateValue item in pv)
                {
                    DateTime itemDate = item.Date.Date;
                    if (itemDate < minPortfoliosStartDate)
                        minPortfoliosStartDate = itemDate; // MinStart Date of the portfolio's

                    if (dateTimeFormat == DateTimeFormat.SecSince1970)
                    {
                        long unixTimeInSec = new DateTimeOffset(item.Date).ToUnixTimeSeconds();
                        chartVal.Dates.Add(unixTimeInSec);
                    }
                    else if (dateTimeFormat == DateTimeFormat.YYYYMMDD)
                    {
                        int dateInt = itemDate.Year * 10000 + itemDate.Month * 100 + itemDate.Day;
                        chartVal.Dates.Add(dateInt);
                    }
                    else // dateTimeFormat == DateTimeFormat.DaysFromADate
                    {
                        int nDaysFromStartDate = (int)(itemDate - startDate).TotalDays; // number of days since startDate
                        chartVal.Dates.Add(nDaysFromStartDate);
                    }

                    chartVal.Values.Add(item.Value);
                }
            }
            _ = prtfPos; // To avoid the compiler Warning "Unnecessary assigment of a value" for unusued variables.
            _ = stat; // To avoid the compiler Warning "Unnecessary assigment of a value" for unusued variables.
            // Step 5: Filling the data in chrtGenPrtfRunResultJs
            chrtGenPrtfRunResultJs.Add(new ChrtGenPrtfRunResultJs { Id = lsPrtf[i].Id, Name = lsPrtf[i].Name, ChrtData = chartVal });
        }

        // BENCHMARK: Processing the message to extract the benchmark tickers
        string? bmrksStr = query.Get("bmrks");
        if (string.IsNullOrEmpty(bmrksStr))
            sqLogs.Add(new SqLog { SqLogLevel = SqLogLevel.Info, Message = $"The bmrksStr from the client is null. We process the pidStr further." });

        if (minPortfoliosStartDate == DateTime.Today) // Default date (2020-01-01) if minStartdate == today
            minPortfoliosStartDate = QCAlgorithmUtils.g_earliestQcDay; // we are giving mindate as (1900-01-01) so that it gets all the data available if its only processing the benchmarks. DateTime.MinValue cannot be used, because QC.HistoryProvider.GetHistory() will convert this time to UTC, but taking away 5 hours from MinDate is not possible.
        List<BmrkHistory> bmrkHistories = new();
        foreach (string bmrkTicker in bmrksStr!.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            string? errMsg = Portfolio.GetBmrksHistoricalResults(bmrkTicker, minPortfoliosStartDate, out PriceHistoryJs histPrcs, out ChartResolution chartResolution);
            if (errMsg == null)
            {
                ChartData chartValBmrk = new();
                chartValBmrk.ChartResolution = chartResolution;
                DateTime startDate = DateTime.MinValue;
                DateTimeFormat dateTimeFormat;
                if (chartResolution == ChartResolution.Daily)
                {
                    dateTimeFormat = DateTimeFormat.DaysFromADate;
                    startDate = DateTimeOffset.FromUnixTimeSeconds(histPrcs.Dates[0]).DateTime.Date;
                    chartValBmrk.DateTimeFormat = "DaysFrom" + startDate.ToYYYYMMDD(); // the standard choice in Production. It results the less data to be sent. Date strings will be only numbers such as 0,1,2,3,4,5,8 (skipping weekends)

                    // dateTimeFormat = DateTimeFormat.YYYYMMDD;
                    // chartValBmrk.DateTimeFormat = "YYYYMMDD"; // YYYYMMDD is a better choice if we debug data sending. (to see it in the TXT message. Or to easily convert it to CSV in Excel)
                }
                else
                {
                    dateTimeFormat = DateTimeFormat.SecSince1970;
                    chartValBmrk.DateTimeFormat = "SecSince1970"; // if it is higher resolution than daily, then we use per second resolution for data
                }

                for (int i = 0; i < histPrcs.Dates.Count; i++)
                {
                    DateTime itemDate = DateTimeOffset.FromUnixTimeSeconds(histPrcs.Dates[i]).DateTime;
                    if (itemDate < minPortfoliosStartDate)
                        minPortfoliosStartDate = itemDate;

                    if (dateTimeFormat == DateTimeFormat.SecSince1970)
                        chartValBmrk.Dates.Add(histPrcs.Dates[i]);
                    else if (dateTimeFormat == DateTimeFormat.YYYYMMDD)
                    {
                        int dateInt = itemDate.Year * 10000 + itemDate.Month * 100 + itemDate.Day;
                        chartValBmrk.Dates.Add(dateInt);
                    }
                    else // dateTimeFormat ==  DateTimeFormat.DaysFromADate
                    {
                        int nDaysFromStartDate = (int)(itemDate - startDate).TotalDays; // number of days since startDate
                        chartValBmrk.Dates.Add(nDaysFromStartDate);
                    }

                    chartValBmrk.Values.Add((float)histPrcs.Prices[i]);
                }
                bmrkHistories.Add(new BmrkHistory { SqTicker = bmrkTicker, ChrtData = chartValBmrk });
            }
            else
                sqLogs.Add(new SqLog { SqLogLevel = SqLogLevel.Warn, Message = $"The Benchmark Tickers {bmrkTicker} not found in DB. ErrMsg {errMsg}" });
        }

        // Step 6: send back the result
        stopwatch.Stop(); // Stopwatch to capture the end time
        chrtGenBacktestResult.PfRunResults = chrtGenPrtfRunResultJs; // Set the portfolio run results in the backtest result object
        chrtGenBacktestResult.BmrkHistories = bmrkHistories;
        chrtGenBacktestResult.Logs = sqLogs;
        chrtGenBacktestResult.ServerBacktestTimeMs = (int)stopwatch.ElapsedMilliseconds; // Set the server backtest time in milliseconds

        string backtestResultStr = Utils.CamelCaseSerialize(chrtGenBacktestResult);

        byte[] encodedMsg = Encoding.UTF8.GetBytes("BacktestResults:" + backtestResultStr);
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