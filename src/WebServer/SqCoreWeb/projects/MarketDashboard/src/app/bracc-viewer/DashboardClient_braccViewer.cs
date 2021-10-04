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

    class BrAccViewerPosJs  // sent to browser clients
    {
        public uint AssetId { get; set; } = 0;
        public string SqTicker { get; set; } = string.Empty;
        public string Symbol { get; set; } = string.Empty;  // can be shown on the HTML UI

        public string Name { get; set; } = string.Empty;
        public double Pos { get; set; }
        public double AvgCost { get; set; }

        // Double.NaN cannot be serialized. Send 0.0 for missing values.

        [JsonConverter(typeof(FloatJsonConverterToNumber4D))]
        public float PriorClose { get; set; } = 0.0f;  // MktValue can be calculated
        public double EstPrice { get; set; } = 0.0;  // MktValue can be calculated, 
        public double EstUndPrice { get; set; } = 0.0;   // In case of options DeliveryValue can be calculated

        public string AccId { get; set; } = string.Empty; // AccountId: "Cha", "DeB", "Gya" (in case of virtual combined portfolio)
    }

    class BrAccViewerAccountSnapshotJs // this is sent to UI client
    {
        public String Symbol { get; set; } = string.Empty;
        public DateTime LastUpdate { get; set; } = DateTime.MinValue;
        public long NetLiquidation { get; set; } = long.MinValue;    // prefer whole numbers. Max int32 is 2B.
        public long PriorCloseNetLiquidation { get; set; } = 0; 
        public long GrossPositionValue { get; set; } = long.MinValue;
        public long TotalCashValue { get; set; } = long.MinValue;
        public long InitMarginReq { get; set; } = long.MinValue;
        public long MaintMarginReq { get; set; } = long.MinValue;
        public List<BrAccViewerPosJs> Poss { get; set; } = new List<BrAccViewerPosJs>();

    }

    public partial class DashboardClient
    {
        // If we store asset pointers (Stock, Nav) if the MemDb reloads, we should reload these pointers from the new MemDb. That adds extra code complexity.
        // However, for fast execution, it is still better to keep asset pointers, instead of keeping the asset's SqTicker and always find them again and again in MemDb.
        BrokerNav? m_braccSelectedNavAsset = null;   // remember which NAV is selected, so we can send RT data
        List<string> c_marketBarSqTickersDefault = new List<string>() { "S/QQQ", "S/SPY", "S/TLT", "S/VXX", "S/UNG", "S/USO", "S/AMZN"};    // TEMP: AMZN is here to test that realtime price is sent to client properly
        List<string> c_marketBarSqTickersDc = new List<string>() { "S/QQQ", "S/SPY", "S/TLT", "S/VXX", "S/UNG", "S/USO", "S/GLD"};
        List<Asset> m_brAccMktBrAssets = new List<Asset>();      // remember, so we can send RT data

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
        public void OnConnectedWsAsync_BrAccViewer(bool p_isThisActiveToolAtConnectionInit, User p_user, ManualResetEvent p_waitHandleRtPriceSending)
        {
            Utils.RunInNewThread(ignored => // running parallel on a ThreadPool thread, FireAndForget: QueueUserWorkItem [26microsec] is 25% faster than Task.Run [35microsec]
            {
                Thread.CurrentThread.IsBackground = true;  //  thread will be killed when all foreground threads have died, the thread will not keep the application alive.

                List<BrokerNav> selectableNavs = p_user.GetAllVisibleBrokerNavsOrdered();
                m_braccSelectedNavAsset = selectableNavs.FirstOrDefault();

                List<string> marketBarSqTickers = (p_user.Username == "drcharmat") ? c_marketBarSqTickersDc : c_marketBarSqTickersDefault;
                m_brAccMktBrAssets = marketBarSqTickers.Select(r => MemDb.gMemDb.AssetsCache.GetAsset(r)).ToList();

                HandshakeBrAccViewer handshake = GetHandshakeBrAccViewer(selectableNavs);
                byte[] encodedMsg = Encoding.UTF8.GetBytes("BrAccViewer.Handshake:" + Utils.CamelCaseSerialize(handshake));
                if (WsWebSocket!.State == WebSocketState.Open)
                    WsWebSocket.SendAsync(new ArraySegment<Byte>(encodedMsg, 0, encodedMsg.Length), WebSocketMessageType.Text, true, CancellationToken.None);

                p_waitHandleRtPriceSending.Set();   // after handshake was sent to this Tool, assume tool can handle if RtPrice arrives.

                // Assuming this tool is not the main Tab page on the client, we delay sending all the data, to avoid making the network and client too busy an unresponsive
                if (!p_isThisActiveToolAtConnectionInit)
                    Thread.Sleep(TimeSpan.FromMilliseconds(5000));

                BrAccViewerSendMarketBarPriorCloses();

                BrAccViewerSendSnapshotAndHist();
            });
        }

        private void BrAccViewerSendMarketBarPriorCloses()
        {
            DateTime mockupPriorDate = DateTime.UtcNow.Date.AddDays(-1); // we get PriorClose from Asset directly. That comes from YF, which don't tell us the date of PriorClose

            var mktBrPriorCloses = m_brAccMktBrAssets.Select(r =>
            {
                return new AssetPriorCloseJs() { AssetId = r.AssetId, PriorClose = r.PriorClose, Date = mockupPriorDate };
            });

            byte[] encodedMsg = Encoding.UTF8.GetBytes("BrAccViewer.MktBrLstCls:" + Utils.CamelCaseSerialize(mktBrPriorCloses));
            if (WsWebSocket!.State == WebSocketState.Open)
                WsWebSocket.SendAsync(new ArraySegment<Byte>(encodedMsg, 0, encodedMsg.Length), WebSocketMessageType.Text, true, CancellationToken.None);
        }

        private void BrAccViewerSendSnapshotAndHist()
        {
            if (m_braccSelectedNavAsset == null)
                return;
            BrAccViewerSendSnapshot();
            BrAccViewerSendHist();
        }

        private void BrAccViewerSendSnapshot()
        {
            byte[]? encodedMsg = null;
            var brAcc = GetBrAccViewerAccountSnapshot();
            if (brAcc != null)
            {
                encodedMsg = Encoding.UTF8.GetBytes("BrAccViewer.BrAccSnapshot:" + Utils.CamelCaseSerialize(brAcc));
                if (WsWebSocket!.State == WebSocketState.Open)
                    WsWebSocket.SendAsync(new ArraySegment<Byte>(encodedMsg, 0, encodedMsg.Length), WebSocketMessageType.Text, true, CancellationToken.None);
            }
        }
        private void BrAccViewerSendHist()
        {
            byte[]? encodedMsg = null;
            IEnumerable<AssetHistJs>? brAccViewerHist = GetBrAccViewerHist("YTD");
            if (brAccViewerHist != null)
            {
                encodedMsg = Encoding.UTF8.GetBytes("BrAccViewer.Hist:" + Utils.CamelCaseSerialize(brAccViewerHist));
                if (WsWebSocket!.State == WebSocketState.Open)
                    WsWebSocket.SendAsync(new ArraySegment<Byte>(encodedMsg, 0, encodedMsg.Length), WebSocketMessageType.Text, true, CancellationToken.None);
            }
        }

        private HandshakeBrAccViewer GetHandshakeBrAccViewer(List<BrokerNav> p_selectableNavs)
        {
            List<AssetJs> marketBarAssets = m_brAccMktBrAssets.Select(r => new AssetJs() { AssetId = r.AssetId, SqTicker = r.SqTicker, Symbol = r.Symbol, Name = r.Name }).ToList();
            List<AssetJs> selectableNavAssets = p_selectableNavs.Select(r => new AssetJs() { AssetId = r.AssetId, SqTicker = r.SqTicker, Symbol = r.Symbol, Name = r.Name }).ToList();

            return new HandshakeBrAccViewer() { MarketBarAssets = marketBarAssets, SelectableNavAssets = selectableNavAssets };
        }

        private BrAccViewerAccountSnapshotJs? GetBrAccViewerAccountSnapshot() // "N/GA.IM, N/DC, N/DC.IM, N/DC.IB"
        {
            if (m_braccSelectedNavAsset == null)
                return null;
            string navSqTicker = m_braccSelectedNavAsset.SqTicker;
            // if it is aggregated portfolio (DC Main + DeBlanzac), then a virtual combination is needed
            if (!GatewayExtensions.NavSqSymbol2GatewayIds.TryGetValue(navSqTicker, out List<GatewayId>? gatewayIds))
                return null;

            TsDateData<DateOnly, uint, float, uint> histData = MemDb.gMemDb.DailyHist.GetDataDirect();

            BrAccViewerAccountSnapshotJs? result = null;
            foreach (GatewayId gwId in gatewayIds)  // AggregateNav has 2 Gateways
            {
                BrAccount? brAccount = MemDb.gMemDb.BrAccounts.FirstOrDefault(r => r.GatewayId == gwId);
                if (brAccount == null)
                    return null;

                string gwIdStr = gwId.ToShortFriendlyString();
                if (result == null)
                {
                    result = new BrAccViewerAccountSnapshotJs()
                    {
                        Symbol = navSqTicker.Replace("N/", string.Empty),
                        LastUpdate = brAccount.LastUpdate,
                        GrossPositionValue = (long)brAccount.GrossPositionValue,
                        TotalCashValue = (long)brAccount.TotalCashValue,
                        InitMarginReq = (long)brAccount.InitMarginReq,
                        MaintMarginReq = (long)brAccount.MaintMarginReq,
                        Poss = GetBrAccViewerPos(brAccount.AccPoss, gwIdStr).ToList()
                    };
                }
                else
                {
                    result.GrossPositionValue += (long)brAccount.GrossPositionValue;
                    result.TotalCashValue += (long)brAccount.TotalCashValue;
                    result.InitMarginReq += (long)brAccount.InitMarginReq;
                    result.MaintMarginReq += (long)brAccount.MaintMarginReq;
                    result.Poss.AddRange(GetBrAccViewerPos(brAccount.AccPoss, gwIdStr));
                }
            }

            if (result != null)
            {
                Asset navAsset = MemDb.gMemDb.AssetsCache.AssetsBySqTicker[navSqTicker];    // realtime NavAsset.LastValue is more up-to-date then from BrAccount (updated 1h in RTH only)
                result.NetLiquidation = (long)MemDb.gMemDb.GetLastRtValue(navAsset);

                List<Asset> assets = new List<Asset>() { m_braccSelectedNavAsset };
                DateTime todayET = Utils.ConvertTimeFromUtcToEt(DateTime.UtcNow).Date;
                List<AssetPriorClose> priorCloses = MemDb.gMemDb.GetSdaPriorClosesFromHist(assets, todayET).ToList();
                result.PriorCloseNetLiquidation  = (long)priorCloses[0].SdaPriorClose;
            }
            return result;
        }

        private List<BrAccViewerPosJs> GetBrAccViewerPos(List<BrAccPos> p_accPoss, string p_gwIdStr)
        {
            // One option is to only send those positions that have both valid AssetId (260 stocks) AND valid HistoricalPrice (only 20 stocks subset)
            // Because in general PriorClose is needed in the UI calculations.
            // But decided it is better to send these PriorClose=NaN rows as well. To show on the client that those historical prices are missing. Needs fixing in MemDb.Hist.
            List<BrAccPos> validBrPoss = p_accPoss.Where(r => r.AssetId != AssetId32Bits.Invalid).ToList();
            List<Asset> validBrPossAssets = validBrPoss.Select(r => MemDb.gMemDb.AssetsCache.AssetsByAssetID[r.AssetId]).ToList();

            // merge the 2 lists together: validBrPoss, validBrPossAssets
            List<BrAccViewerPosJs> result = new List<BrAccViewerPosJs>(validBrPoss.Count);
            for (int i = 0; i < validBrPoss.Count; i++)
            {
                BrAccPos posBr = validBrPoss[i];
                Asset asset = validBrPossAssets[i];
                result.Add(new BrAccViewerPosJs()
                {
                    AssetId = posBr.AssetId,
                    SqTicker = asset.SqTicker,
                    Symbol = asset.Symbol,
                    Name = asset.Name,
                    Pos = posBr.Position,
                    AvgCost = posBr.AvgCost,
                    PriorClose = asset.PriorClose,
                    EstPrice = asset.LastValue,
                    AccId = p_gwIdStr
                });
            }
            return result;
        }

        private IEnumerable<AssetHistJs>? GetBrAccViewerHist(string p_lookbackStr)
        {
            if (m_braccSelectedNavAsset == null)
                return null;

            List<Asset> assets = new List<Asset>();
            assets.Add(m_braccSelectedNavAsset);
            assets.Add(MemDb.gMemDb.AssetsCache.GetAsset("S/SPY")); // add it to BrokerNav for benchmark for the chart

            DateTime todayET = Utils.ConvertTimeFromUtcToEt(DateTime.UtcNow).Date;  // the default is YTD. Leave it as it is used frequently: by default server sends this to client at Open. Or at EvMemDbHistoricalDataReloaded_mktHealth()
            DateOnly lookbackStart = new DateOnly(todayET.Year - 1, 12, 31);  // YTD relative to 31st December, last year
            DateOnly lookbackEnd = todayET.AddDays(-1);
            if (p_lookbackStr.StartsWith("Date:"))  // Browser client never send anything, but "Date:" inputs. Format: "Date:2019-11-11...2020-11-10"
            {
                lookbackStart = Utils.FastParseYYYYMMDD(new StringSegment(p_lookbackStr, "Date:".Length, 10));
                lookbackEnd = Utils.FastParseYYYYMMDD(new StringSegment(p_lookbackStr, "Date:".Length + 13, 10));
            }

            IEnumerable<AssetHist> assetHists = MemDb.gMemDb.GetSdaHistCloses(assets, lookbackStart, lookbackEnd, true, true);

            IEnumerable<AssetHistJs> histToClient = assetHists.Select(r =>
            {
                var histStat = new AssetHistStatJs()
                {
                    AssetId = r.Asset.AssetId,
                    SqTicker = r.Asset.SqTicker,
                    PeriodStartDate = r.PeriodStartDate,    // it may be not the 'asked' start date if asset has less price history
                    PeriodEndDate = r.PeriodEndDate,        // by default it is the date of yesterday, but the user can change it
                    PeriodStart = r.Stat?.PeriodStart ?? Double.NaN,
                    PeriodEnd = r.Stat?.PeriodEnd ?? Double.NaN,
                    PeriodHigh = r.Stat?.PeriodHigh ?? Double.NaN,
                    PeriodLow = r.Stat?.PeriodLow ?? Double.NaN,
                    PeriodMaxDD = r.Stat?.PeriodMaxDD ?? Double.NaN,
                    PeriodMaxDU = r.Stat?.PeriodMaxDU ?? Double.NaN
                };

                var dates = r.Values!.Select(k => ((DateTime)k.Date).ToYYYYMMDD()).ToList();
                var values = r.Values!.Select(k => k.SdaValue).ToList();

                var histValues = new AssetHistValuesJs()
                {
                    AssetId = r.Asset.AssetId,
                    SqTicker = r.Asset.SqTicker,
                    PeriodStartDate = r.PeriodStartDate,
                    PeriodEndDate = r.PeriodEndDate,
                    HistDates = dates,
                    HistSdaCloses = values
                };

                return new AssetHistJs() {HistValues = histValues, HistStat = histStat};
            });

            return histToClient;
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
                case "BrAccViewer.RefreshSnapshot":
                    BrAccViewerRefreshSnapshot();
                    return true;
                default:
                    return false;
            }
        }

        private void BrAccViewerRefreshSnapshot()
        {
            // Step 1: Force reload of poss from IB Gateways.

            // Step 2: send Snapshot to Client.
            BrAccViewerSendSnapshot();
            
        }
    }
}