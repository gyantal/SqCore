using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading;
using FinTechCommon;
using SqCommon;

namespace SqCoreWeb;

class HandshakePortfMgr // Initial params: keept it small
{
    public string UserName { get; set; } = string.Empty;
}

class PortfolioFolderJs
{
    public int Id { get; set; } = -1;
    [JsonPropertyName("n")]
    public string Name { get; set; } = string.Empty;
    [JsonPropertyName("u")]
    public int UserId { get; set; } = -1;
    [JsonPropertyName("p")]
    public int ParentFolderId { get; set; } = -1;
    [JsonPropertyName("uName")]
    public string? UserName { get; set; } = string.Empty;
    public bool? IsHuman { get; set; } = false; // AllUser, SqBacktester... users are not humans.
    public bool? IsAdmin { get; set; } = false;
    [JsonPropertyName("cTime")]
    public string CreationTime { get; set; } = string.Empty;
    public string Note { get; set; } = string.Empty;
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
            PortfMgrSendPortfolioFldrs();
            PortfMgrSendPortfolios();
            // ProcessPrtfFldrsBasedOnVisibilty();
        });
    }

    private HandshakePortfMgr GetHandshakePortfMgr()
    {
        return new HandshakePortfMgr() { UserName = User.Username };
    }

    private void PortfMgrSendPortfolios()
    {
        Dictionary<int, Portfolio>.ValueCollection prtf = MemDb.gMemDb.Portfolios.Values;
        List<PortfolioFolderJs> portfolios = new(prtf.Count);
        foreach(Portfolio pf in prtf)
        {
            PortfolioFolderJs pfJs = new()
            {
                Id = pf.Id,
                Name = pf.Name,
                ParentFolderId = pf.ParentFolderId,
                UserId = pf.User?.Id ?? -1
            };
            portfolios.Add(pfJs);
        }

        // List<string> portfolios = User.IsAdmin ? new List<string>() { "PorfolioName1", "PortfolioName2" } : new List<string>() { "PorfolioName3", "PortfolioName4" };

        byte[] encodedMsg = Encoding.UTF8.GetBytes("PortfMgr.Portfolios:" + Utils.CamelCaseSerialize(portfolios));
        if (WsWebSocket!.State == WebSocketState.Open)
            WsWebSocket.SendAsync(new ArraySegment<Byte>(encodedMsg, 0, encodedMsg.Length), WebSocketMessageType.Text, true, CancellationToken.None);
    }
    private void PortfMgrSendPortfolioFldrs()
    {
        Dictionary<int, PortfolioFolder>.ValueCollection prtfFldrs = MemDb.gMemDb.PortfolioFolders.Values;
        List<PortfolioFolderJs> portfolioFldrs = new(prtfFldrs.Count);
        foreach(PortfolioFolder pf in prtfFldrs)
        {
            PortfolioFolderJs pfJs = new()
            {
                Id = pf.Id,
                Name = pf.Name,
                ParentFolderId = pf.ParentFolderId,
                UserId = pf.User?.Id ?? -1,
                UserName = pf.User?.Username,
                IsHuman = pf.User?.IsHuman,
                IsAdmin = pf.User?.IsAdmin
            };
            portfolioFldrs.Add(pfJs);
        }

        byte[] encodedMsg = Encoding.UTF8.GetBytes("PortfMgr.PortfoliosFldrs:" + Utils.CamelCaseSerialize(portfolioFldrs));
        if (WsWebSocket!.State == WebSocketState.Open)
            WsWebSocket.SendAsync(new ArraySegment<Byte>(encodedMsg, 0, encodedMsg.Length), WebSocketMessageType.Text, true, CancellationToken.None);
    }

    public bool OnReceiveWsAsync_PortfMgr(string msgCode, string msgObjStr)
    {
        switch (msgCode)
        {
            case "PortfMgr.CreatePortfFldr":
                Utils.Logger.Info($"OnReceiveWsAsync_PortfMgr(): CreatePortfFldr '{msgObjStr}'");
                string pfName = msgObjStr;
                CreatePortfolioFolder(pfName, String.Empty);
                return true;
            case "PortfMgr.DeletePortf":
                Utils.Logger.Info($"OnReceiveWsAsync_PortfMgr(): DeletePortf '{msgObjStr}'");
                return true;
            case "PortfMgr.RefreshPortfolios":
                Utils.Logger.Info($"OnReceiveWsAsync_PortfMgr(): RefreshPortfolios '{msgObjStr}'");
                PortfMgrSendPortfolioFldrs();
                return true;
            default:
                return false;
        }
    }

    public void CreatePortfolioFolder(string pfName, string p_note)
    {
        User ownerUser = User;  // That might not be true if an Admin user creates a prFolder in a virtual folder of another user.
        int parentFldId = -1;   // get it from the Client.
        string creationTime = Utils.ToSqDateTimeStr(DateTime.UtcNow); // DateTime.Now.ToString() => "CTime":"2022-10-13T20:00:00"
        MemDb.gMemDb.AddPortfolioFolder(ownerUser, pfName, parentFldId, creationTime, p_note);
    }

    public void ProcessPrtfFldrsBasedOnVisibilty() // naming it temporaryly for understanding purpose, will change once the function is finalized
    {
        // Visibility rules for PortfolioFolders:
        // - Normal users don't see other user's PortfolioFolders. They see a virtual folder with their username ('dkodirekka'),
        // a virtual folder 'Shared with me', 'Shared with Anyone', and a virtual folder called 'AllUsers'
        // - Admin users (developers) see all PortfolioFolders of all human users. Each human user (IsHuman) in a virtual folder with their username.
        // And the 'Shared with me', and 'AllUsers" virtual folders are there too.

        Dictionary<int, PortfolioFolder>.ValueCollection prtfFldrs = MemDb.gMemDb.PortfolioFolders.Values;
        List<PortfolioFolderJs> prtfFldrsToClient = new();
        Dictionary<int, int> userIdToVirtualPrtfId = new();

        // add the virtual folders to prtfFldrsToClient
        int virtuarFolderId = -2;
        if (User.IsAdmin)
        {
            // send a virtual folder for every other users (with different negative Ids)
        }
        else
        {
            // send only his virtual folder
            // PortfolioFolderJs pfJs = new()
            // {
            //     Id = -2,
            // };
            // prtfFldrsToClient.Add(pfJs);
        }

        PortfolioFolderJs pfSharedWithMeJs = new()
        {
            Id = virtuarFolderId--
        };
        prtfFldrsToClient.Add(pfSharedWithMeJs);

        PortfolioFolderJs pfAllUsersJs = new()
        {
            Id = virtuarFolderId--
        };
        prtfFldrsToClient.Add(pfAllUsersJs);

        foreach(PortfolioFolder pf in prtfFldrs)
        {
            bool isSendToUser = User.IsAdmin || (pf.User == User);
            if (!isSendToUser)
                break;

            int parentFolderId = pf.ParentFolderId;
            if (pf.ParentFolderId == -1)
            {
                if (pf.User == null)
                    parentFolderId = pfAllUsersJs.Id;
                else
                    parentFolderId = userIdToVirtualPrtfId[pf.User.Id];
            }

            PortfolioFolderJs pfJs = new()
            {
                Id = pf.Id,
                Name = pf.Name,
                ParentFolderId = parentFolderId,
                UserId = pf.User?.Id ?? -1
            };
            prtfFldrsToClient.Add(pfJs);
        }
        byte[] encodedMsg = Encoding.UTF8.GetBytes("PortfMgr.PortfoliosFldrs:" + Utils.CamelCaseSerialize(prtfFldrsToClient));
        if (WsWebSocket!.State == WebSocketState.Open)
            WsWebSocket.SendAsync(new ArraySegment<Byte>(encodedMsg, 0, encodedMsg.Length), WebSocketMessageType.Text, true, CancellationToken.None);
    }
}