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
    class HandshakeBrAccViewer
    {    //Initial params specific for the BrAccViewer tool
        public String SelectableBrAccs { get; set; } = string.Empty;
    }

    class BrAccViewerPos
    {
        public string SqTicker { get; set; } = string.Empty;
        public double Pos { get; set; }
        public double AvgCost { get; set; }

        // Double.NaN cannot be serialized. Send 0.0 for missing values.
        public double EstPrice { get; set; } = 0.0;  // MktValue can be calculated, 
        public double EstUndPrice { get; set; } = 0.0;   // In case of options DeliveryValue can be calculated

        public string AccId { get; set; } = string.Empty; // AccountId: "Cha", "DeB", "Gya" (in case of virtual combined portfolio)
    }

    class BrAccViewerAccount
    {
        public DateTime LastUpdate { get; set; } = DateTime.MinValue;
        public long NetLiquidation { get; set; } = long.MinValue;    // prefer whole numbers. Max int32 is 2B.
        public long GrossPositionValue { get; set; } = long.MinValue;
        public long TotalCashValue { get; set; } = long.MinValue;
        public long InitMarginReq { get; set; } = long.MinValue;
        public long MaintMarginReq { get; set; } = long.MinValue;
        public List<BrAccViewerPos> Poss { get; set; } = new List<BrAccViewerPos>();
    }

    public partial class DashboardClient
    {

        void Ctor_BrAccViewer()
        {
            // InitAssetData();
        }

        void EvMemDbAssetDataReloaded_BrAccViewer()
        {
            //InitAssetData();
        }

        // Return from this function very quickly. Do not call any Clients.Caller.SendAsync(), because client will not notice that connection is Connected, and therefore cannot send extra messages until we return here
        public void OnConnectedWsAsync_BrAccViewer(bool p_isThisActiveToolAtConnectionInit)
        {
            Task.Run(() => // running parallel on a ThreadPool thread
            {
                Thread.CurrentThread.IsBackground = true;  //  thread will be killed when all foreground threads have died, the thread will not keep the application alive.

                // Assuming this tool is not the main Tab page on the client, we delay sending all the data, to avoid making the network and client too busy an unresponsive
                if (!p_isThisActiveToolAtConnectionInit)
                    Thread.Sleep(TimeSpan.FromMilliseconds(5000));

                // BrAccViewer is not visible at the start for the user. We don't have to hurry to be responsive. 
                // With the handshake msg, we can take our time to collect All necessary data, and send it a bit (500ms) later.
                HandshakeBrAccViewer handshake = GetHandshakeBrAccViewer();
                byte[] encodedMsg = Encoding.UTF8.GetBytes("BrAccViewer.Handshake:" + Utils.CamelCaseSerialize(handshake));
                if (WsWebSocket!.State == WebSocketState.Open)
                    WsWebSocket.SendAsync(new ArraySegment<Byte>(encodedMsg, 0, encodedMsg.Length), WebSocketMessageType.Text, true, CancellationToken.None);

                var brAcc = GetBrAccount("N/DC");
                if (brAcc != null)
                {
                    encodedMsg = Encoding.UTF8.GetBytes("BrAccViewer.BrAccPoss:"  + Utils.CamelCaseSerialize(brAcc));
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

        private HandshakeBrAccViewer GetHandshakeBrAccViewer()
        {
            //string selectableNavs = "GA.IM, DC(virtual), DC.IM, DC.IB";
            List<BrokerNav> selectableNavs = MemDb.gMemDb.Users.FirstOrDefault(r => r.Email == UserEmail)!.GetAllVisibleBrokerNavsOrdered();
            string selectableBrAccsCSV = String.Join(',', selectableNavs.Select(r => r.Symbol));
            return new HandshakeBrAccViewer() { SelectableBrAccs = selectableBrAccsCSV };
        }

        private BrAccViewerAccount? GetBrAccount(string p_sqTicker) // "N/GA.IM, N/DC, N/DC.IM, N/DC.IB"
        {
            // if it is aggregated portfolio (DC Main + DeBlanzac), then a virtual combination is needed
            if (!GatewayExtensions.NavSqSymbol2GatewayIds.TryGetValue(p_sqTicker, out List<GatewayId>? gatewayIds))
                return null;

            BrAccViewerAccount? result = null;
            foreach (GatewayId gwId in gatewayIds)
            {
                BrAccount? brAccount = MemDb.gMemDb.BrAccounts.FirstOrDefault(r => r.GatewayId == gwId);
                if (brAccount == null)
                    return null;

                if (result == null)
                {
                    result = new BrAccViewerAccount()
                    {
                        LastUpdate = brAccount.LastUpdate,
                        GrossPositionValue = (long)brAccount.GrossPositionValue,
                        TotalCashValue = (long)brAccount.TotalCashValue,
                        InitMarginReq = (long)brAccount.InitMarginReq,
                        MaintMarginReq = (long)brAccount.MaintMarginReq,
                        Poss = brAccount.AccPoss.Select(r => new BrAccViewerPos()
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
                    result.GrossPositionValue += (long)brAccount.GrossPositionValue;
                    result.TotalCashValue += (long)brAccount.TotalCashValue;
                    result.InitMarginReq += (long)brAccount.InitMarginReq;
                    result.MaintMarginReq += (long)brAccount.MaintMarginReq;
                    result.Poss.AddRange(brAccount.AccPoss.Select(r => new BrAccViewerPos()
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
                Asset navAsset = MemDb.gMemDb.AssetsCache.AssetsBySqTicker[p_sqTicker];    // realtime NavAsset.LastValue is more up-to-date then from BrAccount (updated 1h in RTH only)
                result.NetLiquidation = (long)MemDb.gMemDb.GetLastRtValue(navAsset);
            }
            return result;
        }
    }
}