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
        static QuickfolioNewsDownloader g_newsDownloader = new QuickfolioNewsDownloader(); // only 1 global downloader for all clients

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
                    Thread.Sleep(DashboardClient.c_initialSleepIfNotActiveToolQn); // 10 sec is quite a long waiting, but we rarely use this tool.

            });
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