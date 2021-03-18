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

namespace SqCoreWeb
{
    class HandshakeMktHealth {    //Initial params specific for the MarketHealth tool
        public String SelectableNavs { get; set; } = string.Empty;
    }
    class RtMktSummaryStock
    {
        public uint AssetId { get; set; } = 0; // invalid value is best to be 0. If it is Uint32.MaxValue is the invalid, then problems if extending to Uint64
        public String Ticker { get; set; } = string.Empty;
    }

    class RtMktSumRtStat   // struct sent to browser clients every 2-4 seconds
    {
        public uint AssetId { get; set; } = 0;
        [JsonConverter(typeof(DoubleJsonConverterToNumber4D))]
        public double Last { get; set; } = -100.0;     // real-time last price
        public DateTime LastUtc { get; set; } = DateTime.MinValue;
    }

    // When the user changes Period from YTD to 2y. It is a choice, but we will resend him the PeriodEnd data (and all data) again. Although it is not necessary. That way we only have one class, not 2.
    // When PeriodEnd (Date and Price) gradually changes (if user left browser open for a week), PeriodHigh, PeriodLow should be sent again (maybe we are at market high or low)
    public class RtMktSumNonRtStat   // this is sent to clients usually just once per day, OR when historical data changes, OR when the PeriodStartDate changes at the client
    {
        public uint AssetId { get; set; } = 0;        // set the Client know what is the assetId, because RtStat will not send it.
        public String Ticker { get; set; } = string.Empty;

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

    // The knowledge 'WHEN to send what' should be programmed on the server. When server senses that there is an update, then it broadcast to clients. 
    // Do not implement the 'intelligence' of WHEN to change data on the client. It can be too complicated, like knowing if there was a holiday, a half-trading day, etc. 
    // Clients should be slim programmed. They should only care, that IF they receive a new data, then Refresh.
    public partial class DashboardClient
    {
        // one global static real-time price Timer serves all clients. For efficiency.
        static Timer m_rtMktSummaryTimer = new System.Threading.Timer(new TimerCallback(RtMktSummaryTimer_Elapsed), null, TimeSpan.FromMilliseconds(-1.0), TimeSpan.FromMilliseconds(-1.0));
        static bool m_rtMktSummaryTimerRunning = false;
        static object m_rtMktSummaryTimerLock = new Object();

        static int m_rtMktSummaryTimerFrequencyMs = 3000;    // as a demo go with 3sec, later change it to 5sec, do decrease server load.

        // Alphabetical order is not required here, because it is searched in MemDb one by one, and that search is fast, because that is ordered alphabetically.
        // This is the order of appearance on the UI.
        // This should not be static, because it us user specific. BrNAV AssetID is user specific. Other assets might be later: User might later change the UNG ticker to sg. else.
        const string g_brNavVirtualTicker = "BrNAV";    // const is compile time determined. Faster that 'static readonly string'.
        List<RtMktSummaryStock> m_mktSummaryStocks = new List<RtMktSummaryStock>() { // this list can be market specific
            // DC.NAV is the aggregate of DC.IM.NAV + DC.ID.NAV. "BrNAV": It is good if there is a lowercase character in it, to show that it is different.
            //new RtMktSummaryStock() { Ticker = "GA.IM.NAV"},
            new RtMktSummaryStock() { Ticker = g_brNavVirtualTicker},    // BrNAV or any other ticker can be user specific. It can be different for every user. Don't even add it to global.
            new RtMktSummaryStock() { Ticker = "QQQ"},
            new RtMktSummaryStock() { Ticker = "SPY"},
            new RtMktSummaryStock() { Ticker = "GLD"},
            new RtMktSummaryStock() { Ticker = "TLT"},
            new RtMktSummaryStock() { Ticker = "VXX"},
            new RtMktSummaryStock() { Ticker = "UNG"},
            new RtMktSummaryStock() { Ticker = "USO"}};

        string m_lastLookbackPeriodStr = "YTD";

        void Ctor_mktHealth()
        {
            InitAssetData();
        }
        void InitAssetData()
        {
            // fill up AssetId based on Tickers. For faster access later.
            foreach (var stock in m_mktSummaryStocks)
            {
                string ticker = stock.Ticker;
                Asset? sec;
                if (stock.Ticker != g_brNavVirtualTicker)
                    sec = MemDb.gMemDb.AssetsCache.GetFirstMatchingAssetByLastTicker(ticker);
                else // Broker NAV
                    sec = GetSelectableNavsOrdered()[0];
                stock.AssetId = sec!.AssetId;
            }
        }

