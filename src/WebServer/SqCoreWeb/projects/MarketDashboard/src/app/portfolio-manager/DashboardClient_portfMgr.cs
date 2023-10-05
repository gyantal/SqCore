using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading;
using Fin.MemDb;
using QuantConnect;
using QuantConnect.Parameters;
using SqCommon;

namespace SqCoreWeb;

enum PrtfItemType { Folder, Portfolio }

class HandshakePortfMgr // Initial params: keept it small
{
    public string UserName { get; set; } = string.Empty;
}

class PrtfRunResultJs
{
    public PortfolioRunResultStatistics Pstat { get; set; } = new();
    public ChartData ChrtData { get; set; } = new();
    public List<PortfolioPosition> PrtfPoss { get; set; } = new();
}

class ChartData
{
    public ChartResolution ChartResolution { get; set; } = ChartResolution.Daily;
    public List<long> Dates { get; set; } = new List<long>();

    // PV values are usually $100,000, $100,350,..., but PV can be around $1000 too and in that case decimals become important and we cannot use 'int' for storing them.
    // So we have to use either decimal (16 bytes) or float (4 byte) or double (8 bytes)
    // We don't like decimal (16 bytes), but the reason to keep it is that QC gives decimal values. Converting it to int or float requires some CPU conversion. But we should do it. For saving RAM. (20KB float List instead of the 80KB decimal List)
    // If we use float (4 bytes), we should use the FloatJsonConverterToNumber4D attribute for generating maximum 4 decimals: (100.342222225 decimal => float 100.3422)
    [JsonConverter(typeof(FloatListJsonConverterToNumber4D))]
    public List<float> Values { get; set; } = new List<float>(); // if 7 digit mantissa precision is enough then use float, otherwise double. Float:  if PV values of 10M = 10,000,000 we don't have any decimals, which is fine usually
}

public partial class DashboardClient
{
    // Return from this function very quickly. Do not call any Clients.Caller.SendAsync(), because client will not notice that connection is Connected, and therefore cannot send extra messages until we return here
    public void OnConnectedWsAsync_PortfMgr(bool p_isThisActiveToolAtConnectionInit)
    {
        Utils.RunInNewThread(ignored => // running parallel on a ThreadPool thread, FireAndForget: QueueUserWorkItem [26microsec] is 25% faster than Task.Run [35microsec]
        {
            Utils.Logger.Debug($"OnConnectedWsAsync_PortfMgr BEGIN, Connection from IP: {this.ClientIP} with email '{this.UserEmail}'");
            Thread.CurrentThread.IsBackground = true;  // thread will be killed when all foreground threads have died, the thread will not keep the application alive.

            HandshakePortfMgr handshake = GetHandshakePortfMgr();
            byte[] encodedMsg = Encoding.UTF8.GetBytes("PortfMgr.Handshake:" + Utils.CamelCaseSerialize(handshake));
            if (WsWebSocket!.State == WebSocketState.Open)
                WsWebSocket.SendAsync(new ArraySegment<Byte>(encodedMsg, 0, encodedMsg.Length), WebSocketMessageType.Text, true, CancellationToken.None);

            // Assuming this tool is not the main Tab page on the client, we delay sending all the data, to avoid making the network and client too busy an unresponsive
            if (!p_isThisActiveToolAtConnectionInit)
                Thread.Sleep(TimeSpan.FromMilliseconds(5000));

            // Portfolio data is big. Don't send it in handshake. Send it 5 seconds later (if it is not the active tool)
            PortfMgrSendFolders();
            PortfMgrSendPortfolios();
        });
    }

    private HandshakePortfMgr GetHandshakePortfMgr()
    {
        return new HandshakePortfMgr() { UserName = User.Username };
    }

