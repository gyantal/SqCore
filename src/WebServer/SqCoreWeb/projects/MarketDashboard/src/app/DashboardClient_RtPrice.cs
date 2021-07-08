using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json.Serialization;
using FinTechCommon;
using Microsoft.AspNetCore.Http;
using SqCommon;

// RT QQQ/SPY or NAV price should be sent only once the Dashboard. And both MktHealth, BrAccInfo, CatalystSniffer should use it. 
// Don't send RT data 2 or 3x to separate tools (it would slow down both C# code and JS). 
// That is a big rework to unify MktHealth/BrAcc both on the server and client side. 	On the server side, there should be only 1 RT timer object. 
// That should collect RT requirement of All tools. (created DashboardClient_RtPrice.cs)
// It should however prioritize. HighRtPriorityAssets list (QQQ,SPY,VXX) maybe sent in evere 5 seconds. MidPriority (GameChanger1): every 30 seconds. 
// LowPriority: everything else. (2 minutes or randomly 20 in every 1 minute. DC has 300 stocks, so those belong to that.)
namespace SqCoreWeb
{
    class AssetJs   // the class Asset converted to the the JS client. Usually it is sent to client tool in the Handshake msg. It can be used later for AssetId to Asset connection.
    {
        public uint AssetId { get; set; } = 0; // invalid value is best to be 0. If it is Uint32.MaxValue is the invalid, then problems if extending to Uint64
        public String SqTicker { get; set; } = string.Empty;    // used for unique identification. "N/DC" (NavAsset) is different to "S/DC" (StockAsset)
        public string Symbol { get; set; } = string.Empty;  // can be shown on the HTML UI
        public string Name { get; set; } = string.Empty;    // if the client has to show the name on the UI.
    }

    // sent SPY realtime price can be used in 3+2 places: BrAccViewer:MarketBar, HistoricalChart, UserAssetList, MktHlth, CatalystSniffer (so, don't send it 5 times. Client will decide what to do with RT price)
    // sent NAV realtime price can be used in 2 places: BrAccViewer.HistoricalChart, AccountSummary, MktHlth (if that is the viewed NAV)
    class AssetRtJs   // struct sent to browser clients every 2-4 seconds
    {
        public uint AssetId { get; set; } = 0;
        [JsonConverter(typeof(FloatJsonConverterToNumber4D))]
        public float Last { get; set; } = -100.0f;     // real-time last price
        public DateTime LastUtc { get; set; } = DateTime.MinValue;
    }

    public class AssetLastCloseJs   // this is sent to clients usually just once per day, OR when historical data changes
    {
        public uint AssetId { get; set; } = 0;        // set the Client know what is the assetId, because Rt will not send it.
        public DateTime Date { get; set; } = DateTime.MinValue;

        [JsonConverter(typeof(FloatJsonConverterToNumber4D))]
        public float LastClose { get; set; } = 0;   // Split Dividend Adjusted. Should be called SdaLastClose, but name goes to client, better to be short.
    }


    // Don't integrate this to BrAccViewerAccount. By default we sent YTD. But client might ask for last 10 years. 
    // But we don't want to send 10 years data and the today positions snapshot all the time together.
    public class AssetHistJs   // this is sent to clients usually just once per day, OR when historical data changes, OR when the Period changes at the client
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
    public class AssetHistStatJs   // this is sent to clients usually just once per day, OR when historical data changes, OR when the Period changes at the client
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

    public partial class DashboardClient {

        
    }

}
