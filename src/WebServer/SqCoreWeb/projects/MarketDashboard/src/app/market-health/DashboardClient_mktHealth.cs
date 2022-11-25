using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using Fin.MemDb;
using Microsoft.Extensions.Primitives;
using SqCommon;

namespace SqCoreWeb;

class HandshakeMktHealth
{ // Initial params
    public List<AssetJs> MarketSummaryAssets { get; set; } = new List<AssetJs>();
    public List<AssetJs> SelectableNavAssets { get; set; } = new List<AssetJs>();
}

// The knowledge 'WHEN to send what' should be programmed on the server. When server senses that there is an update, then it broadcast to clients.
// Do not implement the 'intelligence' of WHEN to change data on the client. It can be too complicated, like knowing if there was a holiday, a half-trading day, etc.
// Clients should be slim programmed. They should only care, that IF they receive a new data, then Refresh.
public partial class DashboardClient
{
    public static readonly TimeSpan c_initialSleepIfNotActiveToolMh = TimeSpan.FromMilliseconds(5000);

    // try to convert to use these fields. At least on the server side.
    // If we store asset pointers (Stock, Nav) if the MemDb reloads, we should reload these pointers from the new MemDb. That adds extra code complexity.
    // However, for fast execution, it is still better to keep asset pointers, instead of keeping the asset's SqTicker and always find them again and again in MemDb.
    readonly List<string> c_marketSummarySqTickersDefault = new() { "S/QQQ", "S/SPY", "S/GLD", "S/TLT", "S/VXX", "S/UNG", "S/USO" };
    readonly List<string> c_marketSummarySqTickersDc = new() { "S/QQQ", "S/SPY", "S/GLD", "S/TLT", "S/VXX", "S/UNG", "S/USO" };   // at the moment DC uses the same as default
    string m_lastLookbackPeriodStrMh = "YTD";
    List<Asset> m_mkthAssets = new();      // remember, so we can send RT data
    BrokerNav? m_mkthSelectedNavAsset = null;   // remember which NAV is selected, so we can send RT data

    // void EvMemDbAssetDataReloaded_MktHealth()
    // {
    //     // have to refresh Asset pointers in memory, such as m_marketSummaryAssets, m_mkthSelectedNavAsset
    //     // have to resend the HandShake message Asset Id to SqTicker associations. Have to resend everything.
    // }

    void EvMemDbHistoricalDataReloaded_MktHealth()
    {
        Utils.Logger.Info("EvMemDbHistoricalDataReloaded_mktHealth() START");

        IEnumerable<AssetHistStatJs> periodStatToClient = GetLookbackStat(m_lastLookbackPeriodStrMh);     // reset lookback to to YTD. Because of BrokerNAV, lookback period stat is user specific.
        Utils.Logger.Info("EvMemDbHistoricalDataReloaded_mktHealth(). Processing client:" + UserEmail);
        byte[] encodedMsg = Encoding.UTF8.GetBytes("MktHlth.NonRtStat:" + Utils.CamelCaseSerialize(periodStatToClient));
        if (WsWebSocket == null)
            Utils.Logger.Info("Warning (TODO)!: Mystery how client.WsWebSocket can be null? Investigate!) ");
        if (WsWebSocket!.State == WebSocketState.Open)
            WsWebSocket.SendAsync(new ArraySegment<Byte>(encodedMsg, 0, encodedMsg.Length), WebSocketMessageType.Text, true, CancellationToken.None);    // takes 0.635ms
    }

    // Return from this function very quickly. Do not call any Clients.Caller.SendAsync(), because client will not notice that connection is Connected, and therefore cannot send extra messages until we return here
    public void OnConnectedWsAsync_MktHealth(bool p_isThisActiveToolAtConnectionInit, ManualResetEvent p_whRtPriceAssetsDetermined)
    {
        Utils.RunInNewThread(ignored => // running parallel on a ThreadPool thread, FireAndForget: QueueUserWorkItem [26microsec] is 25% faster than Task.Run [35microsec]
        {
            Thread.CurrentThread.IsBackground = true;  // thread will be killed when all foreground threads have died, the thread will not keep the application alive.

            List<BrokerNav> selectableNavs = User.GetAllVisibleBrokerNavsOrdered();
            m_mkthSelectedNavAsset = selectableNavs.FirstOrDefault();

            List<string> marketSummarySqTickers = (User.Username == "drcharmat") ? c_marketSummarySqTickersDc : c_marketSummarySqTickersDefault;
            m_mkthAssets = marketSummarySqTickers.Select(r => MemDb.gMemDb.AssetsCache.GetAsset(r)).ToList();

            HandshakeMktHealth handshake = GetHandshakeMktHlth(selectableNavs);
            byte[] encodedMsg = Encoding.UTF8.GetBytes("MktHlth.Handshake:" + Utils.CamelCaseSerialize(handshake));
            if (WsWebSocket!.State == WebSocketState.Open)
                WsWebSocket.SendAsync(new ArraySegment<Byte>(encodedMsg, 0, encodedMsg.Length), WebSocketMessageType.Text, true, CancellationToken.None);

            p_whRtPriceAssetsDetermined.Set();   // after m_mkthAssets is determined and handshake was sent to client, assume the client can handle if RtPrice arrives.

            // Assuming this tool is not the main Tab page on the client, we delay sending all the data, to avoid making the network and client too busy an unresponsive
            if (!p_isThisActiveToolAtConnectionInit)
                Thread.Sleep(c_initialSleepIfNotActiveToolMh);

            SendHistoricalWs();
        });
    }

