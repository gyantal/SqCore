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
        public List<AssetJs> MarketBarAssets { get; set; } = new List<AssetJs>();
        public List<AssetJs> SelectableNavAssets { get; set; } = new List<AssetJs>();

        // Don't send ChartBenchmarkPossibleAssets at the beginning. By default, we don't want to compare with anything. Keep the connection fast. It is not needed usually.
        // However, there will be a text input for CSV values of tickers, like "SPY,QQQ". If user types that and click then server should answer and send the BenchMarkAsset
        // But it should not be in the intial Handshake.

        // public List<AssetJs> ChartBenchmarkPossibleAssets { get; set; } = new List<AssetJs>();
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

    public partial class DashboardClient
    {
        // If we store asset pointers (Stock, Nav) if the MemDb reloads, we should reload these pointers from the new MemDb. That adds extra code complexity.
        // However, for fast execution, it is still better to keep asset pointers, instead of keeping the asset's SqTicker and always find them again and again in MemDb.
        BrokerNav? m_braccSelectedNavAsset = null;   // remember which NAV is selected, so we can send RT data
        List<string> c_marketBarSqTickersDefault = new List<string>() { "S/QQQ", "S/SPY", "S/TLT", "S/VXX", "S/UNG", "S/USO"};
        List<string> c_marketBarSqTickersDc = new List<string>() { "S/QQQ", "S/SPY", "S/TLT", "S/VXX", "S/UNG", "S/USO", "S/GLD"};
        List<Asset> m_marketBarAssets = new List<Asset>();      // remember, so we can send RT data

        List<Asset> m_navChartBenchmarkAssets = new List<Asset>();

        void Ctor_BrAccViewer()
        {
            // InitAssetData();
        }

        void EvMemDbAssetDataReloaded_BrAccViewer()
        {
            //InitAssetData();
        }

        void EvMemDbHistoricalDataReloaded_BrAccViewer()
        {
            // see EvMemDbHistoricalDataReloaded_MktHealth()()
        }

        // Return from this function very quickly. Do not call any Clients.Caller.SendAsync(), because client will not notice that connection is Connected, and therefore cannot send extra messages until we return here
        public void OnConnectedWsAsync_BrAccViewer(bool p_isThisActiveToolAtConnectionInit, User p_user)
        {
            Task.Run(() => // running parallel on a ThreadPool thread
            {
                Thread.CurrentThread.IsBackground = true;  //  thread will be killed when all foreground threads have died, the thread will not keep the application alive.

                // Assuming this tool is not the main Tab page on the client, we delay sending all the data, to avoid making the network and client too busy an unresponsive
                if (!p_isThisActiveToolAtConnectionInit)
                    Thread.Sleep(TimeSpan.FromMilliseconds(5000));

                List<BrokerNav> selectableNavs = p_user.GetAllVisibleBrokerNavsOrdered();
                m_braccSelectedNavAsset = selectableNavs.FirstOrDefault();

                List<string> marketBarSqTickers = (p_user.Username == "drcharmat") ? c_marketBarSqTickersDc : c_marketBarSqTickersDefault;
                m_marketBarAssets = marketBarSqTickers.Select(r => MemDb.gMemDb.AssetsCache.GetAsset(r)).ToList();

                HandshakeBrAccViewer handshake = GetHandshakeBrAccViewer(selectableNavs);
                byte[] encodedMsg = Encoding.UTF8.GetBytes("BrAccViewer.Handshake:" + Utils.CamelCaseSerialize(handshake));
                if (WsWebSocket!.State == WebSocketState.Open)
                    WsWebSocket.SendAsync(new ArraySegment<Byte>(encodedMsg, 0, encodedMsg.Length), WebSocketMessageType.Text, true, CancellationToken.None);

                BrAccViewerSendMarketBarLastCloses();
                
                BrAccViewerSendSnapshotAndHist();

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

        private void BrAccViewerSendMarketBarLastCloses()
        {
            DateTime todayET = Utils.ConvertTimeFromUtcToEt(DateTime.UtcNow).Date;  // Can be Monday
            DateOnly lookbackEnd = todayET.AddDays(-1); // Can be Sunday, but we have to find Friday before

            TsDateData<DateOnly, uint, float, uint> histData = MemDb.gMemDb.DailyHist.GetDataDirect();
            DateOnly[] dates = histData.Dates;
            // At 16:00, or even intraday: YF gives even the today last-realtime price with a today-date. We have to find any date backwards, which is NOT today. That is the PreviousClose.
            int iEndDay = 0;
            for (int i = 0; i < dates.Length; i++)
            {
                if (dates[i] <= lookbackEnd)
                {
                    iEndDay = i;
                    break;
                }
            }
            Debug.WriteLine($"BrAccViewerSendMarketBarLastCloses().EndDate: {dates[iEndDay]}");

            var mktBrLastCloses = m_marketBarAssets.Select(r =>
            {
                float[] sdaCloses = histData.Data[r.AssetId].Item1[TickType.SplitDivAdjClose];
                return new AssetLastCloseJs() { AssetId = r.AssetId, SdaLastClose = sdaCloses[iEndDay], Date = dates[iEndDay] };
            });

            byte[] encodedMsg = Encoding.UTF8.GetBytes("BrAccViewer.MktBrLstCls:" + Utils.CamelCaseSerialize(mktBrLastCloses));
            if (WsWebSocket!.State == WebSocketState.Open)
                WsWebSocket.SendAsync(new ArraySegment<Byte>(encodedMsg, 0, encodedMsg.Length), WebSocketMessageType.Text, true, CancellationToken.None);
        }
        private void BrAccViewerSendSnapshotAndHist()
        {
            if (m_braccSelectedNavAsset == null)
                return;
            string sqTicker = m_braccSelectedNavAsset.SqTicker;
            byte[]? encodedMsg = null;
            var brAcc = GetBrAccViewerAccountSnapshot(sqTicker);
            if (brAcc != null)
            {
                encodedMsg = Encoding.UTF8.GetBytes("BrAccViewer.BrAccSnapshot:" + Utils.CamelCaseSerialize(brAcc));
                if (WsWebSocket!.State == WebSocketState.Open)
                    WsWebSocket.SendAsync(new ArraySegment<Byte>(encodedMsg, 0, encodedMsg.Length), WebSocketMessageType.Text, true, CancellationToken.None);
            }

            IEnumerable<AssetHistJs> brAccViewerHist = GetBrAccViewerHist(sqTicker, "YTD");
            if (brAccViewerHist != null)
            {
                encodedMsg = Encoding.UTF8.GetBytes("BrAccViewer.Hist:" + Utils.CamelCaseSerialize(brAccViewerHist));
                if (WsWebSocket!.State == WebSocketState.Open)
                    WsWebSocket.SendAsync(new ArraySegment<Byte>(encodedMsg, 0, encodedMsg.Length), WebSocketMessageType.Text, true, CancellationToken.None);
            }
        }


        private HandshakeBrAccViewer GetHandshakeBrAccViewer(List<BrokerNav> p_selectableNavs)
        {
            List<AssetJs> marketBarAssets = m_marketBarAssets.Select(r => new AssetJs() { AssetId = r.AssetId, SqTicker = r.SqTicker, Symbol = r.Symbol, Name = r.Name }).ToList();
            List<AssetJs> selectableNavAssets = p_selectableNavs.Select(r => new AssetJs() { AssetId = r.AssetId, SqTicker = r.SqTicker, Symbol = r.Symbol, Name = r.Name }).ToList();

            return new HandshakeBrAccViewer() { MarketBarAssets = marketBarAssets, SelectableNavAssets = selectableNavAssets };
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

        private IEnumerable<AssetHistJs> GetBrAccViewerHist(string p_sqTicker, string p_lookbackStr)
        {
            var result = new List<AssetHistJs>();
            var navHist = new AssetHistJs()
            {
                AssetId = 1,
                SqTicker = p_sqTicker,
                PeriodStartDate = DateTime.UtcNow.Date,
                PeriodEndDate = DateTime.UtcNow.Date,
                HistDates = new List<string>() { new DateTime(2021, 1, 21).ToYYYYMMDD() },
                HistSdaCloses = new List<double>() { 1234.0 }
            };
            result.Add(navHist);

            // By default we don't send any benchmark because the ChartBenchmarks text input is empty.
            
            // foreach (var benchmark in m_navChartBenchmarkAssets) { 
            var spyHist = new AssetHistJs()
            {
                SqTicker = "S/SPY",
                PeriodStartDate = DateTime.UtcNow.Date,
                PeriodEndDate = DateTime.UtcNow.Date,
                HistDates = new List<string>() { new DateTime(2021, 2, 20).ToYYYYMMDD() },
                HistSdaCloses = new List<double>() { 4300.1 }
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
                    m_braccSelectedNavAsset = MemDb.gMemDb.AssetsCache.GetAsset(sqTicker) as BrokerNav;
                    BrAccViewerSendSnapshotAndHist();
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