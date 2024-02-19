using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Fin.Base;
using Fin.BrokerCommon;
using SqCommon;

namespace Fin.MemDb;

enum RtFreq { HighFreq, MidFreq, LowFreq }
internal class RtFreqParam
{
    public RtFreq RtFreq { get; set; }
    public string Name { get; set; } = string.Empty;
    public int FreqRthSec { get; set; } // RTH: Regular Trading Hours
    public int FreqOthSec { get; set; } // OTH: Outside Trading Hours
    public Timer? Timer { get; set; } = null;
    public Asset[] Assets { get; set; } = Array.Empty<Asset>();
    public uint NTimerPassed { get; set; } = 0;
    public DateTime LastUpdateTimeUtc { get; set; } = DateTime.MinValue;
}
// many functions might use RT price, so it should be part of the MemDb
public partial class MemDb
{
    // ***** Plan: Historical and RT quote data
    // - the Website.app, once a day, gets historical price data from YF. Get all history for selected 100 stocks only
    // - HistPriceService does PushHistSdaPriorClosesToAssets(), because Rt price service sometimes fully fail (either YF or Iex). At least we have PriorClose for those assets.
    // - PriorClose is updated for all assets, in Rt-timer LowFreq method. (If RT timer service works) every 1 hour in RTH, 2h in OTH
    // - for RT price: during market-hours use IEX 'top', because monhly 50K queries are free (can use multiple IEX accounts) (and YF might ban our IP if we query too many times, IB has rate limit per minute, and VBroker need that bandwidth)
    // - pre/postmarket, also use YF, but with very-very low frequency. We don't need to use IB Markprice, because YF pre-market is very quickly available at 9:00, 5.5h before open.
    // - code should know whether it is pre/postmarket hours, so we have to implement the same logic as in VBroker. (with the holiday days, and DB).
    // - Call AfterMarket = PostMarket, because shorter and tradingview.com also calls "post-market" (YF calls: After hours)

    readonly RtFreqParam m_highFreqParam = new() { RtFreq = RtFreq.HighFreq, Name = "HighFreq", FreqRthSec = 30, FreqOthSec = 5 * 60 }; // high frequency (30sec RTH, 5min otherwise-OTH) refresh for a known fixed stocks (used by VBroker) and those which were queried in the last 5 minutes (by a VBroker-test)
    readonly RtFreqParam m_midFreqParam = new() { RtFreq = RtFreq.MidFreq, Name = "MidFreq", FreqRthSec = 15 * 60, FreqOthSec = 40 * 60 }; // mid frequency (15min RTH, 40min otherwise) refresh for a know fixed stocks (DashboardClient_mktHealth)
    readonly RtFreqParam m_lowFreqParam = new() { RtFreq = RtFreq.LowFreq, Name = "LowFreq", FreqRthSec = 30 * 60, FreqOthSec = 1 * 60 * 60 }; // with low frequency (30 RTH, 1h otherwise). Almost all stocks. Even if nobody access them.

    // In general: higFreq: probably the traded stocks + what was RT queried by users. Mid: some special tickers (e.g. on MarketDashboard), LowFreq: ALL alive stocks.
    // string[] m_ibRtStreamedTickrs = Array.Empty<string>();   // /* VBroker */ no need for frequency Timer. IB prices will be streamed. So, in the future, we might delete m_highFreqParam. But maybe we need 10seconds ticker prices for non VBroker tasks. So, probably keep the streamed tickers very low. And this can be about 6-20seconds frequency.
    readonly string[] m_highFreqTickrs = Array.Empty<string>(); /* VBroker */
    readonly string[] m_midFreqTickrs = new string[]
    {
        "S/QQQ", "S/SPY", "S/GLD", "S/TLT", "S/VXX", "S/UNG", "S/USO", /* DashboardClient_mktHealth.cs */
        "S/VIXY", "S/TQQQ", "S/UPRO", "S/SVXY", "S/TMV", "S/UCO" /*, "S/UNG" already present in DashboardClient */ /* , "I/VIX" */ /* StrategyRenewedUber.cs */
            /* StrategySin.cs */ // future when we trade Sin based on SqCore: add these tickers from here https://docs.google.com/spreadsheets/d/1JXMbEMAP5AOqB1FjdM8jpptXfpuOno2VaFVYK8A1eLo/edit#gid=0
    };

