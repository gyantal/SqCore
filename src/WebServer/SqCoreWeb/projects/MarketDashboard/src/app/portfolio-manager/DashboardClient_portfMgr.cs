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

// class PortfolioItemJs {
// }
// class PortfolioJs : PortfolioItemJs
// class PortfolioFolderJs: PortfolioItemJs

class PortfolioFolderJs
{
    public int Id { get; set; } = -1;
    [JsonPropertyName("n")]
    public string Name { get; set; } = string.Empty;
    [JsonPropertyName("p")]
    public int ParentFolderId { get; set; } = -1;
    [JsonPropertyName("cTime")]
    public string CreationTime { get; set; } = string.Empty;
    public string Note { get; set; } = string.Empty;
}

// class PortfolioJs : PortfolioFolderJs
// {
//     [JsonPropertyName("sAcs")]
//     public SharedAccess SharedAccess { get; set; } = SharedAccess.Unknown;
//     [JsonPropertyName("sUsr")]
//     public List<User> SharedUsersWith { get; set; } = new();
//     [JsonPropertyName("bCur")]
//     public string BaseCurrency { get; set; } = string.Empty;
//     public string Type { get; set; } = string.Empty;
// }

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
            // PortfMgrSendPortfolios();
        });
    }

    private HandshakePortfMgr GetHandshakePortfMgr()
    {
        return new HandshakePortfMgr() { UserName = User.Username };
    }

    // private void PortfMgrSendPortfolios()
    // {
    //     Dictionary<int, Portfolio>.ValueCollection prtfs = MemDb.gMemDb.Portfolios.Values;
    //     List<PortfolioJs> prtfToClient = new();
    //     foreach(Portfolio pf in prtfs)
    //     {
    //         PortfolioJs pfJs = new()
    //         {
    //             Id = pf.Id,
    //             Name = pf.Name,
    //             ParentFolderId = pf.ParentFolderId,
    //             SharedAccess = pf.SharedAccess,
    //             SharedUsersWith = pf.SharedUsersWith,
    //         };
    //         prtfToClient.Add(pfJs);
    //     }

    // // List<string> portfolios = User.IsAdmin ? new List<string>() { "PorfolioName1", "PortfolioName2" } : new List<string>() { "PorfolioName3", "PortfolioName4" };

    // byte[] encodedMsg = Encoding.UTF8.GetBytes("PortfMgr.Portfolios:" + Utils.CamelCaseSerialize(prtfToClient));
    //     if (WsWebSocket!.State == WebSocketState.Open)
    //         WsWebSocket.SendAsync(new ArraySegment<Byte>(encodedMsg, 0, encodedMsg.Length), WebSocketMessageType.Text, true, CancellationToken.None);
    // }

    public bool OnReceiveWsAsync_PortfMgr(string msgCode, string msgObjStr)
    {
        switch (msgCode)
        {
            case "PortfMgr.CreatePortfFldr": // msg: "DayaTest,prntFId:-1"
                Utils.Logger.Info($"OnReceiveWsAsync_PortfMgr(): CreatePortfFldr '{msgObjStr}'");
                CreatePortfolioFolder(msgObjStr);
                PortfMgrSendPortfolioFldrs();
                return true;
            case "PortfMgr.DeletePortfFldr": // msg: "TestNesting11,prntFId:5,chldrn:true"
                Utils.Logger.Info($"OnReceiveWsAsync_PortfMgr(): DeletePortfFldr '{msgObjStr}'");
                DeletePortfolioFolder(msgObjStr);
                PortfMgrSendPortfolioFldrs();
                return true;
            case "PortfMgr.RefreshPortfolioFldrs":
                Utils.Logger.Info($"OnReceiveWsAsync_PortfMgr(): RefreshPortfolioFldrs '{msgObjStr}'");
                PortfMgrSendPortfolioFldrs();
                return true;
            default:
                return false;
        }
    }

    public void CreatePortfolioFolder(string p_msg)
    {
        int pfNameIdx = p_msg.IndexOf(',');
        int prntFldrIdx = (pfNameIdx == -1) ? -1 : p_msg.IndexOf(":", pfNameIdx);
        if (prntFldrIdx == -1)
            prntFldrIdx = -1;
        string pfName = p_msg[..pfNameIdx];
        int parentFldId = Convert.ToInt32(p_msg[(prntFldrIdx + 1)..]);

        string p_note = string.Empty; // if there is some note mentioned by client we need to take that not the empty
        Dictionary<int, PortfolioFolder>.ValueCollection prtfFldrs = MemDb.gMemDb.PortfolioFolders.Values;
        User? user = User;
        int prntFldIdToSend = -1;
        foreach (PortfolioFolder pf in prtfFldrs)
        {
            if (parentFldId >= -2)
            {
                if (parentFldId == 0) // not allowed. Nobody can create folders in the virtual “Shared” folder. That is a flat virtual folder. No folder hierarchy there (like GoogleDrive)
                    return; // throw new Exception("Nobody can create folders in virtual Shared folder"); // can we send an exception here - Daya
                else if (parentFldId >= 1) // it is a proper folderID, Create the new Folder under that
                {
                    prntFldIdToSend = parentFldId;
                    if (pf.Id == prntFldIdToSend)
                    {
                        user = pf.User;
                        break;
                    }
                }
                else if (parentFldId == -2) // parentFldId == -2  Create the new Folder with “"User":-1,"ParentFolder":-2,”
                {
                    prntFldIdToSend = parentFldId;
                    user = null;
                    break;
                }
            }
            else // parentFldId < -2 is a virtual UserRoot folder. create the new Folder with (User: -1 * thisUserId, ParentFolder = -1)
            {
                if (pf.User?.Id == -1 * parentFldId)
                {
                    user = pf.User;
                    // prntFldIdToSend = -1;
                    break;
                }
            }
        }
        MemDb.gMemDb.AddNewPortfolioFolder(user, pfName, prntFldIdToSend, p_note);
    }

    private void DeletePortfolioFolder(string p_msg)
    {
        int prntFldrIdx = p_msg.IndexOf(":");
        if (prntFldrIdx == -1)
            prntFldrIdx = -1;
        int fldId = Convert.ToInt32(p_msg[(prntFldrIdx + 1)..]);
        string errMsg = MemDb.gMemDb.DeletePortfolioFolder(fldId);
        if (!String.IsNullOrEmpty(errMsg))
        {
            byte[] encodedMsg = Encoding.UTF8.GetBytes("PortfMgr.ErrorToUser:" + errMsg);
            if (WsWebSocket!.State == WebSocketState.Open)
                WsWebSocket.SendAsync(new ArraySegment<Byte>(encodedMsg, 0, encodedMsg.Length), WebSocketMessageType.Text, true, CancellationToken.None);
        }
    }

    private void PortfMgrSendPortfolioFldrs() // Processing the portfolioFolders based on the visiblity rules
    {
        // Visibility rules for PortfolioFolders:
        // - Normal users don't see other user's PortfolioFolders. They see a virtual folder with their username ('dkodirekka'),
        // a virtual folder 'Shared with me', 'Shared with Anyone', and a virtual folder called 'AllUsers'
        // - Admin users (developers) see all PortfolioFolders of all human users. Each human user (IsHuman) in a virtual folder with their username.
        // And the 'Shared with me', and 'AllUsers" virtual folders are there too.
        Dictionary<int, PortfolioFolder>.ValueCollection prtfFldrs = MemDb.gMemDb.PortfolioFolders.Values;
        // add the virtual folders to prtfFldrsToClient
        List<PortfolioFolderJs> prtfFldrsToClient = new();
        Dictionary<int, User> virtUsrFldsToSend = new();

        if (User.IsAdmin)
        {
            foreach (PortfolioFolder pf in prtfFldrs) // iterate over all Folders to filter out those users who don't have any folders at all
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
            PortfolioFolderJs pfAdminUserJs = new() { Id = -1 * user.Id, Name = user.Username };
            prtfFldrsToClient.Add(pfAdminUserJs);
        }

        PortfolioFolderJs pfSharedWithMeJs = new() { Id = 0, Name = "Shared" };
        prtfFldrsToClient.Add(pfSharedWithMeJs);

        const int noUserVirtPortfId = -2;
        PortfolioFolderJs pfAllUsersJs = new() { Id = noUserVirtPortfId, Name = "NoUser" };
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
                    parentFolderId = noUserVirtPortfId;
                else
                    parentFolderId = -1 * pf.User.Id;
            }

            PortfolioFolderJs pfJs = new() { Id = pf.Id, Name = pf.Name, ParentFolderId = parentFolderId };
            prtfFldrsToClient.Add(pfJs);
        }
        byte[] encodedMsg = Encoding.UTF8.GetBytes("PortfMgr.PortfoliosFldrs:" + Utils.CamelCaseSerialize(prtfFldrsToClient));
        if (WsWebSocket!.State == WebSocketState.Open)
            WsWebSocket.SendAsync(new ArraySegment<Byte>(encodedMsg, 0, encodedMsg.Length), WebSocketMessageType.Text, true, CancellationToken.None);
    }
}