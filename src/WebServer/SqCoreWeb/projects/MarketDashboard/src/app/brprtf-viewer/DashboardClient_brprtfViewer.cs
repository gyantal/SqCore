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
using BrokerCommon;

namespace SqCoreWeb
{
    class HandshakeBrPrtfViewer
    {    //Initial params specific for the BrPrtfViewer tool
        public String SelectableNavs { get; set; } = string.Empty;
    }

    class BrPrtfViewerPos
    {
        public string SqTicker { get; set; } = string.Empty;
        public double Pos { get; set; }
        public double AvgCost { get; set; }

        // Double.NaN cannot be serialized. Send 0.0 for missing values.
        public double EstPrice { get; set; } = 0.0;  // MktValue can be calculated, 
        public double EstUndPrice { get; set; } = 0.0;   // In case of options DeliveryValue can be calculated

        public string AccId { get; set; } = string.Empty; // AccountId: "Cha", "DeB", "Gya" (in case of virtual combined portfolio)
    }

    class BrPrtfViewerPortfolio
    {
        public DateTime LastUpdate { get; set; } = DateTime.MinValue;
        public long NetLiquidation { get; set; } = long.MinValue;    // prefer whole numbers. Max int32 is 2B.
        public long GrossPositionValue { get; set; } = long.MinValue;
        public long TotalCashValue { get; set; } = long.MinValue;
        public long InitMarginReq { get; set; } = long.MinValue;
        public long MaintMarginReq { get; set; } = long.MinValue;
        public List<BrPrtfViewerPos> Poss { get; set; } = new List<BrPrtfViewerPos>();
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
        public void OnConnectedWsAsync_BrPrtfViewer(bool p_isThisActiveToolAtConnectionInit)
        {
            Task.Run(() => // running parallel on a ThreadPool thread
            {
                Thread.CurrentThread.IsBackground = true;  //  thread will be killed when all foreground threads have died, the thread will not keep the application alive.

                // Assuming this tool is not the main Tab page on the client, we delay sending all the data, to avoid making the network and client too busy an unresponsive
                if (!p_isThisActiveToolAtConnectionInit)
                    Thread.Sleep(TimeSpan.FromMilliseconds(5000));

                // BrPrtfViewer is not visible at the start for the user. We don't have to hurry to be responsive. 
                // With the handshake msg, we can take our time to collect All necessary data, and send it a bit (500ms) later.
                HandshakeBrPrtfViewer handshake = GetHandshakeBrPrtfViewer();
                byte[] encodedMsg = Encoding.UTF8.GetBytes("BrPrtfViewer.Handshake:" + Utils.CamelCaseSerialize(handshake));
                if (WsWebSocket!.State == WebSocketState.Open)
                    WsWebSocket.SendAsync(new ArraySegment<Byte>(encodedMsg, 0, encodedMsg.Length), WebSocketMessageType.Text, true, CancellationToken.None);

                var brPortf = GetBrPortfolio("N/DC");
                if (brPortf != null)
                {
                    encodedMsg = Encoding.UTF8.GetBytes("BrPrtfViewer.BrPortfolioPoss:"  + Utils.CamelCaseSerialize(brPortf));
                    if (WsWebSocket!.State == WebSocketState.Open)
                        WsWebSocket.SendAsync(new ArraySegment<Byte>(encodedMsg, 0, encodedMsg.Length), WebSocketMessageType.Text, true, CancellationToken.None);
                }

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
            
            
            return new HandshakeBrPrtfViewer() { SelectableNavs = selectableNavsCSV };
        }

        private BrPrtfViewerPortfolio? GetBrPortfolio(string p_sqTicker) // "N/GA.IM, N/DC, N/DC.IM, N/DC.IB"
        {
            // if it is aggregated portfolio (DC Main + DeBlanzac), then a virtual combination is needed
            if (!GatewayExtensions.NavSqSymbol2GatewayIds.TryGetValue(p_sqTicker, out List<GatewayId>? gatewayIds))
                return null;

            BrPrtfViewerPortfolio? result = null;
            foreach (GatewayId gwId in gatewayIds)
            {
                BrPortfolio? brPortfolio = MemDb.gMemDb.BrPortfolios.FirstOrDefault(r => r.GatewayId == gwId);
                if (brPortfolio == null)
                    return null;

                if (result == null)
                {
                    result = new BrPrtfViewerPortfolio()
                    {
                        LastUpdate = brPortfolio.LastUpdate,
                        GrossPositionValue = (long)brPortfolio.GrossPositionValue,
                        TotalCashValue = (long)brPortfolio.TotalCashValue,
                        InitMarginReq = (long)brPortfolio.InitMarginReq,
                        MaintMarginReq = (long)brPortfolio.MaintMarginReq,
                        Poss = brPortfolio.AccPoss.Select(r => new BrPrtfViewerPos()
                        {
                            SqTicker = r.Contract.ToString(),
                            Pos = r.Position,
                            AvgCost = r.AvgCost,
                            AccId = gwId.ToShortFriendlyString()
                        }).ToList()
                    };
                }
                else
                {
                    result.GrossPositionValue += (long)brPortfolio.GrossPositionValue;
                    result.TotalCashValue += (long)brPortfolio.TotalCashValue;
                    result.InitMarginReq += (long)brPortfolio.InitMarginReq;
                    result.MaintMarginReq += (long)brPortfolio.MaintMarginReq;
                    result.Poss.AddRange(brPortfolio.AccPoss.Select(r => new BrPrtfViewerPos()
                    {
                        SqTicker = r.Contract.ToString(),
                        Pos = r.Position,
                        AvgCost = r.AvgCost,
                        AccId = gwId.ToShortFriendlyString()
                    }));
                }
            }

            if (result != null)
            {
                Asset navAsset = MemDb.gMemDb.AssetsCache.AssetsBySqTicker[p_sqTicker];    // realtime NavAsset.LastValue is more up-to-date then from BrPortfolio (updated 1h in RTH only)
                result.NetLiquidation = (long)MemDb.gMemDb.GetLastRtValue(navAsset);
            }
            return result;
        }
    }
}