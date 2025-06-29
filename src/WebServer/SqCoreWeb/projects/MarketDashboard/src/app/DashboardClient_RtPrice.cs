using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading;
using Fin.MemDb;
using Microsoft.AspNetCore.Http;
using SqCommon;

// RT QQQ/SPY or NAV price should be sent only once the Dashboard. And both MktHealth, BrAccInfo, CatalystSniffer should use it.
// Don't send RT data 2 or 3x to separate tools (it would slow down both C# code and JS).
// That is a big rework to unify MktHealth/BrAcc both on the server and client side.    On the server side, there should be only 1 RT timer object.
// That should collect RT requirement of All tools. (created DashboardClient_RtPrice.cs)
// It should however prioritize. HighRtPriorityAssets list (QQQ,SPY,VXX) maybe sent in evere 5 seconds. MidPriority (GameChanger1): every 30 seconds.
// LowPriority: everything else. (2 minutes or randomly 20 in every 1 minute. DC has 300 stocks, so those belong to that.)
namespace SqCoreWeb;

class AssetJs // the class Asset converted to the the JS client. Usually it is sent to client tool in the Handshake msg. It can be used later for AssetId to Asset connection.
{
    public uint AssetId { get; set; } = 0; // invalid value is best to be 0. If it is Uint32.MaxValue is the invalid, then problems if extending to Uint64
    public string SqTicker { get; set; } = string.Empty;    // used for unique identification. "N/DC" (NavAsset) is different to "S/DC" (StockAsset)
    public string Symbol { get; set; } = string.Empty;  // can be shown on the HTML UI
    public string Name { get; set; } = string.Empty;    // if the client has to show the name on the UI.
}

// Don't call it RT=Realtime. If it is the weekend, we don't have RT price, but we have Last Known price that we have to send.
// Don't call it Price, because NAV or other time series (^VIX) has Values, not Price. So, LastValue is the best terminology.
class AssetLastJs // struct sent to browser clients every 2-4 seconds
{
    // property names and values are transformed to a shorter ones for decreasing internet traffic. 700bytes => 395bytes (-45%) per 5sec. Per hour: 500KB => 280KB
    [JsonPropertyName("id")]
    public uint AssetId { get; set; } = 0;

    [JsonPropertyName("t")]
    [JsonConverter(typeof(DateTimeJsonConverterToUnixEpochSeconds))]
    public DateTime LastUtc { get; set; } = DateTime.MinValue;

    [JsonPropertyName("l")]
    [JsonConverter(typeof(FloatJsonConverterToNumber4D))]
    public float Last { get; set; } = -100.0f;     // real-time last price
}

public class AssetPriorCloseJs // this is sent to clients usually just once per day, OR when historical data changes
{
    public uint AssetId { get; set; } = 0;        // set the Client know what is the assetId, because Rt will not send it.
    public DateTime Date { get; set; } = DateTime.MinValue;

    [JsonConverter(typeof(FloatJsonConverterToNumber4D))]
    public float PriorClose { get; set; } = 0;   // Split Dividend Adjusted. Should be called SdaPriorClose, but name goes to client, better to be short.
}

public class AssetHistJs // duplicate that the AssetId is in both HistValues and HistStat, but sometimes client needs only values (a QuickTester), sometimes only stats
{
        public AssetHistValuesJs? HistValues { get; set; } = null;
        public AssetHistStatJs? HistStat { get; set; } = null;
}

// Don't integrate this to BrAccViewerAccount. By default we sent YTD. But client might ask for last 10 years.
// But we don't want to send 10 years data and the today positions snapshot all the time together.
public class AssetHistValuesJs // this is sent to clients usually just once per day, OR when historical data changes, OR when the Period changes at the client
{
    public uint AssetId { get; set; } = 0;        // set the Client know what is the assetId, because Rt will not send it.
    public String SqTicker { get; set; } = string.Empty;  // Not necessary to send as AssetJs contains the SqTicker, but we send it for Debug purposes

    public DateTime PeriodStartDate { get; set; } = DateTime.MinValue;
    public DateTime PeriodEndDate { get; set; } = DateTime.MinValue;

