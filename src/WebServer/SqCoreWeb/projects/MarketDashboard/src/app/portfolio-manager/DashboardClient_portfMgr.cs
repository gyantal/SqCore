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

class PortfolioItemJs
{
    public int Id { get; set; } = -1;
    [JsonPropertyName("n")]
    public string Name { get; set; } = string.Empty;
    [JsonPropertyName("p")]
    public int ParentFolderId { get; set; } = -1;
    [JsonPropertyName("cTime")]
    public string CreationTime { get; set; } = string.Empty;
    public string Note { get; set; } = string.Empty;
    [JsonPropertyName("ouId")]
    public int OwnerUserId { get; set; } = -1;
}

class FolderJs : PortfolioItemJs { }

class PortfolioJs : PortfolioItemJs
{
    [JsonPropertyName("sAcs")]
    public string SharedAccess { get; set; } = string.Empty;
    [JsonPropertyName("sUsr")]
    public List<User> SharedUsersWith { get; set; } = new();
    [JsonPropertyName("bCur")]
    public string BaseCurrency { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    [JsonPropertyName("algo")]
    public string Algorithm { get; set; } = string.Empty;
    [JsonPropertyName("algoP")]
    public string AlgorithmParam { get; set; } = string.Empty;
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
    public List<int> Values { get; set; } = new List<int>();
}

public partial class DashboardClient
{
    const int gPortfolioIdOffset = 10000;
    const int gNoUserVirtPortfId = -2;
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

    static int GetVirtualParentFldId(User? p_user, int p_realParentFldId)
    {
        int virtualParentFldId = p_realParentFldId;
        if (p_realParentFldId == -1) // if Portfolio doesn't have a parent folder, then it is in the Root (of either the NonUser or a concrete user)
        {
            if (p_user == null) // if the owner is the NoUser
                virtualParentFldId = gNoUserVirtPortfId;
            else
                virtualParentFldId = -1 * p_user.Id; // If there is a proper user, the Virtual FolderID is the -1 * UserId by our convention.
        }
        return virtualParentFldId;
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
        else if (p_virtualParentFldId == gNoUserVirtPortfId) // == -2
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
        // Visibility rules for PortfolioFolders:
        // - Normal users don't see other user's PortfolioFolders. They see a virtual folder with their username ('dkodirekka'),
        // a virtual folder 'Shared with me', 'Shared with Anyone', and a virtual folder called 'AllUsers'
        // - Admin users (developers) see all PortfolioFolders of all human users. Each human user (IsHuman) in a virtual folder with their username.
        // And the 'Shared with me', and 'AllUsers" virtual folders are there too.
        Dictionary<int, PortfolioFolder>.ValueCollection prtfFldrs = MemDb.gMemDb.PortfolioFolders.Values;
        Dictionary<int, Portfolio>.ValueCollection prtfs = MemDb.gMemDb.Portfolios.Values;
        // add the virtual folders to prtfFldrsToClient
        List<FolderJs> prtfFldrsToClient = new();
        Dictionary<int, User> virtUsrFldsToSend = new();

        if (User.IsAdmin)
        {
            foreach (PortfolioFolder pf in prtfFldrs) // iterate over all Folders to filter out those users who don't have any folders at all
            {
                if (pf.User != null)
                    virtUsrFldsToSend[pf.User.Id] = pf.User;
            }

            foreach (Portfolio pf in prtfs) // iterate over all Portfolios to filter out those users who don't have any portfolios at all
            {
                if (pf.User != null)
                    virtUsrFldsToSend[pf.User.Id] = pf.User;
            }
        }
        else
        {
            // send only his(User) virtual folder
            virtUsrFldsToSend[User.Id] = User;  // we send the user his main virtual folder even if he has no sub folders at all
        }

        foreach (var kvp in virtUsrFldsToSend)
        {
            User user = kvp.Value;
            int ownerUserId = user.Id;
            FolderJs pfAdminUserJs = new() { Id = -1 * user.Id, Name = user.Username, OwnerUserId = ownerUserId };
            prtfFldrsToClient.Add(pfAdminUserJs);
        }

        FolderJs pfSharedWithMeJs = new() { Id = 0, Name = "Shared", OwnerUserId = -1 };
        prtfFldrsToClient.Add(pfSharedWithMeJs);

        FolderJs pfAllUsersJs = new() { Id = gNoUserVirtPortfId, Name = "NoUser",  OwnerUserId = -1 };
        prtfFldrsToClient.Add(pfAllUsersJs);

        foreach (PortfolioFolder pf in prtfFldrs)
        {
            bool isSendToUser = User.IsAdmin || (pf.User == null) || (pf.User == User); // (pf.User == null) means UserId = -1, which means its intended for All users
            if (!isSendToUser)
                continue;

            int virtualParentFldId = GetVirtualParentFldId(pf.User, pf.ParentFolderId);
            int ownerUserId = pf.User?.Id ?? -1;

            FolderJs pfJs = new() { Id = pf.Id, Name = pf.Name, OwnerUserId = ownerUserId, ParentFolderId = virtualParentFldId };
            prtfFldrsToClient.Add(pfJs);
        }
        byte[] encodedMsg = Encoding.UTF8.GetBytes("PortfMgr.Folders:" + Utils.CamelCaseSerialize(prtfFldrsToClient));
        if (WsWebSocket!.State == WebSocketState.Open)
            WsWebSocket.SendAsync(new ArraySegment<Byte>(encodedMsg, 0, encodedMsg.Length), WebSocketMessageType.Text, true, CancellationToken.None);
    }

