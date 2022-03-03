using System;
using System.Threading;
using SqCommon;
using System.Collections.Generic;
using System.Text;
using System.Net.WebSockets;
using System.Threading.Tasks;

namespace SqCoreWeb
{
    
    public partial class DashboardClient
    {
        // const int m_newsReloadInterval = 15 * 60 * 1000; // 15 minutes in milliseconds
        // Timer? m_newsReloadTimer = null;    // separate Timer is needed for each client  (that is a waste of resources, but fine temporarily)
        readonly QuickfolioNewsDownloader m_newsDownloader = new(); // separate downloader for each client.

        public static readonly TimeSpan c_initialSleepIfNotActiveToolQn = TimeSpan.FromMilliseconds(10 * 1000); // 10sec

        void Ctor_QuickfNews()
        {
            // m_newsReloadTimer = new Timer(NewsReloadTimerElapsed, null, m_newsReloadInterval, m_newsReloadInterval);
        }

        public void OnConnectedWsAsync_QckflNews(bool p_isThisActiveToolAtConnectionInit)
        {
            Utils.RunInNewThread(ignored => // running parallel on a ThreadPool thread, FireAndForget: QueueUserWorkItem [26microsec] is 25% faster than Task.Run [35microsec]
            {
                Thread.CurrentThread.IsBackground = true;  //  thread will be killed when all foreground threads have died, the thread will not keep the application alive.

                // Assuming this tool is not the main Tab page on the client, we delay sending all the data, to avoid making the network and client too busy an unresponsive
                if (!p_isThisActiveToolAtConnectionInit)
                    Thread.Sleep(DashboardClient.c_initialSleepIfNotActiveToolQn); // 10 sec is quite a long waiting, but we rarely use this tool.

                m_newsDownloader.UpdateStockTickers();
                // byte[] encodedMsg = Encoding.UTF8.GetBytes("QckfNews.Tickers:" + Utils.CamelCaseSerialize(m_newsDownloader.GetStockTickers()));
                // if (WsWebSocket!.State == WebSocketState.Open)
                //     WsWebSocket.SendAsync(new ArraySegment<Byte>(encodedMsg, 0, encodedMsg.Length), WebSocketMessageType.Text, true, CancellationToken.None);

                TriggerQuickfolioNewsDownloader();
            });
        }

        public bool OnReceiveWsAsync_QckflNews(string msgCode, string msgObjStr)
        {
            switch (msgCode)
            {
                case "ReloadQuickfolio":
                    Utils.Logger.Info($"OnReceiveWsAsync_QckflNews(): ReloadQuickfolio: {msgObjStr}");
                    ReloadQuickfolioMsgArrived();
                    return true;
                default:
                    return false;
            }
        }

        int nExceptionsTriggerQuickfolioNewsDownloader = 0;
        private void TriggerQuickfolioNewsDownloader()  // called at OnConnected and also periodically. Separete for each clients.
        {
            try
            {
                // we only send all news to the newly connected p_connId, and not All clients.
                // each client has its own private m_newsDownloader, and own timer.
                // this is a waste of resources, because we download things separately for each clients
                // TODO Daya:
                // in the future, we might store news in memory and feed the users from that, and we don't download news multiple times.
                // m_newsDownloader.GetCommonNewsAndSendToClient(DashboardClient.g_clients);
                // m_newsDownloader.GetCommonNewsAndSendToClient(this);

                // m_newsDownloader.GetStockNewsAndSendToClient(this);   // with 13 tickers, it can take 13 * 2 = 26seconds
                nExceptionsTriggerQuickfolioNewsDownloader = 0;
            }
            catch
            {
                // It is expected that DownloadStringWithRetryAsync() throws exceptions sometimes and download fails. Around midnight.
                // The request was canceled due to the configured HttpClient.Timeout of 100 seconds elapsing.
                // We only inform HealthMonitor if it happened 4*4=16 times. (about 4 hours)
                nExceptionsTriggerQuickfolioNewsDownloader++;
            }
            if (nExceptionsTriggerQuickfolioNewsDownloader >= 16)
            {
                Utils.Logger.Error($"TriggerQuickfolioNewsDownloader() nExceptionsTriggerQuickfolioNewsDownloader: {nExceptionsTriggerQuickfolioNewsDownloader}");
                string msg = $"SqCore.TriggerQuickfolioNewsDownloader() failed {nExceptionsTriggerQuickfolioNewsDownloader}x. See log files.";
                HealthMonitorMessage.SendAsync(msg, HealthMonitorMessageID.SqCoreWebCsError).TurnAsyncToSyncTask();
                nExceptionsTriggerQuickfolioNewsDownloader = 0;
            }
        }

        // private void NewsReloadTimerElapsed(object? state)
        // {
        //     if (DashboardClient.g_clients.Count > 0) 
        //     {
        //         TriggerQuickfolioNewsDownloader();
        //     }
        // }

        public void ReloadQuickfolioMsgArrived() {
            // m_newsDownloader.UpdateStockTickers();
            // DashboardPushHubKestrelBckgrndSrv.HubContext?.Clients.All.SendAsync("QckfNews.Tickers", m_newsDownloader.GetStockTickers());
            // m_newsDownloader.GetStockNews(DashboardPushHubKestrelBckgrndSrv.HubContext?.Clients.All);
        }

    }
}