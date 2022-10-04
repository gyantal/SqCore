using System;
using System.Threading;
using SqCommon;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using FinTechCommon;
using System.Text.Json.Serialization;
using System.Net.WebSockets;
using Microsoft.Extensions.Primitives;
using BrokerCommon;
using System.Diagnostics;
using System.Threading.Tasks;

namespace SqCoreWeb;


class AssetCategoryJs
{
    public string Tag { get; set; } = string.Empty;
    public List<string> SqTickers { get; set; } = new List<string>();
}

class HandshakeBrAccViewer
{    //Initial params
    public List<AssetJs> MarketBarAssets { get; set; } = new();
    public List<AssetJs> SelectableNavAssets { get; set; } = new();
    public List<AssetCategoryJs> AssetCategories { get; set; } = new();

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
    public string SymbolEx { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;
    public double Pos { get; set; }

    [JsonConverter(typeof(DoubleJsonConverterToNumber4D))]
    public double AvgCost { get; set; }

    // Double.NaN cannot be serialized. Send 0.0 for missing values.

    [JsonConverter(typeof(DoubleJsonConverterToNumber4D))]
    public double PriorClose { get; set; } = 0.0f;  // MktValue can be calculated

    [JsonConverter(typeof(DoubleJsonConverterToNumber4D))]
    public double EstPrice { get; set; } = 0.0;  // MktValue can be calculated, 

    [JsonConverter(typeof(DoubleJsonConverterToNumber4D))]
    public double EstUndPrice { get; set; } = 0.0;   // In case of options DeliveryValue can be calculated

    [JsonConverter(typeof(DoubleJsonConverterToNumber4D))]
    public double IbCompDelta { get; set; } = 0.0;   // Ib computed Delta for options

    public string AccId { get; set; } = string.Empty; // AccountId: "Cha", "DeB", "Gya" (in case of virtual combined portfolio)
}

class BrAccViewerAccountSnapshotJs // this is sent to UI client
{
    public uint AssetId { get; set; } = 0;
    public string Symbol { get; set; } = string.Empty;
    public DateTime LastUpdate { get; set; } = DateTime.MinValue;
    public long NetLiquidation { get; set; } = long.MinValue;    // prefer whole numbers. Max int32 is 2B.
    public long PriorCloseNetLiquidation { get; set; } = 0; 
    public long GrossPositionValue { get; set; } = long.MinValue;
    public long TotalCashValue { get; set; } = long.MinValue;
    public long InitMarginReq { get; set; } = long.MinValue;
    public long MaintMarginReq { get; set; } = long.MinValue;
    public List<BrAccViewerPosJs> Poss { get; set; } = new List<BrAccViewerPosJs>();

    public string ClientMsg { get; set; } = string.Empty;     // string can send many warnings at once. Separated by ";". Such as: "Info:...;Warning: ...;Warning: ...;Error:"
}

public partial class DashboardClient
{
    // If we store asset pointers (Stock, Nav) if the MemDb reloads, we should reload these pointers from the new MemDb. That adds extra code complexity.
    // However, for fast execution, it is still better to keep asset pointers, instead of keeping the asset's SqTicker and always find them again and again in MemDb.
    BrokerNav? m_braccSelectedNavAsset = null;   // remember which NAV is selected, so we can send RT data
    readonly List<string> c_marketBarSqTickersDefault = new() { "S/QQQ", "S/SPY", "S/TLT", "S/VXX", "S/UNG", "S/USO", "S/AMZN"};    // TEMP: AMZN is here to test that realtime price is sent to client properly
    readonly List<string> c_marketBarSqTickersDc = new() { "S/QQQ", "S/SPY", "S/TLT", "S/VXX", "S/UNG", "S/USO", "S/GLD"};
    List<Asset> m_brAccMktBrAssets = new();      // remember, so we can send RT data
    List<AssetCategoryJs> m_assetCategories = new();

