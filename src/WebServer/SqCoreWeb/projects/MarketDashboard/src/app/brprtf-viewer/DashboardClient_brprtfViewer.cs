using System;
using System.Threading;
using SqCommon;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;
using FinTechCommon;
using System.Text.Json.Serialization;
using System.Net.WebSockets;
using Microsoft.Extensions.Primitives;
using System.Threading.Tasks;

namespace SqCoreWeb
{
    class HandshakeBrPrtfViewer
    {    //Initial params specific for the BrPrtfViewer tool
        public String SelectableNavs { get; set; } = string.Empty;
        public String NavRtDcMain { get; set; } = string.Empty;
    }

    public partial class DashboardClient
    {

        void Ctor_BrPrtfViewer()
        {
            // InitAssetData();
        }

        void EvMemDbAssetDataReloaded_BrPrtfViewer()
        {
            //InitAssetData();
        }

        // Return from this function very quickly. Do not call any Clients.Caller.SendAsync(), because client will not notice that connection is Connected, and therefore cannot send extra messages until we return here
        public void OnConnectedWsAsync_BrPrtfViewer()
        {
            Task.Run(() => // running parallel on a ThreadPool thread
            {
                Thread.CurrentThread.IsBackground = true;  //  thread will be killed when all foreground threads have died, the thread will not keep the application alive.

                // Assuming BrPrtfViewer is not the main Tab page on the client, we delay sending all the data, to avoid making the network and client too busy an unresponsive
                Thread.Sleep(TimeSpan.FromMilliseconds(200));   // 

                // BrPrtfViewer is not visible at the start for the user. We don't have to hurry to be responsive. 
                // With the handshake msg, we can take our time to collect All necessary data, and send it a bit (500ms) later.
                HandshakeBrPrtfViewer handshake = GetHandshakeBrPrtfViewer();
                byte[] encodedMsg = Encoding.UTF8.GetBytes("BrPrtfViewer.Handshake:" + Utils.CamelCaseSerialize(handshake));
                if (WsWebSocket!.State == WebSocketState.Open)
                    WsWebSocket.SendAsync(new ArraySegment<Byte>(encodedMsg, 0, encodedMsg.Length), WebSocketMessageType.Text, true, CancellationToken.None);

                // for both the first and the second client, we get RT prices from MemDb immediately and send it back to this Client only.

                // // 1. Send the Historical data first. SendAsync() is non-blocking. GetLastRtPrice() can be blocking
                // SendHistoricalWs();
                // // 2. Send RT price later, because GetLastRtPrice() might block the thread, if it is the first client.
                // SendRealtimeWs();

                // lock (m_rtMktSummaryTimerLock)
                // {
                //     if (!m_rtMktSummaryTimerRunning)
                //     {
                //         Utils.Logger.Info("OnConnectedAsync_MktHealth(). Starting m_rtMktSummaryTimer.");
                //         m_rtMktSummaryTimerRunning = true;
                //         m_rtMktSummaryTimer.Change(TimeSpan.FromMilliseconds(m_rtMktSummaryTimerFrequencyMs), TimeSpan.FromMilliseconds(-1.0));    // runs only once. To avoid that it runs parallel, if first one doesn't finish
                //     }
                // }
            });
        }

        private HandshakeBrPrtfViewer GetHandshakeBrPrtfViewer()
        {
            //string selectableNavs = "GA.IM.NAV, DC.NAV, DC.IM.NAV, DC.IB.NAV";
            List<BrokerNav> selectableNavs = GetSelectableNavsOrdered();
            string selectableNavsCSV = String.Join(',', selectableNavs.Select(r => r.Symbol + ".NAV")); // on the UI, postfix ".NAV" looks better than prefix "N/"
            
            Asset dcMainNavAsset = MemDb.gMemDb.AssetsCache.AssetsBySqTicker["N/DC.IM"];
            return new HandshakeBrPrtfViewer() { SelectableNavs = selectableNavsCSV, NavRtDcMain = dcMainNavAsset.LastValue.ToString() };
        }
    }
}