    public List<string> HistDates { get; set; } = new List<string>();   // we convert manually DateOnly to short string
    public List<float> HistSdaCloses { get; set; } = new List<float>(); // float takes too much data, but
}

// When the user changes Period from YTD to 2y. It is a choice, but we will resend him the PeriodEnd data (and all data) again. Although it is not necessary. That way we only have one class, not 2.
// When PeriodEnd (Date and Price) gradually changes (if user left browser open for a week), PeriodHigh, PeriodLow should be sent again (maybe we are at market high or low)
public class AssetHistStatJs // this is sent to clients usually just once per day, OR when historical data changes, OR when the Period changes at the client
{
    public uint AssetId { get; set; } = 0;        // set the Client know what is the assetId, because RtStat will not send it.
    public String SqTicker { get; set; } = string.Empty; // Not necessary to send as AssetJs contains the SqTicker, but we send it for Debug purposes

    public DateTime PeriodStartDate { get; set; } = DateTime.MinValue;
    public DateTime PeriodEndDate { get; set; } = DateTime.MinValue;

    [JsonConverter(typeof(DoubleJsonConverterToNumber4D))]
    public double PeriodStart { get; set; } = -100.0;
    [JsonConverter(typeof(DoubleJsonConverterToNumber4D))]
    public double PeriodEnd { get; set; } = -100.0;

    [JsonConverter(typeof(DoubleJsonConverterToNumber4D))]
    public double PeriodHigh { get; set; } = -100.0;
    [JsonConverter(typeof(DoubleJsonConverterToNumber4D))]
    public double PeriodLow { get; set; } = -100.0;
    [JsonConverter(typeof(DoubleJsonConverterToNumber4D))]
    public double PeriodMaxDD { get; set; } = -100.0;
    [JsonConverter(typeof(DoubleJsonConverterToNumber4D))]
    public double PeriodMaxDU { get; set; } = -100.0;
}

public partial class DashboardClient
{
    // one global static real-time price Timer serves all clients. For efficiency.
    static readonly Timer m_rtDashboardTimer = new(new TimerCallback(RtDashboardTimer_Elapsed), null, TimeSpan.FromMilliseconds(-1.0), TimeSpan.FromMilliseconds(-1.0));
    static readonly object m_rtDashboardTimerLock = new();
    static readonly int m_rtDashboardTimerFrequencyMs = 6 * 1000;    // similar to the m_highFreqParam in MemDb_RT.
    static bool m_rtDashboardTimerRunning = false;

    public void OnConnectedWsAsync_Rt()
    {
        // Send RT price (after Tools determine their Rt Assets)
        SendRtStat();

        lock (m_rtDashboardTimerLock)
        {
            if (!m_rtDashboardTimerRunning)
            {
                Utils.Logger.Info("OnConnectedAsync_MktHealth(). Starting m_rtDashboardTimer.");
                m_rtDashboardTimerRunning = true;
                m_rtDashboardTimer.Change(TimeSpan.FromMilliseconds(m_rtDashboardTimerFrequencyMs), TimeSpan.FromMilliseconds(-1.0));    // runs only once. To avoid that it runs parallel, if first one doesn't finish
            }
        }
    }
    public static void RtDashboardTimer_Elapsed(object? state) // Timer is coming on a ThreadPool thread
    {
        try
        {
            // Utils.Logger.Info("RtDashboardTimer_Elapsed(). BEGIN");  // too frequent to log it out in every 5 seconds. Just use it when debugging is needed.
            if (!m_rtDashboardTimerRunning)
                return; // if it was disabled by another thread in the meantime, we should not waste resources to execute this.

            List<DashboardClient> g_clientsPtrCpy = DashboardClient.g_clients; // Copy the pointer for reading. Just in case a Writer overwrites the pointer while we use that pointer for a long time (for a loop or if we use it many times). Multithread warning! Lockfree Read | Copy-Modify-Swap Write Pattern
            foreach (DashboardClient client in g_clientsPtrCpy) // RT timer should be fast, don't use LINQ.ForEach(), but use foreach()
            {
                // To free up resources, send data only if active tool really uses RT prices. HealthMonitor and BrAccViewer
                if (!c_activePagesUsingRtPrices.Contains(client.ActivePage))
                    break;
                // Also don't send RT prices too early, only if some seconds has been passed. OnConnectedWsAsync() sleeps for a while if not active tool.
                TimeSpan timeSinceConnect = DateTime.UtcNow - client.ConnectionTime;
                if (timeSinceConnect < c_initialSleepIfNotActiveToolMh.Add(TimeSpan.FromMilliseconds(100)))
                    break;

                client.SendRtStat();
            }

            lock (m_rtDashboardTimerLock)
            {
                if (m_rtDashboardTimerRunning)
                    m_rtDashboardTimer.Change(TimeSpan.FromMilliseconds(m_rtDashboardTimerFrequencyMs), TimeSpan.FromMilliseconds(-1.0));    // runs only once. To avoid that it runs parallel, if first one doesn't finish
            }
            // Utils.Logger.Info("RtDashboardTimer_Elapsed(). END");
        }
        catch (Exception e)
        {
            Utils.Logger.Error(e, "RtDashboardTimer_Elapsed() exception.");
            throw;
        }
    }

