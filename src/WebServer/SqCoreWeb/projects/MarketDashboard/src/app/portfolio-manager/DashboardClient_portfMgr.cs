using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading;
using Fin.MemDb;
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
    [JsonPropertyName("cTime")]
    public string CreationTime { get; set; } = string.Empty;
    public string Note { get; set; } = string.Empty;
}

public partial class DashboardClient
{
    public Dictionary<int, int> userIdToVirtualPrtfId = new();
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

    public bool OnReceiveWsAsync_PortfMgr(string msgCode, string msgObjStr)
    {
        switch (msgCode)
        {
            case "PortfMgr.CreatePortfFldr": // msg: "DayaTest,vId:-17,prntFId;-1"
                Utils.Logger.Info($"OnReceiveWsAsync_PortfMgr(): CreatePortfFldr '{msgObjStr}'");
                (string pfNameP, int usrIdP, int parentFldIdP, string p_noteP) = ExtractCreatePrtfFldrParams(msgObjStr);
                CreatePortfolioFolder(pfNameP, usrIdP, parentFldIdP, p_noteP);
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

    static (string PfName, int UsrId, int ParentFldId, string P_note) ExtractCreatePrtfFldrParams(string p_msg)
    {
        int pfNameIdx = p_msg.IndexOf(',');
        if (pfNameIdx == -1)
            pfNameIdx = -1;
        string pfName = p_msg[..pfNameIdx];
        int usrIdStartIdx = p_msg.IndexOf(":", pfNameIdx);
        int prntFldrIdx = (usrIdStartIdx == -1) ? -1 : p_msg.IndexOf(";", usrIdStartIdx);
        if (prntFldrIdx == -1)
            prntFldrIdx = -1;
        int usrId = Convert.ToInt32(p_msg.Substring(usrIdStartIdx + 1, prntFldrIdx - usrIdStartIdx - 9));
        int parentFldId = Convert.ToInt32(p_msg[(prntFldrIdx + 1)..]);
        return (pfName, usrId, parentFldId, String.Empty);
    }

    public void CreatePortfolioFolder(string pfName, int usrId, int parentFldId, string p_note)
    {
        int prntFldId = -1;   // get it from the Client.
        // in case of parentFldId == -1 , the folder is created below the virtualUser folder (ex: i.e dkodirekka) need to build complete logic - under development (Daya)
        if(parentFldId == -1)
            prntFldId = usrId;
        // Whenever an Admin user on the UI selects a parent folder and creates a PortfolioFolder, it should inherit the User (owner) of that parent folder.
        // Note that the parent folder can be the Virtual Folder (User), but that is not allowed to be written to the MemDb, of course.
        // User ownerUser = User;  // That might not be true if an Admin user creates a prFolder in a virtual folder of another user.
        User user = User;

        if (User.IsAdmin)
        {
            // The below tackles the issue of Admin creating folder either in his folder or anyone else folder
            int pfUsrId = -1; // getting the portfolio userId
            foreach(var usr in userIdToVirtualPrtfId)
            {
                if (usr.Value == usrId)
                {
                    pfUsrId = usr.Key;
                    break;
                }
            }
            // based on the userId getting the user details
            User[] users = MemDb.gMemDb.Users;
            foreach (User usr in users)
            {
                if(usr.Id == pfUsrId)
                {
                    user = usr;
                    break;
                }
            }
        }
        else
            user = User;
        MemDb.gMemDb.AddNewPortfolioFolder(user, pfName, prntFldId, p_note);
    }

    private void PortfMgrSendPortfolioFldrs() // Processing the portfolioFolders based on the visiblity rules
    {
        // Visibility rules for PortfolioFolders:
        // - Normal users don't see other user's PortfolioFolders. They see a virtual folder with their username ('dkodirekka'),
        // a virtual folder 'Shared with me', 'Shared with Anyone', and a virtual folder called 'AllUsers'
        // - Admin users (developers) see all PortfolioFolders of all human users. Each human user (IsHuman) in a virtual folder with their username.
        // And the 'Shared with me', and 'AllUsers" virtual folders are there too.

        Dictionary<int, PortfolioFolder>.ValueCollection prtfFldrs = MemDb.gMemDb.PortfolioFolders.Values;
        List<PortfolioFolderJs> prtfFldrsToClient = new();

        // add the virtual folders to prtfFldrsToClient
        int virtuarFolderId = -2;
        User[] users = MemDb.gMemDb.Users;
        if (User.IsAdmin)
        {
            // assigning a virtual folder for every other users (with different negative Ids)
            foreach (User usr in users)
            {
                userIdToVirtualPrtfId.Add(usr.Id, virtuarFolderId);
                PortfolioFolderJs pfAdminUserJs = new() { Id = virtuarFolderId, Name = usr.Username };
                virtuarFolderId--;
                prtfFldrsToClient.Add(pfAdminUserJs);
            }
        }
        else
        {
            // send only his(User) virtual folder
            userIdToVirtualPrtfId.Add(User.Id, virtuarFolderId);
            PortfolioFolderJs pfCurUserJs = new() { Id = virtuarFolderId, Name = User.Username };
            virtuarFolderId--;
            prtfFldrsToClient.Add(pfCurUserJs);
        }

        PortfolioFolderJs pfSharedWithMeJs = new() { Id = virtuarFolderId--, Name = "Shared" }; // temporarily assigning the name
        prtfFldrsToClient.Add(pfSharedWithMeJs);

        PortfolioFolderJs pfAllUsersJs = new() { Id = virtuarFolderId--, Name = "AllUser" }; // temporarily assigning the name
        prtfFldrsToClient.Add(pfAllUsersJs);

        foreach(PortfolioFolder pf in prtfFldrs)
        {
            bool isSendToUser = User.IsAdmin || (pf.User == null) || (pf.User == User); // (pf.User == null) means UserId = -1, which means its intended for All users
            if (!isSendToUser)
                continue;

            int parentFolderId = pf.ParentFolderId;
            if (pf.ParentFolderId == -1)
            {
                if (pf.User == null)
                    parentFolderId = pfAllUsersJs.Id;
                else
                    parentFolderId = userIdToVirtualPrtfId[pf.User.Id];
            }

            PortfolioFolderJs pfJs = new() { Id = pf.Id, Name = pf.Name, ParentFolderId = parentFolderId, UserId = pf.User?.Id ?? -1, UserName = pf.User?.Username };
            prtfFldrsToClient.Add(pfJs);
        }
        byte[] encodedMsg = Encoding.UTF8.GetBytes("PortfMgr.PortfoliosFldrs:" + Utils.CamelCaseSerialize(prtfFldrsToClient));
        if (WsWebSocket!.State == WebSocketState.Open)
            WsWebSocket.SendAsync(new ArraySegment<Byte>(encodedMsg, 0, encodedMsg.Length), WebSocketMessageType.Text, true, CancellationToken.None);
    }
}