        void EvMemDbAssetDataReloaded_mktHealth()
        {
            InitAssetData();
        }


        void EvMemDbHistoricalDataReloaded_mktHealth()
        {
            Utils.Logger.Info("EvMemDbHistoricalDataReloaded_mktHealth() START");

            IEnumerable<RtMktSumNonRtStat> periodStatToClient = GetLookbackStat(m_lastLookbackPeriodStr);     // reset lookback to to YTD. Because of BrokerNAV, lookback period stat is user specific.
            Utils.Logger.Info("EvMemDbHistoricalDataReloaded_mktHealth(). Processing client:" + UserEmail);
            byte[] encodedMsg = Encoding.UTF8.GetBytes("RtMktSumNonRtStat:" + Utils.CamelCaseSerialize(periodStatToClient));
            if (WsWebSocket == null)
                Utils.Logger.Info("Warning (TODO)!: Mystery how client.WsWebSocket can be null? Investigate!) ");
            if (WsWebSocket!.State == WebSocketState.Open)
                WsWebSocket.SendAsync(new ArraySegment<Byte>(encodedMsg, 0, encodedMsg.Length), WebSocketMessageType.Text, true, CancellationToken.None);    //  takes 0.635ms
        }

        // Return from this function very quickly. Do not call any Clients.Caller.SendAsync(), because client will not notice that connection is Connected, and therefore cannot send extra messages until we return here
        public void OnConnectedWsAsync_MktHealth()
        {
            new Thread(() =>
            {
                Thread.CurrentThread.IsBackground = true;  //  thread will be killed when all foreground threads have died, the thread will not keep the application alive.

                HandshakeMktHealth handshakeMktHlth = GetHandshakeMktHlth();
                byte[] encodedMsg = Encoding.UTF8.GetBytes("HandshakeMktHlth:" + Utils.CamelCaseSerialize(handshakeMktHlth));
                if (WsWebSocket!.State == WebSocketState.Open)
                    WsWebSocket.SendAsync(new ArraySegment<Byte>(encodedMsg, 0, encodedMsg.Length), WebSocketMessageType.Text, true, CancellationToken.None);

                // for both the first and the second client, we get RT prices from MemDb immediately and send it back to this Client only.

                // 1. Send the Historical data first. SendAsync() is non-blocking. GetLastRtPrice() can be blocking
                SendHistoricalWs();
                // 2. Send RT price later, because GetLastRtPrice() might block the thread, if it is the first client.
                SendRealtimeWs();

                lock (m_rtMktSummaryTimerLock)
                {
                    if (!m_rtMktSummaryTimerRunning)
                    {
                        Utils.Logger.Info("OnConnectedAsync_MktHealth(). Starting m_rtMktSummaryTimer.");
                        m_rtMktSummaryTimerRunning = true;
                        m_rtMktSummaryTimer.Change(TimeSpan.FromMilliseconds(m_rtMktSummaryTimerFrequencyMs), TimeSpan.FromMilliseconds(-1.0));    // runs only once. To avoid that it runs parallel, if first one doesn't finish
                    }
                }
            }).Start();
        }

        private void SendHistoricalWs()
        {
            IEnumerable<RtMktSumNonRtStat> periodStatToClient = GetLookbackStat(m_lastLookbackPeriodStr);
            byte[] encodedMsg = Encoding.UTF8.GetBytes("RtMktSumNonRtStat:" + Utils.CamelCaseSerialize(periodStatToClient));
            if (WsWebSocket!.State == WebSocketState.Open)
                WsWebSocket.SendAsync(new ArraySegment<Byte>(encodedMsg, 0, encodedMsg.Length), WebSocketMessageType.Text, true, CancellationToken.None);    //  takes 0.635ms
        }

        private void SendRealtimeWs()
        {
            IEnumerable<RtMktSumRtStat> rtMktSummaryToClient = GetRtStat();
            byte[] encodedMsg = Encoding.UTF8.GetBytes("RtMktSumRtStat:" + Utils.CamelCaseSerialize(rtMktSummaryToClient));
            if (WsWebSocket!.State == WebSocketState.Open)
                WsWebSocket.SendAsync(new ArraySegment<Byte>(encodedMsg, 0, encodedMsg.Length), WebSocketMessageType.Text, true, CancellationToken.None);    //  takes 0.635ms
        }

