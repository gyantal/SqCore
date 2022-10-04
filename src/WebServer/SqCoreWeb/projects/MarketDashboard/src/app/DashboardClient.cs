using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using FinTechCommon;
using Microsoft.AspNetCore.Http;
using SqCommon;

namespace SqCoreWeb;

public enum ActivePage { Unknown, MarketHealth, BrAccViewer, CatalystSniffer, QuickfolioNews, TooltipSandpit, Docs }

public partial class DashboardClient
{
    public string ClientIP { get; set; } = string.Empty;    // Remote Client IP for WebSocket
    public string UserEmail { get; set; } = string.Empty;
    public User User { get; set; }
    public DateTime ConnectionTime { get; set; } = DateTime.MinValue;
    public ActivePage ActivePage = ActivePage.Unknown; // knowing which Tool is active can be useful. We might not send data to tools which never becomes active

    public string ConnectionId // calculated field: a debugger friendly way of identifying the same websocket, in case WebSocket pointer is not good enough
    {
        get { return this.ClientIP + "@" + ConnectionTime.ToString("MM'-'dd'T'HH':'mm':'ss"); }
    }

    public WebSocket? WsWebSocket { get; set; } = null; // this pointer uniquely identifies the WebSocket as it is not released until websocket is dead
    public HttpContext? WsHttpContext { get; set; } = null;

    public static readonly Dictionary<string, ActivePage> c_urlParam2ActivePage = new()
    {
        { "mh", ActivePage.MarketHealth }, { "bav", ActivePage.BrAccViewer }, { "cs", ActivePage.CatalystSniffer }, { "qn", ActivePage.QuickfolioNews }
    };
    public static readonly HashSet<ActivePage> c_activePagesUsingRtPrices = new() { ActivePage.MarketHealth, ActivePage.BrAccViewer };

    internal static List<DashboardClient> g_clients = new(); // Multithread warning! Lockfree Read | Copy-Modify-Swap Write Pattern

    public static void PreInit()
    {
        MemDb.gMemDb.EvMemDbInitNoHistoryYet += new MemDb.MemDbEventHandler(OnEvMemDbInitNoHistoryYet);
        MemDb.gMemDb.EvFullMemDbDataReloaded += new MemDb.MemDbEventHandler(OnEvFullMemDbDataReloaded);
        MemDb.gMemDb.EvOnlyHistoricalDataReloaded += new MemDb.MemDbEventHandler(OnEvMemDbHistoricalDataReloaded);
    }

    static void OnEvMemDbInitNoHistoryYet()
    {
    }

    static void OnEvFullMemDbDataReloaded()
    {
        DashboardClient.g_clients.ForEach(client => // Notify all the connected clients.
        {
            // client.EvMemDbAssetDataReloaded_MktHealth();
            // client.EvMemDbAssetDataReloaded_BrAccViewer();
        });
    }

    static void OnEvMemDbHistoricalDataReloaded()
    {
        DashboardClient.g_clients.ForEach(client => // Notify all the connected clients.
        {
            client.EvMemDbHistoricalDataReloaded_MktHealth();
            // client.EvMemDbHistoricalDataReloaded_BrAccViewer();
        });
    }

    public static void ServerDiagnostic(StringBuilder p_sb)
    {
        p_sb.Append("<H2>Dashboard Clients</H2>");
        var g_clientsPtrCpy = DashboardClient.g_clients;    // Multithread warning! Lockfree Read | Copy-Modify-Swap Write Pattern
        p_sb.Append($"DashboardClient.g_clients (#{g_clientsPtrCpy.Count}): ");
        p_sb.AppendLongListByLine(g_clientsPtrCpy.Select(r => $"'{r.UserEmail}/{r.ConnectionId}'").ToArray(), ",", 3, "<br>");
        p_sb.Append($"<br>rtDashboardTimerRunning: {m_rtDashboardTimerRunning}<br>");
    }

    public DashboardClient(string p_clientIP, string p_userEmail, User p_user, DateTime p_connectionTime)
    {
        ClientIP = p_clientIP;
        UserEmail = p_userEmail;
        User = p_user;
        ConnectionTime = p_connectionTime;
    }