    private void SendHistoricalWs()
    {
        IEnumerable<AssetHistStatJs> periodStatToClient = GetLookbackStat(m_lastLookbackPeriodStrMh);
        byte[] encodedMsg = Encoding.UTF8.GetBytes("MktHlth.NonRtStat:" + Utils.CamelCaseSerialize(periodStatToClient));
        if (WsWebSocket!.State == WebSocketState.Open)
            WsWebSocket.SendAsync(new ArraySegment<Byte>(encodedMsg, 0, encodedMsg.Length), WebSocketMessageType.Text, true, CancellationToken.None);    // takes 0.635ms
    }

    public bool OnReceiveWsAsync_MktHealth(string msgCode, string msgObjStr)
    {
        switch (msgCode)
        {
            case "MktHlth.ChangeLookback":
                Utils.Logger.Info("OnReceiveWsAsync_MktHealth(): changeLookback");
                m_lastLookbackPeriodStrMh = msgObjStr;
                SendHistoricalWs();
                return true;
            case "MktHlth.ChangeNav":
                Utils.Logger.Info($"OnReceiveWsAsync_MktHealth(): changeNav to '{msgObjStr}'"); // DC.IM
                string sqTicker = "N/" + msgObjStr; // turn DC.IM to N/DC.IM
                var navAsset = MemDb.gMemDb.AssetsCache.GetAsset(sqTicker);
                this.m_mkthSelectedNavAsset = navAsset as BrokerNav;

                SendHistoricalWs();
                SendRtStat();
                return true;
            default:
                return false;
        }
    }

    private IEnumerable<AssetHistStatJs> GetLookbackStat(string p_lookbackStr)
    {
        List<Asset> allAssets = m_mkthAssets.ToList();   // duplicate the asset pointers. Don't add navAsset to m_marketSummaryAssets
        if (m_mkthSelectedNavAsset != null)
            allAssets.Add(m_mkthSelectedNavAsset);

        DateTime todayET = Utils.ConvertTimeFromUtcToEt(DateTime.UtcNow).Date;  // the default is YTD. Leave it as it is used frequently: by default server sends this to client at Open. Or at EvMemDbHistoricalDataReloaded_mktHealth()
        SqDateOnly lookbackStartInc = new(todayET.Year - 1, 12, 31);  // YTD relative to 31st December, last year
        SqDateOnly lookbackEndExcl = todayET;
        if (p_lookbackStr.StartsWith("Date:")) // Browser client never send anything, but "Date:" inputs. Format: "Date:2019-11-11...2020-11-10"
        {
            lookbackStartInc = Utils.FastParseYYYYMMDD(new StringSegment(p_lookbackStr, "Date:".Length, 10));
            lookbackEndExcl = Utils.FastParseYYYYMMDD(new StringSegment(p_lookbackStr, "Date:".Length + 13, 10));
        }
        // else if (p_lookbackStr.EndsWith("y"))
        // {
        //     if (Int32.TryParse(p_lookbackStr.Substring(0, p_lookbackStr.Length - 1), out int nYears))
        //         lookbackStartInc = todayET.AddYears(-1 * nYears);
        // }
        // else if (p_lookbackStr.EndsWith("m"))
        // {
        //     if (Int32.TryParse(p_lookbackStr.Substring(0, p_lookbackStr.Length - 1), out int nMonths))
        //         lookbackStartInc = todayET.AddMonths(-1 * nMonths);
        // }
        // else if (p_lookbackStr.EndsWith("w"))
        // {
        //     if (Int32.TryParse(p_lookbackStr.Substring(0, p_lookbackStr.Length - 1), out int nWeeks))
        //         lookbackStartInc = todayET.AddDays(-7 * nWeeks);
        // }

        IEnumerable<AssetHist> assetHists = MemDb.gMemDb.GetSdaHistCloses(allAssets, lookbackStartInc, lookbackEndExcl, false, true);
        IEnumerable<AssetHistStatJs> lookbackStatToClient = assetHists.Select(r =>
        {
            var rtStock = new AssetHistStatJs()
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
            return rtStock;
        });
        return lookbackStatToClient;
    }

    private HandshakeMktHealth GetHandshakeMktHlth(List<BrokerNav> p_selectableNavs)
    {
        // string selectableNavs = "GA.IM, DC, DC.IM, DC.IB";
        List<AssetJs> marketSummaryAssets = m_mkthAssets.Select(r => new AssetJs() { AssetId = r.AssetId, SqTicker = r.SqTicker, Symbol = r.Symbol, Name = r.Name }).ToList();
        List<AssetJs> selectableNavAssets = p_selectableNavs.Select(r => new AssetJs() { AssetId = r.AssetId, SqTicker = r.SqTicker, Symbol = r.Symbol, Name = r.Name }).ToList();
        return new HandshakeMktHealth() { MarketSummaryAssets = marketSummaryAssets, SelectableNavAssets = selectableNavAssets };
    }
}