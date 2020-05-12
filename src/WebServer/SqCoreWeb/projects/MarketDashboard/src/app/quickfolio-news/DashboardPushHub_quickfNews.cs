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
    
    public partial class DashboardPushHub : Hub
    {
        const int m_newsReloadInterval = 10 * 60 * 1000; // 10 minutes in milliseconds 
        Timer m_newsReloadTimer;
        QuickfolioNewsDownloader m_newsDownloader = new QuickfolioNewsDownloader();

        public DashboardPushHub()
        {
            m_newsReloadTimer = new Timer(NewsReloadTimerElapsed, null, m_newsReloadInterval, m_newsReloadInterval);
        }
        public void OnConnectedAsync_QuickfNews()
        {
            // don't do a long process here. Start big things in a separate thread. One way is in 'DashboardPushHub_mktHealth.cs'
            string connId = this.Context?.ConnectionId ?? String.Empty;
            new Thread(() =>
            {
                Thread.CurrentThread.IsBackground = true;  //  thread will be killed when all foreground threads have died, the thread will not keep the application alive.
                DashboardPushHubKestrelBckgrndSrv.HubContext?.Clients.Client(connId).SendAsync("stockTickerList", m_newsDownloader.GetStockTickers());
                TriggerQuickfolioNewsDownloader(connId);
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

        public void OnDisconnectedAsync_QuickfNews(Exception exception)
        {
        }
        private void NewsReloadTimerElapsed(object? state)
        {
            if (DashboardPushHub.g_clients.Count > 0) 
            {
                TriggerQuickfolioNewsDownloader(String.Empty);
            }
        }

        public void ReloadQuickfolio() {
            m_newsDownloader.UpdateStockTickers();
            DashboardPushHubKestrelBckgrndSrv.HubContext?.Clients.All.SendAsync("stockTickerList", m_newsDownloader.GetStockTickers());
            m_newsDownloader.GetStockNews(DashboardPushHubKestrelBckgrndSrv.HubContext?.Clients.All);
        }

    }
}