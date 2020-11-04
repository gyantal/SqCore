using Microsoft.AspNetCore.SignalR;
using System;
using System.Threading;
using System.Threading.Tasks;
using SqCommon;
using System.Linq;
using System.Collections.Generic;
using Microsoft.Extensions.Hosting;
using System.Text;
using System.Net;

namespace SqCoreWeb
{
    
    public partial class DashboardClient
    {
        const int m_newsReloadInterval = 10 * 60 * 1000; // 10 minutes in milliseconds 
        Timer? m_newsReloadTimer = null;    // separate Timer is needed for each client
        QuickfolioNewsDownloader m_newsDownloader = new QuickfolioNewsDownloader();

        void Ctor_QuickfNews()
        {
            m_newsReloadTimer = new Timer(NewsReloadTimerElapsed, null, m_newsReloadInterval, m_newsReloadInterval);
        }

        public void OnConnectedSignalRAsync_QuickfNews()
        {
            // don't do a long process here. Start big things in a separate thread. One way is in 'DashboardPushHub_mktHealth.cs'
            new Thread(() =>
            {
                Thread.CurrentThread.IsBackground = true;  //  thread will be killed when all foreground threads have died, the thread will not keep the application alive.
                m_newsDownloader.UpdateStockTickers();
                DashboardPushHubKestrelBckgrndSrv.HubContext?.Clients.Client(SignalRConnectionId).SendAsync("stockTickerList", m_newsDownloader.GetStockTickers());
                TriggerQuickfolioNewsDownloader(SignalRConnectionId);
            }).Start();
        }

        private void TriggerQuickfolioNewsDownloader(string p_connId)
        {
            // we can only send all news to the newly connected p_connId, and not All clients.
            // but that complicates things, because then what if we start to send all news to this fresh client, and 2 seconds later NewsReloadTimerElapsed triggers.
            // so, at the moment, whenever a new client connects, we resend all news to all old clients. If NewsReloadTimerElapsed() triggers during that, we send it twice.
            List<NewsItem> commonNews = m_newsDownloader.GetCommonNews();
            DashboardPushHubKestrelBckgrndSrv.HubContext?.Clients.All.SendAsync("quickfNewsCommonNewsUpdated", commonNews);
            m_newsDownloader.GetStockNews(DashboardPushHubKestrelBckgrndSrv.HubContext?.Clients.All);   // with 13 tickers, it can take 13 * 2 = 26seconds
        }

        public void OnDisconnectedSignalRAsync_QuickfNews(Exception exception)
        {
        }

        private void NewsReloadTimerElapsed(object? state)
        {
            if (DashboardClient.g_clients.Count > 0) 
            {
                TriggerQuickfolioNewsDownloader(String.Empty);
            }
        }

        // public void ReloadQuickfolio() {
        //     m_newsDownloader.UpdateStockTickers();
        //     DashboardPushHubKestrelBckgrndSrv.HubContext?.Clients.All.SendAsync("stockTickerList", m_newsDownloader.GetStockTickers());
        //     m_newsDownloader.GetStockNews(DashboardPushHubKestrelBckgrndSrv.HubContext?.Clients.All);
        // }

    }
}