    readonly RtPriceDownloader m_rtPriceDownloader = new();
    Dictionary<Asset, DateTime> m_lastRtPriceQueryTime = new();

    void InitAllStockAssetsPriorCloseAndLastPrice(AssetsCache p_newAssetCache) // this is called at Program.Init() and at ReloadDbDataIfChangedImpl(). Keep this method as the logic of what to select should be at one place.
    {
        Asset[] assetsWithRtValue = p_newAssetCache.Assets.Where(r => r.AssetId.AssetTypeID == AssetType.FinIndex || (r.AssetId.AssetTypeID == AssetType.Stock && (r as Stock)!.ExpirationDate == string.Empty)).ToArray();
        m_rtPriceDownloader.DownloadPriorCloseAndLastPrice(assetsWithRtValue).TurnAsyncToSyncTask();
    }

    void InitAllOptionAssetsPriorCloseAndLastPrice(AssetsCache p_newAssetCache) // this is called at Program.Init() and at ReloadDbDataIfChangedImpl()
    {
        // var options = p_newAssetCache.Assets.Where(r => r.AssetId.AssetTypeID == AssetType.Option).Take(1).Select(r => (Option)r).ToArray();
        var options = p_newAssetCache.Assets.Where(r => r.AssetId.AssetTypeID == AssetType.Option).ToArray();
        m_rtPriceDownloader.DownloadLastPriceOptions(options);
    }

    public Task DownloadLastPrice(Asset[] p_assets) // takes 45 ms from WinPC. Can handle Stock and Index assets as "^VIX"
    {
        if (p_assets.Length == 0)
            return Task.CompletedTask;
        return m_rtPriceDownloader.DownloadLastPrice(p_assets);
    }

    // Once a day, PriorClose download for all assets is also required (because not all assets have historical data), but in general when clients frequently wants only RT price, don't query PriorCloses too
    // IEX: Getting PriorCloses takes 11 queries, while only 1 query if only LastPrice is needed
    public Task DownloadPriorCloseAndLastPrice(Asset[] p_assets) // takes 45 ms from WinPC. Can handle Stock and Index assets as "^VIX"
    {
        if (p_assets.Length == 0)
            return Task.CompletedTask;
        return m_rtPriceDownloader.DownloadPriorCloseAndLastPrice(p_assets);
    }

    public void DownloadLastPriceOptions(Asset[] p_options)
    {
        m_rtPriceDownloader.DownloadLastPriceOptions(p_options);
    }

    void OnReloadAssetData_InitAndScheduleRtTimers() // this is called at Program.Init() and at ReloadDbDataIfChangedImpl()
    {
        m_lastRtPriceQueryTime = new Dictionary<Asset, DateTime>(); // purge out history after AssetData reload
        m_highFreqParam.Assets = m_highFreqTickrs.Select(r => AssetsCache.GetAsset(r)!).ToArray();
        m_midFreqParam.Assets = m_midFreqTickrs.Select(r => AssetsCache.GetAsset(r)!).ToArray();
        // m_lowFreqParam.Assets = AssetsCache.Assets.Where(r =>
        //     (r.AssetId.AssetTypeID == AssetType.FinIndex || (r.AssetId.AssetTypeID == AssetType.Stock && (r as Stock)!.ExpirationDate == string.Empty))
        //     && !m_highFreqTickrs.Contains(r.SqTicker) && !m_midFreqTickrs.Contains(r.SqTicker)).ToArray()!;

        // Main logic:
        // schedule RtTimer_Elapsed() at Init() (after every OnReloadAssetData) and also once per hour (lowFreq) (even if nobody asked it) for All assets in MemDb. So we always have more or less fresh data
        // GetLastRtPrice() always return data without blocking. Data might be 1 hour old, but it is OK. If we are in a Non-busy mode, then switch to busy and schedule it immediately.
        m_highFreqParam.Timer ??= new System.Threading.Timer(new TimerCallback(RtTimer_Elapsed), m_highFreqParam, TimeSpan.FromMilliseconds(-1.0), TimeSpan.FromMilliseconds(-1.0));
        m_midFreqParam.Timer ??= new System.Threading.Timer(new TimerCallback(RtTimer_Elapsed), m_midFreqParam, TimeSpan.FromMilliseconds(-1.0), TimeSpan.FromMilliseconds(-1.0));
        m_lowFreqParam.Timer ??= new System.Threading.Timer(new TimerCallback(RtTimer_Elapsed), m_lowFreqParam, TimeSpan.FromMilliseconds(-1.0), TimeSpan.FromMilliseconds(-1.0));

        ScheduleTimerRt(m_highFreqParam);
        ScheduleTimerRt(m_midFreqParam);
        ScheduleTimerRt(m_lowFreqParam);
    }

