using Microsoft.AspNetCore.SignalR;
using System;
using System.Threading;
using System.Threading.Tasks;
using SqCommon;
using System.Linq;
using System.Collections.Generic;
using Microsoft.Extensions.Hosting;
using System.Text;
using System.Net;
using System.Diagnostics;
using FinTechCommon;
using System.Runtime.Serialization;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Net.WebSockets;

namespace SqCoreWeb
{
    class RtMktSummaryStock
    {
        public uint AssetId { get; set; } = 0; // invalid value is best to be 0. If it is Uint32.MaxValue is the invalid, then problems if extending to Uint64
        public String Ticker { get; set; } = String.Empty;
    }

    class RtMktSumRtStat   // struct sent to browser clients every 2-4 seconds
    {
        public uint AssetId { get; set; } = 0;
        public double Last { get; set; } = -100.0;     // real-time last price
    }

    public class RtMktSumNonRtStat   // this is sent to clients usually just once per day, OR when the PeriodStartDate changes at the client
    {
        public uint AssetId { get; set; } = 0;        // set the Client know what is the assetId, because RtStat will not send it.
        public String Ticker { get; set; } = String.Empty;

        // when previousClose gradually changes (if user left browser open for a week), PeriodHigh, PeriodLow should be sent again (maybe we are at market high or low)
        // sometimes, the user changed Period from YTD to 2y. It is a choice, we will resend him the PreviousClose data again. Although it is not necessary. That way we only one class, not 2.
        [JsonConverter(typeof(DoubleJsonConverter))]
        public double PreviousClose { get; set; } = -100.0;
        public DateTime PeriodStart { get; set; } = DateTime.MinValue;
        public double PeriodOpen { get; set; } = -100.0;
        public double PeriodHigh { get; set; } = -100.0;
        public double PeriodLow { get; set; } = -100.0;
        public double PeriodMaxDD { get; set; } = -100.0;
        public double PeriodMaxDU { get; set; } = -100.0;
    }

    public class DoubleJsonConverter : JsonConverter<double>
    {
        public override double Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
                Double.Parse(reader.GetString());

        public override void Write(Utf8JsonWriter writer, double doubleValue, JsonSerializerOptions options) =>
                writer.WriteStringValue(doubleValue.ToString());
    }


    // The knowledge 'WHEN to send what' should be programmed on the server. When server senses that there is an update, then it broadcast to clients. 
    // Do not implement the 'intelligence' of WHEN to change data on the client. It can be too complicated, like knowing if there was a holiday, a half-trading day, etc. 
    // Clients should be slim programmed. They should only care, that IF they receive a new data, then Refresh.
    public partial class DashboardClient
    {
        static Timer m_rtMktSummaryTimer = new System.Threading.Timer(new TimerCallback(RtMktSummaryTimer_Elapsed), null, TimeSpan.FromMilliseconds(-1.0), TimeSpan.FromMilliseconds(-1.0));
        static bool m_rtMktSummaryTimerRunning = false;
        static object m_rtMktSummaryTimerLock = new Object();

        static int m_rtMktSummaryTimerFrequencyMs = 3000;    // as a demo go with 3sec, later change it to 5sec, do decrease server load.

        // alphabetical order is not required here, because it is searched in MemDb one by one, and that search is fast, because that is ordered alphabetically.
        // this is the order of appearance on the UI.
        static List<RtMktSummaryStock> g_mktSummaryStocks = new List<RtMktSummaryStock>() {
            new RtMktSummaryStock() { Ticker = "QQQ"},
            new RtMktSummaryStock() { Ticker = "SPY"},
            new RtMktSummaryStock() { Ticker = "GLD"},
            new RtMktSummaryStock() { Ticker = "TLT"},
            new RtMktSummaryStock() { Ticker = "VXX"},
            new RtMktSummaryStock() { Ticker = "UNG"},
            new RtMktSummaryStock() { Ticker = "USO"}};
        static DateTime g_rtMktSummaryPreviousClosePrChecked = DateTime.MinValue; // UTC

        static void EvMemDbInitialized_mktHealth()
        {
            // fill up AssetId based on Tickers. For faster access later.
            foreach (var stock in g_mktSummaryStocks)
            {
                Asset sec = MemDb.gMemDb.GetFirstMatchingAssetByLastTicker(stock.Ticker);
                stock.AssetId = sec.AssetId;
            }
        }


