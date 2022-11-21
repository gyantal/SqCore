using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using FinTechCommon;
using SqCommon;
using StackExchange.Redis;

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
                UserId = pf.User?.Id ?? -1
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
            case "PortfMgr.CreatePortf":
                Utils.Logger.Info($"OnReceiveWsAsync_PortfMgr(): CreatePortf '{msgObjStr}'");
                string pfName = msgObjStr;
                PortfMgrSendPortfoliosToServer(pfName);
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

    public void PortfMgrSendPortfoliosToServer(string portfolioname)
    {
        string cTime = Utils.ToSqDateTimeStr(DateTime.UtcNow); // adding the Creation Time
        Dictionary<int, PortfolioFolder>.ValueCollection prtfFldrs = MemDb.gMemDb.PortfolioFolders.Values; // getting the total portfolioFolders data to count the items
        int key = prtfFldrs.Count + 1; // setting the Key item, reuired to push to server(redisDb)
        User[] users = MemDb.gMemDb.Users;
        // Gettting the logged in userId from Users
        int id = 0;
        foreach (User usr in users)
        {
            if(usr.Email == UserEmail)
            {
                id = usr.Id;
                break;
            }
        }
        // taking the default value for parentFolder
        int parentFolder = -1;
        // setting the note data field as empty
        string note = string.Empty;
        PortfolioFolderInDb pf = new()
        {
            UserId = id,
            Name = portfolioname,
            ParentFolderId = parentFolder,
            CreationTime = cTime,
            Note = note
        };
        // serializing the pf object
        string pfStr = JsonSerializer.Serialize<PortfolioFolderInDb>(pf);
        // Creating the connection and sending the data to server
        // 1. Create a new connection. Don't use the MemDb main connection, because we might want to switch to a non-default DB, like DB-1. It is safer this way. Don't tinker with the MemDb main connection
        var redisConnString = OperatingSystem.IsWindows() ? Utils.Configuration["ConnectionStrings:RedisDefault"] : Utils.Configuration["ConnectionStrings:RedisLinuxLocalhost"];
        ConnectionMultiplexer newConn = ConnectionMultiplexer.Connect(redisConnString);
        // for testing purpose the DB value is provided as '1' once this method works we can push the data to current working db.
        var destDb = newConn.GetDatabase(1);
        destDb.HashSet("portfolioFolder", key, pfStr);
        // throw new NotImplementedException();
    }
}