    public void ServerDiagnosticRealtime(StringBuilder p_sb)
    {
        m_rtPriceDownloader.ServerDiagnostic(p_sb);
        var recentlyAskedAssets = m_lastRtPriceQueryTime.Where(r => (DateTime.UtcNow - r.Value) <= TimeSpan.FromSeconds(5 * 60));
        p_sb.Append($"All recentlyAskedAssets:'{String.Join(',', recentlyAskedAssets.Select(r => r.Key.SqTicker + "(" + ((int)(DateTime.UtcNow - r.Value).TotalSeconds).ToString() + "sec)"))}' <br>");

        ServerDiagnosticRealtime(p_sb, m_highFreqParam);
        ServerDiagnosticRealtime(p_sb, m_midFreqParam);
        ServerDiagnosticRealtime(p_sb, m_lowFreqParam);
    }

    private static void ServerDiagnosticRealtime(StringBuilder p_sb, RtFreqParam p_rtFreqParam)
    {
        p_sb.Append($"Realtime ({p_rtFreqParam.Name}): LastUpdateUtc: {p_rtFreqParam.LastUpdateTimeUtc.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)}, FreqRthSec: {p_rtFreqParam.FreqRthSec}, FreqOthSec: {p_rtFreqParam.FreqOthSec}, #TimerPassed: {p_rtFreqParam.NTimerPassed}, assets: '");
        p_sb.AppendLongListByLine(p_rtFreqParam.Assets.Where(r => r.AssetId.AssetTypeID == AssetType.Stock).Select(r => ((Stock)r).YfTicker), ",", 30, "<br>");
        p_sb.Append($"'<br>");
    }

    public void RtTimer_Elapsed(object? p_state) // Timer is coming on a ThreadPool thread
    {
        if (p_state == null)
            throw new Exception("RtTimer_Elapsed() received null object.");

        RtFreqParam freqParam = (RtFreqParam)p_state;
        Utils.Logger.Info($"MemDbRt.RtTimer_Elapsed({freqParam.RtFreq}). BEGIN.");
        freqParam.LastUpdateTimeUtc = DateTime.UtcNow;
        try
        {
            RtTimerUpdate(freqParam);
        }
        catch (System.Exception e) // Exceptions in timers crash the app.
        {
            Utils.Logger.Error(e, $"MemDbRt.RtTimer_Elapsed({freqParam.RtFreq}) exception.");
        }
        ScheduleTimerRt(freqParam);
        Utils.Logger.Debug($"MemDbRt.RtTimer_Elapsed({freqParam.RtFreq}). END");
    }
    private void RtTimerUpdate(RtFreqParam p_freqParam)
    {
        p_freqParam.NTimerPassed++;
        Asset[] downloadAssets = p_freqParam.Assets;
        if (p_freqParam.RtFreq == RtFreq.HighFreq) // if it is highFreq timer, then add the recently asked assets.
        {
            var recentlyAskedNonNavAssets = m_lastRtPriceQueryTime.Where(r => r.Key.AssetId.AssetTypeID != AssetType.BrokerNAV && ((DateTime.UtcNow - r.Value) <= TimeSpan.FromSeconds(5 * 60))).Select(r => r.Key); // if there was a function call in the last 5 minutes
            downloadAssets = p_freqParam.Assets.Concat(recentlyAskedNonNavAssets).ToArray();
        }
        else if (p_freqParam.RtFreq == RtFreq.LowFreq) // LowFreq will update PriorClose as well for All assets
        {
            downloadAssets = AssetsCache.Assets.Where(r =>
                r.AssetId.AssetTypeID == AssetType.FinIndex || (r.AssetId.AssetTypeID == AssetType.Stock && (r as Stock)!.ExpirationDate == string.Empty))
                // && !m_highFreqTickrs.Contains(r.SqTicker) && !m_midFreqTickrs.Contains(r.SqTicker)
                .ToArray()!;
        }

        if (downloadAssets.Length != 0)
            m_rtPriceDownloader.RtTimerUpdate(p_freqParam, downloadAssets);

        if (p_freqParam.RtFreq == RtFreq.LowFreq)
            m_rtPriceDownloader.DownloadLastPriceOptions(MemDb.gMemDb.AssetsCache.Assets.Where(r => r.AssetId.AssetTypeID == AssetType.Option).ToArray());
    }

