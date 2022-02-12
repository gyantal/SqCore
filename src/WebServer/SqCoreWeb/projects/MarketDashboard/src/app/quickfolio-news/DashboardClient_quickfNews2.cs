using System;
using System.Threading;
using SqCommon;
using System.Collections.Generic;
using System.Text;
using System.Net.WebSockets;
using System.Threading.Tasks;
using System.Linq;

namespace SqCoreWeb
{
    public partial class DashboardClient
    {
        static QuickfolioNewsDownloader g_newsDownloader = new QuickfolioNewsDownloader(); // only 1 global downloader for all clients
        public static TimeSpan c_initialSleepIfNotActiveToolQn2 = TimeSpan.FromMilliseconds(10 * 1000); // 10sec

        string[] m_stockTickers2 = { };

        void Ctor_QuickfNews2()
        {

        }

        public void OnConnectedWsAsync_QckflNews2(bool p_isThisActiveToolAtConnectionInit)
        {
            Utils.RunInNewThread(ignored => // running parallel on a ThreadPool thread, FireAndForget: QueueUserWorkItem [26microsec] is 25% faster than Task.Run [35microsec]
            {
                Thread.CurrentThread.IsBackground = true;  //  thread will be killed when all foreground threads have died, the thread will not keep the application alive.

                // Assuming this tool is not the main Tab page on the client, we delay sending all the data, to avoid making the network and client too busy an unresponsive
                if (!p_isThisActiveToolAtConnectionInit)
                    Thread.Sleep(DashboardClient.c_initialSleepIfNotActiveToolQn2); // 10 sec is quite a long waiting, but we rarely use this tool.
                
                if (m_stockTickers2.Length == 0)
                    m_stockTickers2 = GetStockTickers2() ?? new string[0];
                
                byte[] encodedMsg = Encoding.UTF8.GetBytes("QckfNews.Tickers:" + Utils.CamelCaseSerialize(new List<string> { "All assets" }.Union(m_stockTickers2).ToList()));
                if (WsWebSocket!.State == WebSocketState.Open)
                    WsWebSocket.SendAsync(new ArraySegment<Byte>(encodedMsg, 0, encodedMsg.Length), WebSocketMessageType.Text, true, CancellationToken.None);
                // TriggerQuickfolioNewsDownloader2();

                // first client connects, we start the timer immediately. helper: bool isQckflNewsTimerRunning = false;
                // after that... this timer should be run every 15min
                // in that timer function... we have do download CommonNews + Stock news.
                // in that timer, when the downloading of news are done => send it to All open Clients.
            });
        }

        int nExceptionsTriggerQuickfolioNewsDownloader2 = 0;
        private void TriggerQuickfolioNewsDownloader2()  // called at OnConnected and also periodically. Separete for each clients.
        {
            try
            {
                g_newsDownloader.GetStockNewsAndSendToClient(this);   // with 13 tickers, it can take 13 * 2 = 26seconds
                nExceptionsTriggerQuickfolioNewsDownloader2 = 0;
            }
            catch
            {
                // It is expected that DownloadStringWithRetryAsync() throws exceptions sometimes and download fails. Around midnight.
                // The request was canceled due to the configured HttpClient.Timeout of 100 seconds elapsing.
                // We only inform HealthMonitor if it happened 4*4=16 times. (about 4 hours)
                nExceptionsTriggerQuickfolioNewsDownloader2++;
            }
            if (nExceptionsTriggerQuickfolioNewsDownloader2 >= 16)
            {
                Utils.Logger.Error($"TriggerQuickfolioNewsDownloader() nExceptionsTriggerQuickfolioNewsDownloader: {nExceptionsTriggerQuickfolioNewsDownloader2}");
                string msg = $"SqCore.TriggerQuickfolioNewsDownloader() failed {nExceptionsTriggerQuickfolioNewsDownloader2}x. See log files.";
                HealthMonitorMessage.SendAsync(msg, HealthMonitorMessageID.SqCoreWebCsError).TurnAsyncToSyncTask();
                nExceptionsTriggerQuickfolioNewsDownloader2 = 0;
            }
        }
        public static string[]? GetStockTickers2()
        {
            string? valuesFromGSheetStr = "Error. Make sure GoogleApiKeyKey, GoogleApiKeyKey is in SQLab.WebServer.SQLab.NoGitHub.json !";
            if (!String.IsNullOrEmpty(Utils.Configuration["Google:GoogleApiKeyName"]) && !String.IsNullOrEmpty(Utils.Configuration["Google:GoogleApiKeyKey"]))
            {
                valuesFromGSheetStr = Utils.DownloadStringWithRetryAsync("https://sheets.googleapis.com/v4/spreadsheets/1c5ER22sXDEVzW3uKthclpArlZvYuZd6xUffXhs6rRsM/values/A1%3AA1?key=" + Utils.Configuration["Google:GoogleApiKeyKey"]).TurnAsyncToSyncTask();
                if (valuesFromGSheetStr == null)
                    valuesFromGSheetStr = "Error in DownloadStringWithRetry().";
            }
            if (!valuesFromGSheetStr.StartsWith("Error"))
                return ExtractTickers2(valuesFromGSheetStr);
            else
                return null;
        }

        private static string[]? ExtractTickers2(string p_spreadsheetString)
        {
            int pos = p_spreadsheetString.IndexOf(@"""values"":");
            if (pos < 0)
                return null;
            p_spreadsheetString = p_spreadsheetString.Substring(pos + 9); // cut off until the end of "values":
            int posStart = p_spreadsheetString.IndexOf(@"""");
            if (posStart < 0)
                return null;
            int posEnd = p_spreadsheetString.IndexOf(@"""", posStart + 1);
            if (posEnd < 0)
                return null;
            string cellValue = p_spreadsheetString.Substring(posStart + 1, posEnd - posStart - 1);
            return cellValue.Split(',').Select(x => x.Trim()).ToArray();
        }

        public bool OnReceiveWsAsync_QckflNews2(WebSocketReceiveResult? wsResult, string msgCode, string msgObjStr)
        {
            switch (msgCode)
            {
                case "QckflNews.ReloadQuickfolio":
                    Utils.Logger.Info("OnReceiveWsAsync_QckflNews(): QckflNews.ReloadQuickfolio");
                    // ReloadQuickfolioMsgArrived();
                    return true;
                default:
                    return false;
            }
        }

    }
}