    private void SendRtStat()
    {
        IEnumerable<AssetLastJs> rtDataToClient = GetHighPriorityRtStat();

        byte[] encodedMsg = Encoding.UTF8.GetBytes("All.LstVal:" + Utils.CamelCaseSerialize(rtDataToClient));
        if (WsWebSocket == null)
            Utils.Logger.Info("Warning (TODO)!: Mystery how client.WsWebSocket can be null? Investigate!) ");
        if (WsWebSocket != null && WsWebSocket!.State == WebSocketState.Open)
            WsWebSocket.SendAsync(new ArraySegment<Byte>(encodedMsg, 0, encodedMsg.Length), WebSocketMessageType.Text, true, CancellationToken.None);    // takes 0.635ms
    }

    private IEnumerable<AssetLastJs> GetHighPriorityRtStat()
    {
        // sent SPY realtime price can be used in 3+2 places: BrAccViewer:MarketBar, HistoricalChart, UserAssetList, MktHlth, CatalystSniffer (so, don't send it 5 times. Client will decide what to do with RT price)
        // sent NAV realtime price can be used in 3 places: BrAccViewer.HistoricalChart, AccountSummary, MktHlth (if that is the viewed NAV)

        // The RT asset list is user (connection) specific. Combination of the Tools assets. MarketHealh's m_mkthAssets and BrAccViewer's m_brAccMktBrAssets should be combined. The Tools calculate these at OnConnection()
        List<Asset> highPriorityAssets = new(m_mkthAssets);
        foreach (Asset mktBrAsset in m_brAccMktBrAssets)
        {
            if (!highPriorityAssets.Contains(mktBrAsset))
                highPriorityAssets.Add(mktBrAsset);
        }

        if (m_mkthSelectedNavAsset != null)
            highPriorityAssets.Add(m_mkthSelectedNavAsset);
        if (m_braccSelectedNavAsset != null && !highPriorityAssets.Contains(m_braccSelectedNavAsset))
            highPriorityAssets.Add(m_braccSelectedNavAsset);

        var lastValues = MemDb.gMemDb.GetLastRtValueWithUtc(highPriorityAssets); // GetLastRtValue() is non-blocking, returns immediately (maybe with NaN values)
        return lastValues.Where(r => float.IsFinite(r.LastValue)).Select(r => // there is no point of sending if LastValue is NaN
        {
            var rtStock = new AssetLastJs()
            {
                AssetId = r.SecdID,
                Last = r.LastValue,
                LastUtc = r.LastValueUtc
            };
            return rtStock;
        });
    }

    // TODO: <when BrAcc Snapshot needs RT data for positions> Realtime price sending should be prioritized.
    // HighRtPriorityAssets list (QQQ,SPY,VXX) maybe sent in evere 5 seconds. MarketHealth.MarketSummary + BrAccViewer.MktBar
    // MidPriority: every 30 seconds. GameChanger1s in BrAccViewer.SnapshotPos
    // LowPriority: everything else (BrAccViewer.SnapshotPos). (2 minutes or randomly 20 in every 1 minute. DC has 300 stocks, so those belong to that.)
    // private IEnumerable<AssetLastJs> GetMidPriorityRtStat()
    // {
    //     throw new NotImplementedException();
    // }
}