    public bool OnReceiveWsAsync_PortfMgr(string msgCode, string msgObjStr)
    {
        switch (msgCode)
        {
            case "PortfMgr.RefreshFolders":
                Utils.Logger.Info($"OnReceiveWsAsync_PortfMgr(): RefreshFolders '{msgObjStr}'");
                PortfMgrSendFolders();
                return true;
            case "PortfMgr.CreateOrEditFolder": // msg: "id:-1,name:DayaTesting,prntFlrId:2,note:tesing"
                Utils.Logger.Info($"OnReceiveWsAsync_PortfMgr(): CreateOrEditFolder '{msgObjStr}'");
                PortfMgrCreateOrEditFolder(msgObjStr);
                PortfMgrSendFolders();
                return true;
            case "PortfMgr.CreateOrEditPortfolio": // msg: "id:-1,name:TestPrtf,prntFId:16,currency:USD,type:Simulation,access:Anyone,note:Testing"
                Utils.Logger.Info($"OnReceiveWsAsync_PortfMgr(): CreatePortfolio '{msgObjStr}'");
                PortfMgrCreateOrEditPortfolio(msgObjStr);
                PortfMgrSendPortfolios();
                return true;
            case "PortfMgr.DeletePortfolioItem": // msg: "id:5" // if id > 10,000 then it is a PortfolioID otherwise it is the FolderID
                Utils.Logger.Info($"OnReceiveWsAsync_PortfMgr(): DeletePortfolioItem '{msgObjStr}'");
                PortfMgrDeletePortfolioItem(msgObjStr);
                return true;
            case "PortfMgr.GetPortfolioRunResult": // msg: "id:5"
                Utils.Logger.Info($"OnReceiveWsAsync_PortfMgr(): GetPortfolioRunResult '{msgObjStr}'");
                PortfMgrSendPortfolioRunResults(msgObjStr);
                return true;
            default:
                return false;
        }
    }

    static string? GetRealParentFldId(int p_virtualParentFldId, out User? p_user, out int p_realParentFldId) // returns error string or empty if no error
    {
        p_user = null;
        p_realParentFldId = -1; // root of an existing user or the NoUser (if user = null)

        if (p_virtualParentFldId < -2) // if virtualParentFldId < -2, then the -1*virtualParentFldId represents an existing user and we want the root folder of the user. If the user is not found returns error.
        {
            int userId = -1 * p_virtualParentFldId; // try to find this userId among the users
            User[] users = MemDb.gMemDb.Users;
            for (int i = 0; i < users.Length; i++)
            {
                if (users[i].Id == userId)
                {
                    p_user = users[i];
                    return null; // returns p_user = found user; p_realParentFldId = -1 (Root) of the found user.
                }
            }
            return $"Error. No user found for userId {userId}";
        }
        else if (p_virtualParentFldId == UiUtils.gNoUserVirtPortfId) // == -2
        {
            return null; // returns p_user = null (the NoUser); p_realParentFldId = -1 (Root). This is fine. Admins should be able to create PortfolioItems in the Root folder of the NoUser
        }
        else if (p_virtualParentFldId == -1)
            return $"Error. virtualParentFldId == -1 is the Root of the UI FolderTree. We cannot create anything in that virtual folder as that is non-existent in the DB.";
        else // >=0, if virtualParentFldId is a proper folderID, just get its user and return that. Admin users can create a new PortfolioItem anywhere in the FolderTree. And the owner (user) of this new item will be the owner of the ParentFolder.
        {
            if (MemDb.gMemDb.PortfolioFolders.TryGetValue(p_virtualParentFldId, out PortfolioFolder? folder))
            {
                p_user = folder.User; // if the folder belongs to the NoUser, then folder.User == null, which is fine.
                p_realParentFldId = p_virtualParentFldId;
                return null; // returns p_user = found user; p_realParentFldId = -1 (Root) of the found user.
            }
            return $"Error. A positive virtualParentFldId {p_virtualParentFldId} was received, but that that Folder is not found in DB.";
        }
    }

    private void PortfMgrSendFolders() // Processing the portfolioFolders based on the visiblity rules
    {
        List<FolderJs> prtfFldrsToClient = UiUtils.GetPortfMgrFolders(User);
        byte[] encodedMsg = Encoding.UTF8.GetBytes("PortfMgr.Folders:" + Utils.CamelCaseSerialize(prtfFldrsToClient));
        if (WsWebSocket!.State == WebSocketState.Open)
            WsWebSocket.SendAsync(new ArraySegment<Byte>(encodedMsg, 0, encodedMsg.Length), WebSocketMessageType.Text, true, CancellationToken.None);
    }