        public bool OnReceiveWsAsync_MktHealth(WebSocketReceiveResult? wsResult, string msgCode, string msgObjStr)
        {
            switch (msgCode)
            {
                case "changeLookback":
                    Utils.Logger.Info("OnReceiveWsAsync_MktHealth(): changeLookback");
                    m_lastLookbackPeriodStr = msgObjStr;
                    SendHistoricalWs();
                    return true;
                case "changeNav":
                    Utils.Logger.Info($"OnReceiveWsAsync_MktHealth(): changeNav to '{msgObjStr}'");
                    var navAsset = MemDb.gMemDb.AssetsCache.GetFirstMatchingAssetByLastTicker(msgObjStr);
                    RtMktSummaryStock? navStock = m_mktSummaryStocks.FirstOrDefault(r => r.Ticker == g_brNavVirtualTicker);
                    if (navStock != null)
                        navStock.AssetId = navAsset!.AssetId;

                    SendHistoricalWs();
                    SendRealtimeWs();
                    return true;
                default:
                    return false;
            }
        }

        private IEnumerable<RtMktSumNonRtStat> GetLookbackStat(string p_lookbackStr)
        {
            DateTime todayET = Utils.ConvertTimeFromUtcToEt(DateTime.UtcNow).Date;  // the default is YTD. Leave it as it is used frequently: by default server sends this to client at Open. Or at EvMemDbHistoricalDataReloaded_mktHealth()
            DateOnly lookbackStart = new DateOnly(todayET.Year - 1, 12, 31);  // YTD relative to 31st December, last year
            DateOnly lookbackEnd = todayET.AddDays(-1);
            if (p_lookbackStr.StartsWith("Date:"))  // Browser client never send anything, but "Date:" inputs. Format: "Date:2019-11-11...2020-11-10"
            {
                lookbackStart = Utils.FastParseYYYYMMDD(new StringSegment(p_lookbackStr, "Date:".Length, 10));
                lookbackEnd = Utils.FastParseYYYYMMDD(new StringSegment(p_lookbackStr, "Date:".Length + 13, 10));
            }
            // else if (p_lookbackStr.EndsWith("y"))
            // {
            //     if (Int32.TryParse(p_lookbackStr.Substring(0, p_lookbackStr.Length - 1), out int nYears))
            //         lookbackStart = todayET.AddYears(-1 * nYears);
            // }
            // else if (p_lookbackStr.EndsWith("m"))
            // {
            //     if (Int32.TryParse(p_lookbackStr.Substring(0, p_lookbackStr.Length - 1), out int nMonths))
            //         lookbackStart = todayET.AddMonths(-1 * nMonths);
            // }
            // else if (p_lookbackStr.EndsWith("w"))
            // {
            //     if (Int32.TryParse(p_lookbackStr.Substring(0, p_lookbackStr.Length - 1), out int nWeeks))
            //         lookbackStart = todayET.AddDays(-7 * nWeeks);
            // }

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
            // int iEndDay = (dates[0] >= new DateOnly(Utils.ConvertTimeFromUtcToEt(DateTime.UtcNow))) ? 1 : 0;
            Debug.WriteLine($"EndDate: {dates[iEndDay]}");

            int iStartDay = histData.IndexOfKeyOrAfter(new DateOnly(lookbackStart));      // the valid price at the weekend is the one on the previous Friday. After.
            if (iStartDay == -1 || iStartDay >= dates.Length) // If not found then fix the startDate as the first available date of history.
            {
                iStartDay = dates.Length - 1;
            }
            Debug.WriteLine($"StartDate: {dates[iStartDay]}");


            IEnumerable<RtMktSumNonRtStat> lookbackStatToClient = m_mktSummaryStocks.Select(r =>
            {
                float[] sdaCloses = histData.Data[r.AssetId].Item1[TickType.SplitDivAdjClose];
                // if startDate is not found, because e.g. we want to go back 3 years, while stock has only 2 years history
                int iiStartDay = (iStartDay < sdaCloses.Length) ? iStartDay : sdaCloses.Length - 1;
                if (Single.IsNaN(sdaCloses[iiStartDay]) // if that date in the global MemDb was an USA stock market holiday (e.g. President days is on monday), price is NaN for stocks, but valid value for NAV
                    && ((iiStartDay + 1) <= sdaCloses.Length))
                    iiStartDay++;   // that start 1 day earlier. It is better to give back more data, then less. Besides on that holiday day, the previous day price is valid.

                // reverse marching from yesterday into past is not good, because we have to calculate running maxDD, maxDU.
                float max = float.MinValue, min = float.MaxValue, maxDD = float.MaxValue, maxDU = float.MinValue;
                int iStockEndDay = Int32.MinValue, iStockFirstDay = Int32.MinValue;
                for (int i = iiStartDay; i >= iEndDay; i--)   // iEndDay is index 0 or 1. Reverse marching from yesterday iEndDay to deeper into the past. Until startdate iStartDay or until history beginning reached
                {
                    if (Single.IsNaN(sdaCloses[i]))
                        continue;   // if that date in the global MemDb was an USA stock market holiday (e.g. President days is on monday), price is NaN for stocks, but valid value for NAV
                    if (iStockFirstDay == Int32.MinValue)
                        iStockFirstDay = i;
                    iStockEndDay = i;
                    if (sdaCloses[i] > max)
                        max = sdaCloses[i];
                    if (sdaCloses[i] < min)
                        min = sdaCloses[i];
                    float dailyDD = sdaCloses[i] / max - 1;     // -0.1 = -10%. daily Drawdown = how far from High = loss felt compared to Highest
                    if (dailyDD < maxDD)                        // dailyDD are a negative values, so we should do MIN-search to find the Maximum negative value
                        maxDD = dailyDD;                        // maxDD = maximum loss, pain felt over the period
                    float dailyDU = sdaCloses[i] / min - 1;     // daily DrawUp = how far from Low = profit felt compared to Lowest
                    if (dailyDU > maxDU)
                        maxDU = dailyDU;                        // maxDU = maximum profit, happiness felt over the period
                }

                // it is possible that both iStockFirstDay, iStockEndDay are left as Int32.MinValue, because there is no valid value at all in that range. Fine.
                var rtStock = new RtMktSumNonRtStat()
                {
                    AssetId = r.AssetId,
                    Ticker = r.Ticker, // DateTime.MaxValue: {9999-12-31 23:59:59}
                    PeriodStartDate = (iStockFirstDay >= 0) ? (DateTime)dates[iStockFirstDay] : DateTime.MaxValue,    // it may be not the 'asked' start date if asset has less price history
                    PeriodEndDate = (iStockEndDay >= 0) ? (DateTime)dates[iStockEndDay] : DateTime.MaxValue,        // by default it is the date of yesterday, but the user can change it
                    PeriodStart = (iStockFirstDay >= 0) ? sdaCloses[iStockFirstDay] : Double.NaN,
                    PeriodEnd = (iStockEndDay >= 0) ? sdaCloses[iStockEndDay] : Double.NaN,
                    PeriodHigh = (max == float.MinValue) ? float.NaN : max,
                    PeriodLow = (min == float.MaxValue) ? float.NaN : min,
                    PeriodMaxDD = (maxDD == float.MaxValue) ? float.NaN : maxDD,
                    PeriodMaxDU = (maxDU == float.MinValue) ? float.NaN : maxDU
                };
                return rtStock;
            });

            return lookbackStatToClient;
        }