    // Return from this function very quickly. Do not call any Clients.Caller.SendAsync(), because client will not notice that connection is Connected, and therefore cannot send extra messages until we return here
    public void OnConnectedWsAsync_DshbrdClient()
    {
        // Note: as client is not fully initialized yet, 'this.client' is not yet in DashboardClient.g_clients list.
        ManualResetEvent waitHandleMkthConnect = new(false);
        ManualResetEvent waitHandleBrAccConnect = new(false);
        OnConnectedWsAsync_MktHealth(ActivePage == ActivePage.MarketHealth, User, waitHandleMkthConnect); // the code inside should run in a separate thread to return fast, so all Tools can work parallel
        OnConnectedWsAsync_BrAccViewer(ActivePage == ActivePage.BrAccViewer, User, waitHandleBrAccConnect); // the code inside should run in a separate thread to return fast, so all Tools can work parallel
        OnConnectedWsAsync_QckflNews(ActivePage == ActivePage.QuickfolioNews);
        // OnConnectedWsAsync_QckflNews2(ActivePage == ActivePage.QuickfolioNews);

        // have to wait until the tools initialize themselves to know what assets need RT prices
        bool sucessfullWait = ManualResetEvent.WaitAll(new WaitHandle[] { waitHandleMkthConnect }, 10 * 1000);
        if (!sucessfullWait)
            Utils.Logger.Warn("OnConnectedAsync():ManualResetEvent.WaitAll() timeout.");

        OnConnectedWsAsync_Rt();    // immediately send SPY realtime price. It can be used in 3+2 places: BrAccViewer:MarketBar, HistoricalChart, UserAssetList, MktHlth, CatalystSniffer (so, don't send it 5 times. Client will decide what to do with RT price)
    }

    public bool OnReceiveWsAsync_DshbrdClient(string msgCode, string msgObjStr)
    {
        switch (msgCode)
        {
            case "Dshbrd.IsDshbrdOpenManyTimes":
                Utils.Logger.Info($"OnReceiveWsAsync__DshbrdClient(): IsDashboardOpenManyTimes:{msgObjStr}");
                SendIsDashboardOpenManyTimes();
                return true;
            default:
                bool isHandled = OnReceiveWsAsync_MktHealth(msgCode, msgObjStr);
                if (!isHandled)
                    isHandled = OnReceiveWsAsync_BrAccViewer(msgCode, msgObjStr);
                if (!isHandled)
                    isHandled = OnReceiveWsAsync_QckflNews(msgCode, msgObjStr);
                return isHandled;
        }
    }

    public void SendIsDashboardOpenManyTimes() // If Dashboard is open in more than one tab or browser.
    {
        int nClientsWitSameUserAndIp = 0;
        var g_clientsPtrCpy = DashboardClient.g_clients;    // Multithread warning! Lockfree Read | Copy-Modify-Swap Write Pattern
        foreach (var client in g_clientsPtrCpy) // !Warning: Multithreaded Warning: This Reader code is fine. But potential problem if another thread removes clients from the List. The Modifier (Writer) thread should be careful, and Copy and Pointer-Swap when that Edit is taken.
        {
            if (client.UserEmail == UserEmail && client.ClientIP == ClientIP)
                nClientsWitSameUserAndIp++;
            if (nClientsWitSameUserAndIp > 1)
                break;
        }
        bool isDashboardOpenManyTimes = nClientsWitSameUserAndIp > 1;
        byte[] encodedMsg = Encoding.UTF8.GetBytes("Dshbrd.IsDshbrdOpenManyTimes:" + Utils.CamelCaseSerialize(isDashboardOpenManyTimes)); // => e.g. "Dshbrd.IsDshbrdOpenManyTimes:false"
        if (WsWebSocket!.State == WebSocketState.Open)
            WsWebSocket.SendAsync(new ArraySegment<Byte>(encodedMsg, 0, encodedMsg.Length), WebSocketMessageType.Text, true, CancellationToken.None); // takes 0.635ms
    }

    public static DashboardClient? FindClient(WebSocket? p_webSocket)
    {
        return DashboardClient.g_clients.Find(r => r.WsWebSocket == p_webSocket);
    }

    public static void AddToClients(DashboardClient p_client)
    {
        // !Warning: Multithreaded Warning: The Modifier (Writer) thread should be careful, and Copy and Pointer-Swap when Edit/Remove is done.
        lock (DashboardClient.g_clients) // lock assures that there are no 2 threads that is Adding at the same time on Cloned g_glients.
        {
            List<DashboardClient> clonedClients = new(DashboardClient.g_clients)
            {
                p_client // equivalent to clonedClients.Add(p_client);
            }; // adding new item to clone assures that no enumerating reader threads will throw exception.
            DashboardClient.g_clients = clonedClients;
        }
    }

    public static void RemoveFromClients(DashboardClient p_client)
    {
        // 'beforeunload' will be fired if the user submits a form, clicks a link, closes the window (or tab), or goes to a new page using the address bar, search box, or a bookmark.
        // server removes this client from DashboardClient.g_clients list

        // !Warning: Multithreaded Warning: The Modifier (Writer) thread should be careful, and Copy and Pointer-Swap when Edit/Remove is done.
        lock (DashboardClient.g_clients)
        {
            List<DashboardClient> clonedClients = new(DashboardClient.g_clients);
            clonedClients.Remove(p_client);
            DashboardClient.g_clients = clonedClients;
        }
    }
}