    private void PortfMgrSendPortfolios()
    {
        List<PortfolioJs> prtfsToClient = UiUtils.GetPortfMgrPortfolios(User);
        byte[] encodedMsg = Encoding.UTF8.GetBytes("PortfMgr.Portfolios:" + Utils.CamelCaseSerialize(prtfsToClient));
        if (WsWebSocket!.State == WebSocketState.Open)
            WsWebSocket.SendAsync(new ArraySegment<Byte>(encodedMsg, 0, encodedMsg.Length), WebSocketMessageType.Text, true, CancellationToken.None);
    }

    public void PortfMgrCreateOrEditFolder(string p_msg) // msg: id:-1,name:DayaTesting,prntFlrId:2,note:testing
    {
        int idStartIdx = p_msg.IndexOf(":");
        int fldNameIdx = (idStartIdx == -1) ? -1 : p_msg.IndexOf(':', idStartIdx + 1);
        int prntFldrIdx = (fldNameIdx == -1) ? -1 : p_msg.IndexOf(":", fldNameIdx + 1);
        int userNoteIdx = prntFldrIdx == -1 ? -1 : p_msg.IndexOf(":", prntFldrIdx + 1);

        int id = Convert.ToInt32(p_msg.Substring(idStartIdx + 1, fldNameIdx - idStartIdx - ",name:".Length));
        string fldName = p_msg.Substring(fldNameIdx + 1, prntFldrIdx - fldNameIdx - ",prntFId:".Length);
        int virtualParentFldId = Convert.ToInt32(p_msg.Substring(prntFldrIdx + 1, userNoteIdx - prntFldrIdx - ",note:".Length));
        string userNote = p_msg[(userNoteIdx + 1)..];

        string? errMsg = GetRealParentFldId(virtualParentFldId, out User? user, out int realParentFldId);
        if (errMsg == null)
        {
            errMsg = MemDb.gMemDb.AddOrEditPortfolioFolder(id, user, fldName, realParentFldId, userNote, out PortfolioFolder? p_newItem);
            if (errMsg == String.Empty && p_newItem == null)
                errMsg = "Error. Folder change was not done.";
        }

        if (!String.IsNullOrEmpty(errMsg))
        {
            byte[] encodedMsg = Encoding.UTF8.GetBytes("PortfMgr.ErrorToUser:" + errMsg);
            if (WsWebSocket!.State == WebSocketState.Open)
                WsWebSocket.SendAsync(new ArraySegment<Byte>(encodedMsg, 0, encodedMsg.Length), WebSocketMessageType.Text, true, CancellationToken.None);
        }
    }

    public void PortfMgrCreateOrEditPortfolio(string p_msg) // "msg - id:-1,name:TestPrtf,prntFId:16,currency:USD,type:Simulation,algo:SqPctAllocation,algoP:assets=SVXY,VXX,VXZ,TQQQ,TLT,USO,UNG&weights=15,-5, 10, 25, 255,-27,-78&rebFreq=Daily,1d,access:Anyone,note:Testing"
    {
        int idStartIdx = p_msg.IndexOf(":");
        int pfNameIdx = (idStartIdx == -1) ? -1 : p_msg.IndexOf(':', idStartIdx + 1);
        int prntFldrIdx = (pfNameIdx == -1) ? -1 : p_msg.IndexOf(":", pfNameIdx + 1);
        int currencyIdx = prntFldrIdx == -1 ? -1 : p_msg.IndexOf(":", prntFldrIdx + 1);
        int prtfTypeIdx = currencyIdx == -1 ? -1 : p_msg.IndexOf(":", currencyIdx + 1);
        int algoIdx = prtfTypeIdx == -1 ? -1 : p_msg.IndexOf(":", prtfTypeIdx + 1);
        int algoParamIdx = algoIdx == -1 ? -1 : p_msg.IndexOf(":", algoIdx + 1);
        int userAccessIdx = algoParamIdx == -1 ? -1 : p_msg.IndexOf(":", algoParamIdx + 1);
        int userNoteIdx = userAccessIdx == -1 ? -1 : p_msg.IndexOf(":", userAccessIdx + 1);

        int id = Convert.ToInt32(p_msg.Substring(idStartIdx + 1, pfNameIdx - idStartIdx - ",name:".Length));
        string pfName = p_msg.Substring(pfNameIdx + 1, prntFldrIdx - pfNameIdx - ",prntFId:".Length);
        int virtualParentFldId = Convert.ToInt32(p_msg.Substring(prntFldrIdx + 1, currencyIdx - prntFldrIdx - ",currency:".Length));
        string currency = p_msg.Substring(currencyIdx + 1, prtfTypeIdx - currencyIdx - ",type:".Length);
        string prtfType = p_msg.Substring(prtfTypeIdx + 1, algoIdx - prtfTypeIdx - ",algo:".Length);
        string algorithm = p_msg.Substring(algoIdx + 1, algoParamIdx - algoIdx - ",algoP:".Length);
        string algorithmParam = p_msg.Substring(algoParamIdx + 1, userAccessIdx - algoParamIdx - ",access:".Length);
        string userAccess = p_msg.Substring(userAccessIdx + 1, userNoteIdx - userAccessIdx - ",note:".Length);
        string userNote = p_msg[(userNoteIdx + 1)..];

        string? errMsg = GetRealParentFldId(virtualParentFldId, out User? user, out int realParentFldId);
        if (errMsg == null)
        {
            errMsg = MemDb.gMemDb.AddOrEditPortfolio(id, user, pfName, realParentFldId, currency, prtfType, algorithm, algorithmParam, userAccess, userNote, out Portfolio? p_newItem);
            if (errMsg == String.Empty && p_newItem == null)
                errMsg = "Error. Portfolio change was not done.";
        }

        if (!String.IsNullOrEmpty(errMsg))
        {
            byte[] encodedMsg = Encoding.UTF8.GetBytes("PortfMgr.ErrorToUser:" + errMsg);
            if (WsWebSocket!.State == WebSocketState.Open)
                WsWebSocket.SendAsync(new ArraySegment<Byte>(encodedMsg, 0, encodedMsg.Length), WebSocketMessageType.Text, true, CancellationToken.None);
        }
    }