        private IEnumerable<RtMktSumRtStat> GetRtStat()
        {
            var lastValues = MemDb.gMemDb.GetLastRtValueWithUtc(m_mktSummaryStocks.Select(r => r.AssetId).ToArray());
            return lastValues.Where(r => float.IsFinite(r.LastValue)).Select(r =>
            {
                var rtStock = new RtMktSumRtStat()
                {
                    AssetId = r.SecdID,
                    Last = r.LastValue,
                    LastUtc = r.LastValueUtc
                };
                return rtStock;
            });
        }

        private HandshakeMktHealth GetHandshakeMktHlth()
        {
            //string selectableNavs = "GA.IM.NAV, DC.NAV, DC.IM.NAV, DC.IB.NAV";
            List<Asset> selectableNavs = GetSelectableNavsOrdered();
            string selectableNavsCSV = String.Join(',', selectableNavs.Select(r => r.LastTicker));
            return new HandshakeMktHealth() { SelectableNavs = selectableNavsCSV };
        }

        List<Asset> GetSelectableNavsOrdered()
        {
            // SelectableNavs is an ordered list of tickers. The first item is user specific. User should be able to select between the NAVs. DB, Main, Aggregate.
            // bool isAdmin = UserEmail == Utils.Configuration["Emails:Gyant"].ToLower();
            // if (isAdmin) // Now, it is not used. Now, every Google email user with an email can see DC NAVs. Another option is that only Admin users (GA,BL,LN) can see the DC user NAVs.
            var user = MemDb.gMemDb.Users.FirstOrDefault(r => r.Email == UserEmail);
            List<Asset> selectableNavs = new List<Asset>();

            var userNavAssets = MemDb.gMemDb.AssetsCache.Assets.Where(r => r.User == user && r.AssetId.AssetTypeID == AssetType.BrokerNAV).ToArray();
            Asset? aggNavAsset = userNavAssets.FirstOrDefault(r => r.LastTicker == r.User!.Initials + ".NAV");
            if (aggNavAsset != null)
                selectableNavs.Add(aggNavAsset);
            foreach (var nav in userNavAssets)
            {
                if (nav != aggNavAsset)
                    selectableNavs.Add(nav);
            }

            var dcUser = MemDb.gMemDb.Users.FirstOrDefault(r => r.Email == Utils.Configuration["Emails:Charm0"].ToLower());
            if (user != dcUser) // if user is dcUser, then don't add NAVs twice
            {
                var dcUserNavAssets = MemDb.gMemDb.AssetsCache.Assets.Where(r => r.User == dcUser && r.AssetId.AssetTypeID == AssetType.BrokerNAV).ToArray();
                Asset? aggNavAssetDC = dcUserNavAssets.FirstOrDefault(r => r.LastTicker == r.User!.Initials + ".NAV");
                if (aggNavAssetDC != null)
                    selectableNavs.Add(aggNavAssetDC);
                foreach (var nav in dcUserNavAssets)
                {
                    if (nav != aggNavAssetDC)
                        selectableNavs.Add(nav);
                }
            }
            // Utils.Logger.Info($"GetSelectableNavsOrdered(): #{selectableNavs.Count}, ({String.Join(',', selectableNavs.Select(r => r.LastTicker))})");
            return selectableNavs;
        }