    private void PortfMgrSendPortfolios()
    {
        Dictionary<int, Portfolio>.ValueCollection prtfs = MemDb.gMemDb.Portfolios.Values;
        List<PortfolioJs> prtfToClient = new();

        foreach (Portfolio pf in prtfs)
        {
            bool isSendToUser = User.IsAdmin || (pf.User == null) || (pf.User == User); // (pf.User == null) means UserId = -1, which means its intended for All users
            if (!isSendToUser)
                continue;

            int virtualParentFldId = GetVirtualParentFldId(pf.User, pf.ParentFolderId);
            int ownerUserId = pf.User?.Id ?? -1;

            PortfolioJs pfJs = new() { Id = pf.Id + gPortfolioIdOffset, Name = pf.Name, OwnerUserId = ownerUserId, ParentFolderId = virtualParentFldId, BaseCurrency = pf.BaseCurrency.ToString(), SharedAccess = pf.SharedAccess.ToString(), SharedUsersWith = pf.SharedUsersWith, Type = pf.Type.ToString(), Algorithm = pf.Algorithm, AlgorithmParam = pf.AlgorithmParam };
            prtfToClient.Add(pfJs);
        }

        byte[] encodedMsg = Encoding.UTF8.GetBytes("PortfMgr.Portfolios:" + Utils.CamelCaseSerialize(prtfToClient));
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

    public void PortfMgrCreateOrEditPortfolio(string p_msg) // "msg - id:-1,name:TestPrtf,prntFId:16,currency:USD,type:Simulation,access:Anyone,note:Testing"
    {
        int idStartIdx = p_msg.IndexOf(":");
        int pfNameIdx = (idStartIdx == -1) ? -1 : p_msg.IndexOf(':', idStartIdx + 1);
        int prntFldrIdx = (pfNameIdx == -1) ? -1 : p_msg.IndexOf(":", pfNameIdx + 1);
        int currencyIdx = prntFldrIdx == -1 ? -1 : p_msg.IndexOf(":", prntFldrIdx + 1);
        int prtfTypeIdx = currencyIdx == -1 ? -1 : p_msg.IndexOf(":", currencyIdx + 1);
        int userAccessIdx = prtfTypeIdx == -1 ? -1 : p_msg.IndexOf(":", prtfTypeIdx + 1);
        int userNoteIdx = userAccessIdx == -1 ? -1 : p_msg.IndexOf(":", userAccessIdx + 1);

        int id = Convert.ToInt32(p_msg.Substring(idStartIdx + 1, pfNameIdx - idStartIdx - ",name:".Length));
        string pfName = p_msg.Substring(pfNameIdx + 1, prntFldrIdx - pfNameIdx - ",prntFId:".Length);
        int virtualParentFldId = Convert.ToInt32(p_msg.Substring(prntFldrIdx + 1, currencyIdx - prntFldrIdx - ",currency:".Length));
        string currency = p_msg.Substring(currencyIdx + 1, prtfTypeIdx - currencyIdx - ",type:".Length);
        string prtfType = p_msg.Substring(prtfTypeIdx + 1, userAccessIdx - prtfTypeIdx - ",access:".Length);
        string userAccess = p_msg.Substring(userAccessIdx + 1, userNoteIdx - userAccessIdx - ",note:".Length);
        string userNote = p_msg[(userNoteIdx + 1)..];

        string? errMsg = GetRealParentFldId(virtualParentFldId, out User? user, out int realParentFldId);
        if (errMsg == null)
        {
            errMsg = MemDb.gMemDb.AddOrEditPortfolio(id, user, pfName, realParentFldId, currency, prtfType, userAccess, userNote, out Portfolio? p_newItem);
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
        bool isFolder = id < gPortfolioIdOffset;
        string errMsg;
        if (isFolder)
            errMsg = MemDb.gMemDb.DeletePortfolioFolder(id);
        else
            errMsg = MemDb.gMemDb.DeletePortfolio(id - gPortfolioIdOffset);

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