    private void PortfMgrDeletePortfolioItem(string p_msg) // "id:5"
    {
        int idStartInd = p_msg.IndexOf(":");
        if (idStartInd == -1)
            return;
        string idStr = p_msg[(idStartInd + 1)..];
        int id = Convert.ToInt32(idStr);
        bool isFolder = id < UiUtils.gPortfolioIdOffset;
        string errMsg;
        if (isFolder)
            errMsg = MemDb.gMemDb.DeletePortfolioFolder(id);
        else
            errMsg = MemDb.gMemDb.DeletePortfolio(id - UiUtils.gPortfolioIdOffset);

        if (!String.IsNullOrEmpty(errMsg))
        {
            byte[] encodedMsg = Encoding.UTF8.GetBytes("PortfMgr.ErrorToUser:" + errMsg);
            if (WsWebSocket!.State == WebSocketState.Open)
                WsWebSocket.SendAsync(new ArraySegment<Byte>(encodedMsg, 0, encodedMsg.Length), WebSocketMessageType.Text, true, CancellationToken.None);
        }

        if (isFolder)
            PortfMgrSendFolders();
        else
            PortfMgrSendPortfolios();
    }

    public void PortfMgrSendPortfolioRunResults(string p_msg)
    {
        // Step1: Processing the message to extract the Id
        int idStartInd = p_msg.IndexOf(":");
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
                    ChrtData = chartVal,
                    PrtfPoss = prtfPoss
                };

                // Step7: Sending the pfRunResults data to client
                if (pfRunResult != null)
                {
                    byte[] encodedMsg = Encoding.UTF8.GetBytes("PortfMgr.PrtfRunResult:" + Utils.CamelCaseSerialize(pfRunResult));
                    if (WsWebSocket!.State == WebSocketState.Open)
                        WsWebSocket.SendAsync(new ArraySegment<Byte>(encodedMsg, 0, encodedMsg.Length), WebSocketMessageType.Text, true, CancellationToken.None);
                }
            }
            _ = chartResolution; // To avoid the compiler Warning "Unnecessary assigment of a value" for unusued variables.
        }

        if (errMsg != null)
        {
            byte[] encodedMsg = Encoding.UTF8.GetBytes("PortfMgr.ErrorToUser:" + errMsg);
            if (WsWebSocket!.State == WebSocketState.Open)
                WsWebSocket.SendAsync(new ArraySegment<Byte>(encodedMsg, 0, encodedMsg.Length), WebSocketMessageType.Text, true, CancellationToken.None);
        }
    }
}