        static void EvMemDbHistoricalDataReloaded_mktHealth()
        {
            Utils.Logger.Info("EvMemDbHistoricalDataReloaded_mktHealth() START");

            if (DashboardClient.g_clients.Count > 0)    // Notify all the connected users. 
            {
                IEnumerable<RtMktSumNonRtStat> periodStatToClient = GetLookbackStat("YTD");     // reset lookback to to YTD.
                DashboardPushHubKestrelBckgrndSrv.HubContext?.Clients.All.SendAsync("RtMktSumNonRtStat", periodStatToClient);

                byte[] encodedMsg = Encoding.UTF8.GetBytes("RtMktSumNonRtStat:" + Utils.CamelCaseSerialize(periodStatToClient));
                DashboardClient.g_clients.ForEach(client =>
                {
                    if (client.WsWebSocket!.State == WebSocketState.Open)
                        client.WsWebSocket.SendAsync(new ArraySegment<Byte>(encodedMsg, 0, encodedMsg.Length), WebSocketMessageType.Text, true, CancellationToken.None);    //  takes 0.635ms
                });
            }
        }

        // Return from this function very quickly. Do not call any Clients.Caller.SendAsync(), because client will not notice that connection is Connected, and therefore cannot send extra messages until we return here
        public void OnConnectedWsAsync_MktHealth()
        {
            new Thread(() =>
            {
                Thread.CurrentThread.IsBackground = true;  //  thread will be killed when all foreground threads have died, the thread will not keep the application alive.

                // for both the first and the second client, we get RT prices from MemDb immediately and send it back to this Client only.

                // 1. Send the Historical data first. SendAsync() is non-blocking. GetLastRtPrice() can be blocking
                IEnumerable<RtMktSumNonRtStat> periodStatToClient = GetLookbackStat("YTD");
                Utils.Logger.Info("OnConnectedWsAsync_MktHealth(): RtMktSumNonRtStat");
                byte[] encodedMsg = Encoding.UTF8.GetBytes("RtMktSumNonRtStat:" + Utils.CamelCaseSerialize(periodStatToClient));
                if (WsWebSocket!.State == WebSocketState.Open)
                    WsWebSocket.SendAsync(new ArraySegment<Byte>(encodedMsg, 0, encodedMsg.Length), WebSocketMessageType.Text, true, CancellationToken.None);    //  takes 0.635ms

                // 2. Send RT price later, because GetLastRtPrice() might block the thread, if it is the first client.
                var lastPrices = MemDb.gMemDb.GetLastRtPrice(g_mktSummaryStocks.Select(r => r.AssetId).ToArray());
                IEnumerable<RtMktSumRtStat> rtMktSummaryToClient = lastPrices.Select(r =>
                {
                    var rtStock = new RtMktSumRtStat()
                    {
                        AssetId = r.SecdID,
                        Last = r.LastPrice,
                    };
                    return rtStock;
                });
                Utils.Logger.Info("OnConnectedWsAsync_MktHealth(): RtMktSumRtStat");
                encodedMsg = Encoding.UTF8.GetBytes("RtMktSumRtStat:" + Utils.CamelCaseSerialize(rtMktSummaryToClient));
                if (WsWebSocket!.State == WebSocketState.Open)
                    WsWebSocket.SendAsync(new ArraySegment<Byte>(encodedMsg, 0, encodedMsg.Length), WebSocketMessageType.Text, true, CancellationToken.None);    //  takes 0.635ms

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

        public async Task OnReceiveWsAsync_MktHealth(WebSocketReceiveResult? wsResult, string msgCode, string msgObjStr)
        {
            switch (msgCode)
            {
                case "changeLookback":
                    IEnumerable<RtMktSumNonRtStat> periodStatToClient = GetLookbackStat(msgObjStr);
                    Utils.Logger.Info("OnConnectedWsAsync_MktHealth(): RtMktSumNonRtStat");
                    byte[] encodedMsg = Encoding.UTF8.GetBytes("RtMktSumNonRtStat:" + Utils.CamelCaseSerialize(periodStatToClient));
                    if (WsWebSocket!.State == WebSocketState.Open)
                        await WsWebSocket.SendAsync(new ArraySegment<Byte>(encodedMsg, 0, encodedMsg.Length), WebSocketMessageType.Text, true, CancellationToken.None);    //  takes 0.635ms
                    break;
                default:
                    // throw new Exception($"Unexpected websocket received msgCode '{msgCode}'");
                    Utils.Logger.Error($"Unexpected websocket received msgCode '{msgCode}'");
                    break;
            }
        }

        // Return from this function very quickly. Do not call any Clients.Caller.SendAsync(), because client will not notice that connection is Connected, and therefore cannot send extra messages until we return here
        public void OnConnectedSignalRAsync_MktHealth()
        {
            new Thread(() =>
            {
                Thread.CurrentThread.IsBackground = true;  //  thread will be killed when all foreground threads have died, the thread will not keep the application alive.

                // for both the first and the second client, we get RT prices from MemDb immediately and send it back to this Client only.

                // 1. Send the Historical data first. SendAsync() is non-blocking. GetLastRtPrice() can be blocking
                IEnumerable<RtMktSumNonRtStat> periodStatToClient = GetLookbackStat("YTD");
                Utils.Logger.Info("Clients.Caller.SendAsync: RtMktSumNonRtStat");
                // Clients.Caller.SendAsync("RtMktSumNonRtStat", periodStatToClient);      // Cannot access a disposed object.
                DashboardPushHubKestrelBckgrndSrv.HubContext?.Clients.Client(SignalRConnectionId).SendAsync("RtMktSumNonRtStat", periodStatToClient);

                // 2. Send RT price later, because GetLastRtPrice() might block the thread, if it is the first client.
                var lastPrices = MemDb.gMemDb.GetLastRtPrice(g_mktSummaryStocks.Select(r => r.AssetId).ToArray());
                IEnumerable<RtMktSumRtStat> rtMktSummaryToClient = lastPrices.Select(r =>
                {
                    var rtStock = new RtMktSumRtStat()
                    {
                        AssetId = r.SecdID,
                        Last = r.LastPrice,
                    };
                    return rtStock;
                });
                Utils.Logger.Info("Clients.Caller.SendAsync: RtMktSumRtStat");
                DashboardPushHubKestrelBckgrndSrv.HubContext?.Clients.Client(SignalRConnectionId).SendAsync("RtMktSumRtStat", rtMktSummaryToClient);   // Cannot access a disposed object.

                // lock (m_rtMktSummaryTimerLock)
                // {
                //     if (!m_rtMktSummaryTimerRunning)
                //     {
                //         Utils.Logger.Info("OnConnectedAsync_MktHealth(). Starting m_rtMktSummaryTimer.");
                //         m_rtMktSummaryTimerRunning = true;
                //         m_rtMktSummaryTimer.Change(TimeSpan.FromMilliseconds(m_rtMktSummaryTimerFrequencyMs), TimeSpan.FromMilliseconds(-1.0));    // runs only once. To avoid that it runs parallel, if first one doesn't finish
                //     }
                // }
            }).Start();
        }

        private static IEnumerable<RtMktSumNonRtStat> GetLookbackStat(string p_lookbackStr)
        {
            DateTime todayET = Utils.ConvertTimeFromUtcToEt(DateTime.UtcNow).Date;  // the default is YTD.
            DateTime lookbackStart = new DateTime(todayET.Year - 1, 12, 31);  // YTD relative to 31st December, last year
            if (p_lookbackStr.EndsWith("y"))
            {
                if (Int32.TryParse(p_lookbackStr.Substring(0, p_lookbackStr.Length - 1), out int nYears))
                    lookbackStart = todayET.AddYears(-1 * nYears);
            }
            else if (p_lookbackStr.EndsWith("m"))
            {
                if (Int32.TryParse(p_lookbackStr.Substring(0, p_lookbackStr.Length - 1), out int nMonths))
                    lookbackStart = todayET.AddMonths(-1 * nMonths);
            }
            else if (p_lookbackStr.EndsWith("w"))
            {
                if (Int32.TryParse(p_lookbackStr.Substring(0, p_lookbackStr.Length - 1), out int nWeeks))
                    lookbackStart = todayET.AddDays(-7 * nWeeks);
            }

            TsDateData<DateOnly, uint, float, uint> histData = MemDb.gMemDb.DailyHist.GetDataDirect();
            DateOnly[] dates = histData.Dates;
            // At 16:00, or even intraday: YF gives even the today last-realtime price with a today-date. We have to find any date backwards, which is NOT today. That is the PreviousClose.
            int iEndDay = (dates[0] >= new DateOnly(Utils.ConvertTimeFromUtcToEt(DateTime.UtcNow))) ? 1 : 0;
            Debug.WriteLine($"EndDate: {dates[iEndDay]}");

            int iStartDay = histData.IndexOfKeyOrAfter(new DateOnly(lookbackStart));      // the valid price at the weekend is the one on the previous Friday. After.
            if (iStartDay == -1) // If not found then fix the startDate as the first available date of history.
            {
                iStartDay = dates.Length - 1; 
            }
            Debug.WriteLine($"StartDate: {dates[iStartDay]}");


            IEnumerable<RtMktSumNonRtStat> lookbackStatToClient = g_mktSummaryStocks.Select(r =>
            {
                float[] sdaCloses = histData.Data[r.AssetId].Item1[TickType.SplitDivAdjClose];
                // if startDate is not found, because e.g. we want to go back 3 years, while stock has only 2 years history
                int iiStartDay = (iStartDay < sdaCloses.Length) ? iStartDay : sdaCloses.Length - 1;

                // reverse marching from yesterday into past is not good, because we have to calculate running maxDD, maxDU.
                float max = float.MinValue, min = float.MaxValue, maxDD = float.MaxValue, maxDU = float.MinValue;
                for (int i = iiStartDay; i >= iEndDay; i--)   // reverse marching from yesterday iEndDay to deeper into the past. Until startdate iStartDay or until history beginning reached
                {
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

                var rtStock = new RtMktSumNonRtStat()
                {
                    AssetId = r.AssetId,
                    Ticker = r.Ticker,
                    PreviousClose = sdaCloses[iEndDay],
                    PeriodStart = dates[iiStartDay],    // it may be not the 'asked' start date if we have less price history
                    PeriodOpen = sdaCloses[iiStartDay],
                    PeriodHigh = max,
                    PeriodLow = min,
                    PeriodMaxDD = maxDD,
                    PeriodMaxDU = maxDU
                };
                return rtStock;
            });

            return lookbackStatToClient;
        }

        public void OnDisconnectedSignalRAsync_MktHealth(Exception exception)
        {
            if (DashboardClient.g_clients.Count == 0)
            {
                lock (m_rtMktSummaryTimerLock)
                {
                    if (m_rtMktSummaryTimerRunning)
                    {
                        m_rtMktSummaryTimer.Change(TimeSpan.FromMilliseconds(-1.0), TimeSpan.FromMilliseconds(-1.0));    // runs only once. To avoid that it runs parallel, if first one doesn't finish
                        m_rtMktSummaryTimerRunning = false;
                    }
                }
            }
        }

        public static void RtMktSummaryTimer_Elapsed(object? state)    // Timer is coming on a ThreadPool thread
        {
            try
            {
                Utils.Logger.Info("RtMktSummaryTimer_Elapsed(). BEGIN");
                if (!m_rtMktSummaryTimerRunning)
                    return; // if it was disabled by another thread in the meantime, we should not waste resources to execute this.

                var lastPrices = MemDb.gMemDb.GetLastRtPrice(g_mktSummaryStocks.Select(r => r.AssetId).ToArray());
                IEnumerable<RtMktSumRtStat> rtMktSummaryToClient = lastPrices.Select(r =>
                {
                    var rtStock = new RtMktSumRtStat()
                    {
                        AssetId = r.SecdID,
                        Last = r.LastPrice,
                    };
                    return rtStock;
                });

                // Utils.Logger.Info("Clients.All.SendAsync: RtMktSumRtStat");
                DashboardPushHubKestrelBckgrndSrv.HubContext?.Clients.All.SendAsync("RtMktSumRtStat", rtMktSummaryToClient);

                byte[] encodedMsg = Encoding.UTF8.GetBytes("RtMktSumRtStat:" + Utils.CamelCaseSerialize(rtMktSummaryToClient));
                DashboardClient.g_clients.ForEach(client =>
                {
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