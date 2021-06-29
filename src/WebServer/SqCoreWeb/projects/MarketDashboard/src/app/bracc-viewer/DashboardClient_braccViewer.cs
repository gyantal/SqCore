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
    {    //Initial params
        public String SelectableNavs { get; set; } = string.Empty;
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

    class BrAccViewerAccountSnapshot // this is sent to UI client
    {
        public String Symbol { get; set; } = string.Empty;
        public DateTime LastUpdate { get; set; } = DateTime.MinValue;
        public long NetLiquidation { get; set; } = long.MinValue;    // prefer whole numbers. Max int32 is 2B.
        public long GrossPositionValue { get; set; } = long.MinValue;
        public long TotalCashValue { get; set; } = long.MinValue;
        public long InitMarginReq { get; set; } = long.MinValue;
        public long MaintMarginReq { get; set; } = long.MinValue;
        public List<BrAccViewerPos> Poss { get; set; } = new List<BrAccViewerPos>();

    }



    // Don't integrate this to BrAccViewerAccount. By default we sent YTD. But client might ask for last 10 years. 
    // But we don't want to send 10 years data and the today positions snapshot all the time together.
    public class BrAccViewerHist   // this is sent to clients usually just once per day, OR when historical data changes, OR when the PeriodStartDate changes at the client
    {
        public uint AssetId { get; set; } = 0;        // set the Client know what is the assetId, because Rt will not send it.
        public String SqTicker { get; set; } = string.Empty;  // this has to be SqTicker, not Symbol. "N/DC" is different to "S/DC"

        public DateTime PeriodStartDate { get; set; } = DateTime.MinValue;
        public DateTime PeriodEndDate { get; set; } = DateTime.MinValue;

        public List<string> HistDates { get; set; } = new List<string>();   // we convert manually DateOnly to short string
        public List<int> HistSdaCloses { get; set; } = new List<int>(); // NAV value: float takes too much data
    }

    class BrAccViewerAssetRt  // see RtMktSumRtStat. Can we refactor that to AssetRtStat and use that in both places?
    {
        // sent SPY realtime price can be used in 3 places: MarketBar, HistoricalChart, UserAssetList (so, don't send it 3 times. Client will decide what to do with RT price)
        // sent NAV realtime price can be used in 2 places: HistoricalChart, AccountSummary
    }

    public partial class DashboardClient
    {
        BrokerNav? m_selectedNavAsset = null;   // remember which NAV is selected so we can send RT data

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
                //string selectableNavs = "GA.IM, DC, DC.IM, DC.IB";
                List<BrokerNav> selectableNavs = MemDb.gMemDb.Users.FirstOrDefault(r => r.Email == UserEmail)!.GetAllVisibleBrokerNavsOrdered();
                HandshakeBrAccViewer handshake = GetHandshakeBrAccViewer(selectableNavs);
                byte[] encodedMsg = Encoding.UTF8.GetBytes("BrAccViewer.Handshake:" + Utils.CamelCaseSerialize(handshake));
                if (WsWebSocket!.State == WebSocketState.Open)
                    WsWebSocket.SendAsync(new ArraySegment<Byte>(encodedMsg, 0, encodedMsg.Length), WebSocketMessageType.Text, true, CancellationToken.None);
                
                m_selectedNavAsset = selectableNavs.FirstOrDefault();
                if (m_selectedNavAsset != null)
                    BrAccViewerSendSnapshotAndHist(m_selectedNavAsset.SqTicker);

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

        private void BrAccViewerSendSnapshotAndHist(string p_sqTicker)
        {
            byte[]? encodedMsg = null;
            var brAcc = GetBrAccViewerAccountSnapshot(p_sqTicker);
            if (brAcc != null)
            {
                encodedMsg = Encoding.UTF8.GetBytes("BrAccViewer.BrAccSnapshot:" + Utils.CamelCaseSerialize(brAcc));
                if (WsWebSocket!.State == WebSocketState.Open)
                    WsWebSocket.SendAsync(new ArraySegment<Byte>(encodedMsg, 0, encodedMsg.Length), WebSocketMessageType.Text, true, CancellationToken.None);
            }

            IEnumerable<BrAccViewerHist> brAccViewerHist = GetBrAccViewerHist(p_sqTicker, "YTD");
            if (brAccViewerHist != null)
            {
                encodedMsg = Encoding.UTF8.GetBytes("BrAccViewer.Hist:" + Utils.CamelCaseSerialize(brAccViewerHist));
                if (WsWebSocket!.State == WebSocketState.Open)
                    WsWebSocket.SendAsync(new ArraySegment<Byte>(encodedMsg, 0, encodedMsg.Length), WebSocketMessageType.Text, true, CancellationToken.None);
            }
        }

        private HandshakeBrAccViewer GetHandshakeBrAccViewer(List<BrokerNav> p_selectableNavs)
        {
            string selectableNavsCSV = String.Join(',', p_selectableNavs.Select(r => r.Symbol));
            return new HandshakeBrAccViewer() { SelectableNavs = selectableNavsCSV };
        }

        private BrAccViewerAccountSnapshot? GetBrAccViewerAccountSnapshot(string p_sqTicker) // "N/GA.IM, N/DC, N/DC.IM, N/DC.IB"
        {
            // if it is aggregated portfolio (DC Main + DeBlanzac), then a virtual combination is needed
            if (!GatewayExtensions.NavSqSymbol2GatewayIds.TryGetValue(p_sqTicker, out List<GatewayId>? gatewayIds))
                return null;

            TsDateData<DateOnly, uint, float, uint> histData = MemDb.gMemDb.DailyHist.GetDataDirect();

            BrAccViewerAccountSnapshot? result = null;
            foreach (GatewayId gwId in gatewayIds)
            {
                BrAccount? brAccount = MemDb.gMemDb.BrAccounts.FirstOrDefault(r => r.GatewayId == gwId);
                if (brAccount == null)
                    return null;

                if (result == null)
                {
                    result = new BrAccViewerAccountSnapshot()
                    {
                        Symbol = p_sqTicker.Replace("N/", string.Empty),
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

        private IEnumerable<BrAccViewerHist> GetBrAccViewerHist(string p_sqTicker, string p_lookbackStr)
        {
            var result = new List<BrAccViewerHist>();
            var navHist = new BrAccViewerHist()
            {
                AssetId = 1,
                SqTicker = p_sqTicker,
                PeriodStartDate = DateTime.UtcNow.Date,
                PeriodEndDate = DateTime.UtcNow.Date,
                HistDates = new List<string>() { new DateTime(2021, 1, 21).ToYYYYMMDD() },
                HistSdaCloses = new List<int>() { 1234 }
            };
            result.Add(navHist);
            var spyHist = new BrAccViewerHist()
            {
                SqTicker = "S/SPY",
                PeriodStartDate = DateTime.UtcNow.Date,
                PeriodEndDate = DateTime.UtcNow.Date,
                HistDates = new List<string>() { new DateTime(2021, 2, 20).ToYYYYMMDD() },
                HistSdaCloses = new List<int>() { 4300 }
            };
            result.Add(spyHist);
            return result;
        }

        public bool OnReceiveWsAsync_BrAccViewer(WebSocketReceiveResult? wsResult, string msgCode, string msgObjStr)
        {
            switch (msgCode)
            {
                case "BrAccViewer.ChangeLookback":
                    Utils.Logger.Info("OnReceiveWsAsync_BrAccViewer(): changeLookback");
                    // m_lastLookbackPeriodStr = msgObjStr;
                    // SendHistoricalWs();
                    return true;
                case "BrAccViewer.ChangeNav":
                    Utils.Logger.Info($"OnReceiveWsAsync_BrAccViewer(): changeNav to '{msgObjStr}'"); // DC.IM
                    string sqTicker = "N/" + msgObjStr; // turn DC.IM to N/DC.IM
                    BrAccViewerSendSnapshotAndHist(sqTicker);
                    // var navAsset = MemDb.gMemDb.AssetsCache.GetAsset(sqTicker);
                    // RtMktSummaryStock? navStock = m_mktSummaryStocks.FirstOrDefault(r => r.SqTicker == g_brNavVirtualTicker);
                    // if (navStock != null)
                    //     navStock.AssetId = navAsset!.AssetId;

                    // SendHistoricalWs();
                    // SendRealtimeWs();
                    return true;
                default:
                    return false;
            }
        }

    }
}