    // void EvMemDbAssetDataReloaded_BrAccViewer()
    // {
    //     //InitAssetData();
    // }

    // void EvMemDbHistoricalDataReloaded_BrAccViewer()
    // {
    //     // see EvMemDbHistoricalDataReloaded_MktHealth()()
    // }

    // Return from this function very quickly. Do not call any Clients.Caller.SendAsync(), because client will not notice that connection is Connected, and therefore cannot send extra messages until we return here
    public void OnConnectedWsAsync_BrAccViewer(bool p_isThisActiveToolAtConnectionInit, User p_user, ManualResetEvent p_waitHandleRtPriceSending)
    {
        Utils.RunInNewThread(ignored => // running parallel on a ThreadPool thread, FireAndForget: QueueUserWorkItem [26microsec] is 25% faster than Task.Run [35microsec]
        {
            Utils.Logger.Debug($"OnConnectedWsAsync_BrAccViewer BEGIN, Connection from IP: {this.ClientIP} with email '{this.UserEmail}'");

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

            Utils.Logger.Debug($"OnConnectedWsAsync_BrAccViewer.SendMarketBarPriorCloses() BEGIN, Connection from IP: {this.ClientIP} with email '{this.UserEmail}'");
            BrAccViewerSendMarketBarPriorCloses();

            Utils.Logger.Debug($"OnConnectedWsAsync_BrAccViewer.SendSnapshotAndHist() BEGIN, Connection from IP: {this.ClientIP} with email '{this.UserEmail}'");
            DateTime todayET = Utils.ConvertTimeFromUtcToEt(DateTime.UtcNow).Date;  // the default is YTD. Leave it as it is used frequently: by default server sends this to client at Open. Or at EvMemDbHistoricalDataReloaded_mktHealth()
            SqDateOnly lookbackStart = new(todayET.Year - 1, 12, 31);  // YTD relative to 31st December, last year
            SqDateOnly lookbackEndExcl = todayET;
            BrAccViewerSendSnapshotAndHist(lookbackStart, lookbackEndExcl, "S/SPY");
            Utils.Logger.Debug($"OnConnectedWsAsync_BrAccViewer END, Connection from IP: {this.ClientIP} with email '{this.UserEmail}'");
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

    private void BrAccViewerSendSnapshotAndHist(SqDateOnly p_lookbackStart, SqDateOnly p_lookbackEndExcl, string p_bnchmrkTicker)
    {
        Stopwatch sw1 = Stopwatch.StartNew();
        if (m_braccSelectedNavAsset == null)
            return;

        BrAccViewerSendSnapshot();
        BrAccViewerSendNavHist(p_lookbackStart, p_lookbackEndExcl, p_bnchmrkTicker);

        sw1.Stop();
        Utils.Logger.Info($"BrAccViewerSendSnapshotAndHist() ends in {sw1.ElapsedMilliseconds}ms, p_bnchmrkTicker: '{p_bnchmrkTicker}'");
    }

    private async void BrAccViewerSendSnapshot()
    {
        // TEMP: 2022-06-23: temporary benchmarking with Stopwatch. Will be removed after we found the occasional lag problem.
        Stopwatch sw = Stopwatch.StartNew();
        Stopwatch sw1 = Stopwatch.StartNew();
        var brAcc = GetBrAccViewerAccountSnapshot();
        sw1.Stop();

        Stopwatch sw2 = new(), sw3 = new();
        if (brAcc != null)
        {
            sw2.Start();
            byte[]? encodedMsg = Encoding.UTF8.GetBytes("BrAccViewer.BrAccSnapshot:" + Utils.CamelCaseSerialize(brAcc));
            sw2.Stop();
            if (WsWebSocket!.State == WebSocketState.Open)
            {
                sw3.Start();
                await WsWebSocket.SendAsync(new ArraySegment<Byte>(encodedMsg, 0, encodedMsg.Length), WebSocketMessageType.Text, true, CancellationToken.None);
                sw3.Stop();
            }
        }
        sw.Stop();
        Utils.Logger.Info($"BrAccViewerSendSnapshot() ends in {sw.ElapsedMilliseconds}ms ({sw1.ElapsedMilliseconds}/{sw2.ElapsedMilliseconds}/{sw3.ElapsedMilliseconds}) SqTicker: '{m_braccSelectedNavAsset?.SqTicker ?? string.Empty}'");
    }
    private void BrAccViewerSendNavHist(SqDateOnly p_lookbackStart, SqDateOnly p_lookbackEndExcl, string p_bnchmrkTicker)
    {
        IEnumerable<AssetHistJs>? brAccViewerHist = GetBrAccViewerNavHist(p_lookbackStart, p_lookbackEndExcl, p_bnchmrkTicker);
        if (brAccViewerHist != null)
        {
            byte[]? encodedMsg = Encoding.UTF8.GetBytes("BrAccViewer.NavHist:" + Utils.CamelCaseSerialize(brAccViewerHist));
            if (WsWebSocket!.State == WebSocketState.Open)
                WsWebSocket.SendAsync(new ArraySegment<Byte>(encodedMsg, 0, encodedMsg.Length), WebSocketMessageType.Text, true, CancellationToken.None);
        }
    }

    private void BrAccViewerSendStockHist(SqDateOnly lookbackStart, SqDateOnly lookbackEndExcl, string sqTicker)
    {
        Asset? asset = MemDb.gMemDb.AssetsCache.TryGetAsset(sqTicker);
        if (asset == null)
            return;
        Stock? stock = asset as Stock;
        if (stock == null)
        {
            if (asset is Option option)
                stock = option.UnderlyingAsset as Stock;
        }
        if (stock == null)
            return;

        string yfTicker = stock.YfTicker;
        byte[]? encodedMsg = null;

        (SqDateOnly[] dates, float[] adjCloses) = MemDb.GetSelectedStockTickerHistData(lookbackStart, lookbackEndExcl, yfTicker);

        AssetHistValuesJs stockHistValues = new()
        {
            AssetId = AssetId32Bits.Invalid,
            SqTicker = sqTicker,
            PeriodStartDate = lookbackStart.Date,
            PeriodEndDate = lookbackEndExcl.Date.AddDays(-1),
        };
        if (adjCloses.Length != 0)
        {
            stockHistValues.HistDates = dates.Select(r => r.Date.ToYYYYMMDD()).ToList();
            stockHistValues.HistSdaCloses = adjCloses.ToList();
        }
        if (stockHistValues != null)
        {
            encodedMsg = Encoding.UTF8.GetBytes("BrAccViewer.StockHist:" + Utils.CamelCaseSerialize(stockHistValues));
            if (WsWebSocket!.State == WebSocketState.Open)
                WsWebSocket.SendAsync(new ArraySegment<Byte>(encodedMsg, 0, encodedMsg.Length), WebSocketMessageType.Text, true, CancellationToken.None);
        }
    }
    private HandshakeBrAccViewer GetHandshakeBrAccViewer(List<BrokerNav> p_selectableNavs)
    {
        List<AssetJs> marketBarAssets = m_brAccMktBrAssets.Select(r => new AssetJs() { AssetId = r.AssetId, SqTicker = r.SqTicker, Symbol = r.Symbol, Name = r.Name }).ToList();
        List<AssetJs> selectableNavAssets = p_selectableNavs.Select(r => new AssetJs() { AssetId = r.AssetId, SqTicker = r.SqTicker, Symbol = r.Symbol, Name = r.Name }).ToList();

        if (m_assetCategories.Count == 0)
        {
            m_assetCategories = GetAssetCategoriesFromGSheet();
            if (m_assetCategories.Count == 0)
            {
                m_assetCategories.Add(new() { Tag = "Food", SqTickers = new() { "S/WHEAT", "S/CORN", "S/COW" } });
                m_assetCategories.Add(new() { Tag = "Energy", SqTickers = new() { "S/XOM", "S/XLE", "S/XOP", "S/CRAK", "S/XES", "S/UNG", "S/USO" } });
            }
        }

        return new HandshakeBrAccViewer() { MarketBarAssets = marketBarAssets, SelectableNavAssets = selectableNavAssets, AssetCategories = m_assetCategories };
    }

    private BrAccViewerAccountSnapshotJs? GetBrAccViewerAccountSnapshot() // "N/GA.IM, N/DC, N/DC.IM, N/DC.IB"
    {
        if (m_braccSelectedNavAsset == null)
            return null;
        string navSqTicker = m_braccSelectedNavAsset.SqTicker;
        // if it is aggregated portfolio (DC Main + DeBlanzac), then a virtual combination is needed
        if (!GatewayExtensions.NavSqSymbol2GatewayIds.TryGetValue(navSqTicker, out List<GatewayId>? gatewayIds))
            return null;

        TsDateData<SqDateOnly, uint, float, uint> histData = MemDb.gMemDb.DailyHist.GetDataDirect();

        BrAccViewerAccountSnapshotJs? result = null;
        List<BrAccPos> unrecognizedAssets = new();
        foreach (GatewayId gwId in gatewayIds)  // AggregateNav has 2 Gateways
        {
            BrAccount? brAccount = MemDb.gMemDb.BrAccounts.FirstOrDefault(r => r.GatewayId == gwId);
            if (brAccount == null)
                return null;

            string gwIdStr = gwId.ToShortFriendlyString();
            if (result == null) // if this is the first gwID
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
            unrecognizedAssets.AddRange(brAccount.AccPossUnrecognizedAssets);
        }

        if (result != null)
        {
            if (unrecognizedAssets.Count > 0)
            {
                StringBuilder sb = new();
                var unrecognizedStocks = unrecognizedAssets.Where(r => r.Contract.SecType == "STK").Select(r => r.Contract.SecType + "-" + r.Contract.Symbol).ToList(); // if there is a Where(), ToList() is faster than ToArray()
                if (unrecognizedStocks.Count > 0)
                {
                    sb.Append($"Warning: Unrecognised stocks: (#{unrecognizedStocks.Count}): ");
                    unrecognizedStocks.ForEach(r => sb.Append(r + ","));
                }

                var unrecognizedNonStocks = unrecognizedAssets.Where(r => r.Contract.SecType != "STK").Select(r => r.Contract.SecType + "-" + r.Contract.Symbol).ToList();
                if (unrecognizedNonStocks.Count > 0)
                {
                    if (unrecognizedStocks.Count > 0)   // if there is a previous warning in StringBuilder. (Note: we don't want to use StringBuilder.Length because that will collapse the StringBuilder, and there is no IsEmpty method)
                        sb.Append(';'); // separate many Warnings by ";"
                    sb.Append($"Warning: Unrecognised non-stocks: (#{unrecognizedNonStocks.Count}): ");
                    unrecognizedNonStocks.ForEach(r => sb.Append(r + ","));
                }
                result.ClientMsg += sb.ToString();
            }

            result.AssetId = m_braccSelectedNavAsset.AssetId;
            // Asset navAsset = MemDb.gMemDb.AssetsCache.AssetsBySqTicker[navSqTicker];    // realtime NavAsset.LastValue is more up-to-date then from BrAccount (updated 1h in RTH only)
            result.NetLiquidation = (long)MemDb.gMemDb.GetLastRtValue(m_braccSelectedNavAsset);

            DateTime todayET = Utils.ConvertTimeFromUtcToEt(DateTime.UtcNow).Date;
            List<AssetPriorClose> navPriorCloses = MemDb.gMemDb.GetSdaPriorClosesFromHist(new List<Asset>() { m_braccSelectedNavAsset }, todayET).ToList();
            result.PriorCloseNetLiquidation  = (long)navPriorCloses[0].SdaPriorClose;
        }
        return result;
    }

    private static List<BrAccViewerPosJs> GetBrAccViewerPos(List<BrAccPos> p_accPoss, string p_gwIdStr)
    {
        // One option is to only send those positions that have both valid AssetId (260 stocks) AND valid HistoricalPrice (only 20 stocks subset)
        // Because in general PriorClose is needed in the UI calculations.
        // But decided it is better to send these PriorClose=NaN rows as well. To show on the client that those historical prices are missing. Needs fixing in MemDb.Hist.
        List<BrAccPos> validBrPoss = p_accPoss.Where(r => r.AssetId != AssetId32Bits.Invalid).ToList();
        List<Asset> validBrPossAssets = validBrPoss.Select(r => (Asset)r.AssetObj!).ToList();
        // List<Asset> validBrPossAssets = validBrPoss.Select(r => MemDb.gMemDb.AssetsCache.AssetsByAssetID[r.AssetId]).ToList();

        // merge the 2 lists together: validBrPoss, validBrPossAssets
        List<BrAccViewerPosJs> result = new(validBrPoss.Count);
        for (int i = 0; i < validBrPoss.Count; i++)
        {
            BrAccPos posBr = validBrPoss[i];
            Asset asset = validBrPossAssets[i];
            float estUndValue = (asset as Option)?.UnderlyingAsset?.EstValue ?? float.NaN;
            double ibCompDelta = (asset  as Option)?.IbCompDelta ?? double.NaN;
            result.Add(new BrAccViewerPosJs()
            {
                AssetId = posBr.AssetId,
                SqTicker = asset!.SqTicker,
                Symbol = asset.Symbol,
                SymbolEx = asset.SymbolEx,
                Name = asset.Name,
                Pos = posBr.Position,
                AvgCost = posBr.AvgCost,
                PriorClose = asset.PriorClose,  // can be NaN if not given by IB. Sending "priorClose":"NaN". Client should be able to handle it. IB UI shows empty cell. Otherwise, we create fake data.
                EstPrice = asset.EstValue,  // can be NaN if not given by IB. Sending "estPrice":"NaN". Client should handle it. IB UI shows empty cell. Otherwise, we create fake data.
                EstUndPrice = (float.IsNaN(estUndValue)) ? 0.0f : estUndValue,
                IbCompDelta = (double.IsNaN(ibCompDelta)) ? 0.0 : ibCompDelta,
                AccId = p_gwIdStr
            });
        }
        return result;
    }

    private IEnumerable<AssetHistJs>? GetBrAccViewerNavHist(SqDateOnly lookbackStart, SqDateOnly lookbackEndExcl, string p_bnchmrkTicker)
    {
        if (m_braccSelectedNavAsset == null)
            return null;
        List<Asset> assets = new()
        {
            m_braccSelectedNavAsset,
            MemDb.gMemDb.AssetsCache.GetAsset(p_bnchmrkTicker) // add it to BrokerNav for benchmark for the chart
        };

        IEnumerable<AssetHist> assetHists = MemDb.gMemDb.GetSdaHistCloses(assets, lookbackStart, lookbackEndExcl, true, true);

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

    public bool OnReceiveWsAsync_BrAccViewer(string msgCode, string msgObjStr)
    {
        switch (msgCode)
        {
            case "BrAccViewer.ChangeNav": // msg: "DC.IM,Bnchmrk:SPY,Date:2021-01-02...2021-12-12"
                Utils.Logger.Info($"OnReceiveWsAsync_BrAccViewer(): changeNav to '{msgObjStr}'"); // DC.IM

                int navAssetEndIdx = msgObjStr.IndexOf(",");
                if (navAssetEndIdx == -1)
                    return true;    // processed, but there was a problem
                string sqTicker = string.Concat("N/", msgObjStr.AsSpan(0, navAssetEndIdx)); // turn DC.IM to N/DC.IM
                m_braccSelectedNavAsset = MemDb.gMemDb.AssetsCache.GetAsset(sqTicker) as BrokerNav;

                (SqDateOnly lookbackStart, SqDateOnly lookbackEndExcl, string braccSelectedBnchmkSqTicker) = ExtractHistDataParams(msgObjStr, navAssetEndIdx + 1);
                BrAccViewerSendSnapshotAndHist(lookbackStart, lookbackEndExcl, braccSelectedBnchmkSqTicker);
                return true;
            case "BrAccViewer.RefreshSnapshot":
                BrAccViewerUpdateStOptPricesAndSendSnapshotTwice();
                return true;
            case "BrAccViewer.RefreshMktBrPriorCloses":
                BrAccViewerSendMarketBarPriorCloses();
                return true;
            case "BrAccViewer.GetNavChrtData": // msg: "Bnchmrk:SPY,Date:2021-01-02...2021-12-12"
                Utils.Logger.Info($"OnReceiveWsAsync_BrAccViewer(): GetNavChrtData to '{msgObjStr}'");
                (SqDateOnly lookbackStartH, SqDateOnly lookbackEndExclH, string braccSelectedBnchmkSqTickerH) = ExtractHistDataParams(msgObjStr, 0);
                BrAccViewerSendNavHist(lookbackStartH, lookbackEndExclH, braccSelectedBnchmkSqTickerH);
                return true;
            case "BrAccViewer.GetStockChrtData":
                Utils.Logger.Info($"OnReceiveWsAsync_BrAccViewer(): GetStockChrtData to '{msgObjStr}'");
                string stockSqTicker = msgObjStr;
                DateTime todayET = Utils.ConvertTimeFromUtcToEt(DateTime.UtcNow).Date;
                SqDateOnly stckChrtLookbackStart = new(todayET.Year - 1, todayET.Month, todayET.Day);  // gets the 1 year data starting from yesterday to back 1 year
                SqDateOnly stckChrtLookbackEndExcl = todayET;
                BrAccViewerSendStockHist(stckChrtLookbackStart, stckChrtLookbackEndExcl, stockSqTicker);
                return true;
            default:
                return false;
        }
    }

    static (SqDateOnly lookbackStart, SqDateOnly lookbackEndExcl, string braccSelectedBnchmkSqTicker) ExtractHistDataParams(string p_msg, int startIdx) // msg: "Bnchmrk:SPY,Date:2021-01-02...2021-12-12"
    {
        int bnchmkStartIdx = p_msg.IndexOf(":", startIdx);
        int periodStartIdx = (bnchmkStartIdx == -1) ? -1 : p_msg.IndexOf(",", bnchmkStartIdx);
        string braccSelectedBnchmkSqTicker = (bnchmkStartIdx == -1 || periodStartIdx == -1) ? "S/SPY" : string.Concat("S/", p_msg.AsSpan(bnchmkStartIdx + 1, periodStartIdx - bnchmkStartIdx - 1));
        
        SqDateOnly lookbackStart, lookbackEndExcl;
        if (periodStartIdx == -1)
        {
            DateTime todayET = Utils.ConvertTimeFromUtcToEt(DateTime.UtcNow).Date;  // the default is YTD. Leave it as it is used frequently: by default server sends this to client at Open. Or at EvMemDbHistoricalDataReloaded_mktHealth()
            lookbackStart = new(todayET.Year - 1, 12, 31);  // YTD relative to 31st December, last year
            lookbackEndExcl = todayET;
        }
        else
        {
            string periodSelected = p_msg[(periodStartIdx + 1)..];
            lookbackStart = Utils.FastParseYYYYMMDD(new StringSegment(periodSelected, "Date:".Length, 10));
            DateTime lookbackEndIncl = Utils.FastParseYYYYMMDD(new StringSegment(periodSelected, "Date:".Length + 13, 10)); // the web UI is written that 'EndDate' is yesterday, which should be included in returned data (if it is not a weekend or holiday, so complicated).
            lookbackEndExcl = lookbackEndIncl.AddDays(1);   // convert it to excludedEndDate, which converts it to Today. Because that is what MemDb_helper.cs/GetSdaHistCloses() expects
        }

        return (lookbackStart, lookbackEndExcl, braccSelectedBnchmkSqTicker);
    }

    // On the SqCore Linux server, the London user browser RefreshSnapshot-latency: only 380ms. That includes getting message at server + 2 IbUpdateBrAccount() for DC.IM, DC.DB, +  RT price from YF for 120 stocks + And sending back data to client. Pretty fast with all the cleverness.
    private void BrAccViewerUpdateStOptPricesAndSendSnapshotTwice()
    {
        // Step 1: Force reload of poss from IB Gateways.
        if (m_braccSelectedNavAsset == null)
            return;
        string navSqTicker = m_braccSelectedNavAsset.SqTicker;
        // if it is aggregated portfolio (DC Main + DeBlanzac), then we have to force reload all sub-brAccounts
        if (!GatewayExtensions.NavSqSymbol2GatewayIds.TryGetValue(navSqTicker, out List<GatewayId>? gatewayIds))
            return;
        HashSet<Asset> validBrPossAssets = new(ReferenceEqualityComparer.Instance);     // AggregatedNav can contain the same company stocks many times. We need it only once. Force ReferenceEquality, even if the Asset class later implements deep IEquality
        foreach (GatewayId gwId in gatewayIds)  // AggregateNav has 2 Gateways
        {
            BrAccount? brAccount = MemDb.gMemDb.BrAccounts.FirstOrDefault(r => r.GatewayId == gwId);
            if (brAccount == null)
                continue;
            MemDb.gMemDb.UpdateBrAccount_AddAssetsToMemData(brAccount, gwId);

            brAccount.AccPoss.Where(r => r.AssetObj != null).ForEach(r => validBrPossAssets.Add((Asset)r.AssetObj!));
        }

        // Step 2: update the RT prices of only those 30-120 stocks (150ms) that is in the IbPortfolio. Don't need to update all the 700 (later 2000) stocks in MemDb, that is done automatically by RtTimer in every 30min
        // validBrPossAssets is a mix of stocks, options, futures.
        MemDb.DownloadPriorCloseAndLastPriceYF(validBrPossAssets.Where(r => r.AssetId.AssetTypeID == AssetType.Stock).ToArray()).TurnAsyncToSyncTask();
        BrAccViewerSendSnapshot();  // Report to the user immediately after the YF returned the realtime stock prices. YF doesn't have RT option prices.

        // Step 3: update the RT prices of only those options (7-10sec) that is in the IbPortfolio. If there are no options in the portfolio then it takes only 0 sec.
        var validBrPossOptions = validBrPossAssets.Where(r => r.AssetId.AssetTypeID == AssetType.Option).ToArray();
        if (validBrPossOptions.Length == 0) // Dc DeBlan account doesn't have options. No need to run anything in a separate thread.
            return;
        // Run any long process (1+ sec) in separate than the WebSocket-processing thread. Otherwise any later message the client sends is queued on the server for seconds and not processed immediately. Resulting in UI unresponsiveness at the client.
        _ = Task.Run(() =>    // Task.Run() runs it immediately in a separate threod on the ThreadPool
        {
            MemDb.DownloadLastPriceOptionsIb(validBrPossOptions);    // can take 7-20 seconds, don't wait it. Report to the user earlier the stock price data.
            BrAccViewerSendSnapshot();  // Report to the user 6..16 seconds later again. With the updated option prices.
        }).LogUnobservedTaskExceptions("!Error in BrAccViewerUpdateStOptPricesAndSendSnapshotTwice() sub-thread.");
    }

//  Under Development...Daya
    static List<AssetCategoryJs> GetAssetCategoriesFromGSheet() {

        if (String.IsNullOrEmpty(Utils.Configuration["Google:GoogleApiKeyName"]) || String.IsNullOrEmpty(Utils.Configuration["Google:GoogleApiKeyKey"]))
            return new List<AssetCategoryJs>();

        string? valuesFromGSheetStr = Utils.DownloadStringWithRetryAsync("https://sheets.googleapis.com/v4/spreadsheets/1NP8Tg08MqSoqd6wXSCus0rLXYG4TGPejzsGIP8r9YOk/values/A1:Z2000?key=" + Utils.Configuration["Google:GoogleApiKeyKey"]).TurnAsyncToSyncTask();
        if (valuesFromGSheetStr == null)
             return new List<AssetCategoryJs>();

        Debug.WriteLine("The length of data from gSheet for AssetCategory is ", valuesFromGSheetStr.Length);

        string[] rows = valuesFromGSheetStr.Split(new string[] { "],\n" }, StringSplitOptions.RemoveEmptyEntries);
        List<AssetCategoryJs> result = new(rows.Length);
        List<AssetCategoryJs> nestedTags = new();
        for (int i = 0; i < rows.Length; i++)
        {
            string[] cells = rows[i].Split(new string[] { "\",\n" }, StringSplitOptions.RemoveEmptyEntries);
            if (cells.Length != 2)  // The lengths: first line: 3, Line with ony 1 comment cell: 1, empty lines: 1, only accept if it has 2 cells
                continue;
            string cellFirst = cells[0];
            int tagStartIdx = cellFirst.IndexOf('\"');
            if (tagStartIdx == -1)
                continue;
            string tag = cellFirst[(tagStartIdx + 1)..];
            string cellSecond = cells[1];
            int tickersStartIdx = cellSecond.IndexOf('\"');
            int tickersEndIdx = (tickersStartIdx == -1) ? -1 : cellSecond.IndexOf('\"', tickersStartIdx + 1);
            if (tickersEndIdx == -1)
                continue;
            string cellSecondStr = cellSecond.Substring(tickersStartIdx + 1, tickersEndIdx - tickersStartIdx - 1);
            string[] cellSecondStrArr = cellSecondStr.Split(',');
            if (cellSecondStr.StartsWith("Tag:", StringComparison.InvariantCultureIgnoreCase))
            {
                nestedTags.Add(new AssetCategoryJs() { Tag = tag, SqTickers = cellSecondStrArr.ToList() });
                continue;
            }
            List<string> sqTickers = cellSecondStrArr.Select(r => string.Concat("S/", r.Trim())).ToList();
            result.Add(new AssetCategoryJs() { Tag = tag, SqTickers = sqTickers });
        }

        // need another loop for nested tags and find them in the 'result'
        // Step2: when you add new sqTickers to the list, don't add if it is already in the list
        for (int i = 0; i < nestedTags.Count; i++)
        {
            List<string> allSqTickers = new();
            for (int j = 0; j < nestedTags[i].SqTickers.Count; j++)
            {
                int nestedTagStartIndx = nestedTags[i].SqTickers[j].IndexOf(':');
                if (nestedTagStartIndx == -1)
                    continue;
                string nestedTagStr = nestedTags[i].SqTickers[j][(nestedTagStartIndx + 1)..];

                // find nestedTagStr in results
                int nestedTickersIdx = result.FindIndex(r => r.Tag == nestedTagStr);
                if (nestedTickersIdx == -1)
                    continue;
                // add them one by one to allSqTickers
                foreach (var sqTicker in result[nestedTickersIdx].SqTickers)
                {
                    if (!allSqTickers.Contains(sqTicker))
                        allSqTickers.Add(sqTicker);
                }
            }

            result.Add(new AssetCategoryJs() { Tag = nestedTags[i].Tag, SqTickers = allSqTickers });
        }
        return result;
    }
}