    private static void ScheduleTimerRt(RtFreqParam p_freqParam)
    {
        // lock (m_rtTimerLock)
        var tradingHoursNow = Utils.UsaTradingHoursExNow_withoutHolidays();
        p_freqParam.Timer!.Change(TimeSpan.FromSeconds((tradingHoursNow == TradingHoursEx.RegularTrading) ? p_freqParam.FreqRthSec : p_freqParam.FreqOthSec), TimeSpan.FromMilliseconds(-1.0));
    }

    public float GetLastRtValue(Asset p_asset)
    {
        m_lastRtPriceQueryTime[p_asset] = DateTime.UtcNow;
        float lastValue;
        if (p_asset.AssetId.AssetTypeID == AssetType.BrokerNAV)
            lastValue = GetLastNavRtPrice((p_asset as BrokerNav)!).LastValue;
        else
            lastValue = p_asset.EstValue;
        return lastValue;
    }

    // GetLastRtValue() always return data without blocking. Data might be 1 hour old or 3sec (RTH) or in 60sec (non-RTH) for m_assetIds only if there was a function call in the last 5 minutes (busyMode), but it is OK.
    public IEnumerable<(AssetId32Bits SecdID, float LastValue)> GetLastRtValue(uint[] p_assetIds) // C# 7.0 adds tuple types and named tuple literals. uint[] is faster to create and more RAM efficient than linked-list<uint>
    {
        IEnumerable<(AssetId32Bits SecdID, float LastValue)> rtPrices = p_assetIds.Select(r =>
        {
            var asset = AssetsCache.GetAsset(r);
            m_lastRtPriceQueryTime[asset] = DateTime.UtcNow;
            DateTime lastDateTime = DateTime.MinValue;
            float lastValue;
            if (asset.AssetId.AssetTypeID == AssetType.BrokerNAV)
                (lastValue, lastDateTime) = GetLastNavRtPrice((asset as BrokerNav)!);
            else
                lastValue = asset.EstValue;

            return (asset.AssetId, lastValue);
        });
        return rtPrices;
    }
    public IEnumerable<(AssetId32Bits SecdID, float LastValue, DateTime LastValueUtc)> GetLastRtValueWithUtc(uint[] p_assetIds) // C# 7.0 adds tuple types and named tuple literals. uint[] is faster to create and more RAM efficient than List<uint>
    {
        IEnumerable<(AssetId32Bits SecdID, float LastValue, DateTime LastValueUtc)> rtPrices = p_assetIds.Select(r =>
        {
            var asset = AssetsCache.GetAsset(r);
            m_lastRtPriceQueryTime[asset] = DateTime.UtcNow;
            DateTime lastDateTime = DateTime.MinValue;
            float lastValue;
            if (asset.AssetId.AssetTypeID == AssetType.BrokerNAV)
                (lastValue, lastDateTime) = GetLastNavRtPrice((asset as BrokerNav)!);
            else
            {
                lastValue = asset.EstValue;
                lastDateTime = asset.EstValueTimeUtc;
            }
            return (asset.AssetId, lastValue, lastDateTime);
        });
        return rtPrices;
    }

    public IEnumerable<(AssetId32Bits SecdID, float LastValue, DateTime LastValueUtc)> GetLastRtValueWithUtc(List<Asset> p_assets) // C# 7.0 adds tuple types and named tuple literals. uint[] is faster to create and more RAM efficient than List<uint>
    {
        IEnumerable<(AssetId32Bits SecdID, float LastValue, DateTime LastValueUtc)> rtPrices = p_assets.Select(r =>
            {
                m_lastRtPriceQueryTime[r] = DateTime.UtcNow;
                DateTime lastDateTime = DateTime.MinValue;
                float lastValue;
                if (r.AssetId.AssetTypeID == AssetType.BrokerNAV)
                    (lastValue, lastDateTime) = GetLastNavRtPrice((r as BrokerNav)!);
                else
                {
                    lastValue = r.EstValue;
                    lastDateTime = r.EstValueTimeUtc;
                }
                return (r.AssetId, lastValue, lastDateTime);
            });
        return rtPrices;
    }
}