        public static void RtMktSummaryTimer_Elapsed(object? state)    // Timer is coming on a ThreadPool thread
        {
            try
            {
                Utils.Logger.Info("RtMktSummaryTimer_Elapsed(). BEGIN");
                if (!m_rtMktSummaryTimerRunning)
                    return; // if it was disabled by another thread in the meantime, we should not waste resources to execute this.

                DashboardClient.g_clients.ForEach(client =>
                {
                    IEnumerable<RtMktSumRtStat> rtMktSummaryToClient = client.GetRtStat();
                    byte[] encodedMsg = Encoding.UTF8.GetBytes("RtMktSumRtStat:" + Utils.CamelCaseSerialize(rtMktSummaryToClient));
                    if (client.WsWebSocket == null)
                        Utils.Logger.Info("Warning (TODO)!: Mystery how client.WsWebSocket can be null? Investigate!) ");
                    if (client.WsWebSocket != null && (client.WsWebSocket.State == WebSocketState.Open))
                        client.WsWebSocket.SendAsync(new ArraySegment<Byte>(encodedMsg, 0, encodedMsg.Length), WebSocketMessageType.Text, true, CancellationToken.None);    //  takes 0.635ms
                
                });

                lock (m_rtMktSummaryTimerLock)
                {
                    if (m_rtMktSummaryTimerRunning)
                    {
                        m_rtMktSummaryTimer.Change(TimeSpan.FromMilliseconds(m_rtMktSummaryTimerFrequencyMs), TimeSpan.FromMilliseconds(-1.0));    // runs only once. To avoid that it runs parallel, if first one doesn't finish
                    }
                }
                // Utils.Logger.Info("RtMktSummaryTimer_Elapsed(). END");
            }
            catch (Exception e)
            {
                Utils.Logger.Error(e, "RtMktSummaryTimer_Elapsed() exception.");
                throw;
            }
        }
    }
}