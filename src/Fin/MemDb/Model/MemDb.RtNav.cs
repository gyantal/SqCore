using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Fin.Base;
using Fin.BrokerCommon;
using IBApi;
using SqCommon;

namespace Fin.MemDb;

public partial class MemDb
{
    readonly RtFreqParam m_highNavFreqParam = new() { RtFreq = RtFreq.HighFreq, FreqRthSec = 60, FreqOthSec = 10 * 60 }; // 1min RTH, 10 min OTH
    readonly RtFreqParam m_lowNavFreqParam = new() { RtFreq = RtFreq.LowFreq, FreqRthSec = 1 * 60 * 60, FreqOthSec = 3 * 60 * 60 }; // 1h RTH, 3h OTH

    uint m_nNavDownload = 0;

    void InitAndScheduleNavRtTimers()
    {
        m_highNavFreqParam.Timer = new System.Threading.Timer(new TimerCallback(RtNavTimer_Elapsed), m_highNavFreqParam, TimeSpan.FromMilliseconds(-1.0), TimeSpan.FromMilliseconds(-1.0));
        m_lowNavFreqParam.Timer = new System.Threading.Timer(new TimerCallback(RtNavTimer_Elapsed), m_lowNavFreqParam, TimeSpan.FromMilliseconds(-1.0), TimeSpan.FromMilliseconds(-1.0));

        m_highNavFreqParam.Assets = Array.Empty<Asset>();
        m_lowNavFreqParam.Assets = AssetsCache.Assets.Where(r => r.AssetId.AssetTypeID == AssetType.BrokerNAV && !(r as BrokerNav)!.IsAggregatedNav && !m_highNavFreqParam.Assets.Contains(r)).ToArray()!;

        ScheduleTimerRt(m_highNavFreqParam);
        ScheduleTimerRt(m_lowNavFreqParam);
    }

    void OnReloadAssetData_ReloadRtNavDataAndSetTimer()
    {
        Utils.Logger.Info("ReloadRtNavDataAndSetTimer() START");
        m_highNavFreqParam.Assets = Array.Empty<Asset>();
        m_lowNavFreqParam.Assets = AssetsCache.Assets.Where(r => r.AssetId.AssetTypeID == AssetType.BrokerNAV && !(r as BrokerNav)!.IsAggregatedNav && !m_highNavFreqParam.Assets.Contains(r)).ToArray()!;
        RtNavTimer_Elapsed(m_highNavFreqParam);
        RtNavTimer_Elapsed(m_lowNavFreqParam);
        Utils.Logger.Info("ReloadRtNavDataAndSetTimer() END");
    }

    public void ServerDiagnosticNavRealtime(StringBuilder p_sb)
    {
        IEnumerable<Asset>? recentlyAskedNavAssets = m_lastRtPriceQueryTime.Where(r => r.Key.AssetId.AssetTypeID == AssetType.BrokerNAV && ((DateTime.UtcNow - r.Value) <= TimeSpan.FromSeconds(5 * 60))).Select(r => r.Key); // if there was a function call in the last 5 minutes
        p_sb.Append($"NavRealtime: m_nNavDownload: {m_nNavDownload} <br>");
        p_sb.Append($"NavRealtime (HighFreq-RTH:{(int)(m_highNavFreqParam.FreqRthSec / 60)}min-OTH:{(int)(m_highNavFreqParam.FreqOthSec / 60)}min): NTimerPassed: {m_highNavFreqParam.NTimerPassed}, fix Assets:'{String.Join(',', m_highNavFreqParam.Assets.Select(r => r.SqTicker))}', recently asket Assets:'{String.Join(',', recentlyAskedNavAssets.Select(r => r.SqTicker))}' <br>");
        p_sb.Append($"NavRealtime (LowFreq-RTH:{(int)(m_lowNavFreqParam.FreqRthSec / 60)}min-OTH:{(int)(m_lowNavFreqParam.FreqOthSec / 60)}min): NTimerPassed: {m_lowNavFreqParam.NTimerPassed}, fix Assets:'{String.Join(',', m_lowNavFreqParam.Assets.Select(r => r.SqTicker))}' <br>");
    }

    public void RtNavTimer_Elapsed(object? p_state) // Timer is coming on a ThreadPool thread
    {
        if (p_state == null)
            throw new Exception("RtNavTimer_Elapsed() received null object.");
        RtFreqParam freqParam = (RtFreqParam)p_state;
        Utils.Logger.Info($"MemDbRt.RtNavTimer_Elapsed({freqParam.RtFreq}). BEGIN.");
        try
        {
            UpdateNavRt(freqParam);
        }
        catch (System.Exception e) // Exceptions in timers crash the app.
        {
            Utils.Logger.Error(e, $"MemDbRt.RtNavTimer_Elapsed({freqParam.RtFreq}) exception.");
        }
        ScheduleTimerRt(freqParam);
        Utils.Logger.Info($"MemDbRt.RtNavTimer_Elapsed({freqParam.RtFreq}). END");
    }
    private void UpdateNavRt(RtFreqParam p_freqParam)
    {
        // NAV Tickers GA.IM.NAV, DC.IM.NAV, DC.ID.NAV, DC.NAV , but select only the Non-virtual non-Aggregate ones.
        // Most of the time, DC watches he "DC.NAV" real-time only. In that case, don't update Agy's "GA.IM.NAV" in every 60 seconds.
        p_freqParam.NTimerPassed++;
        BrokerNav[] downloadAssets = p_freqParam.Assets.Select(r => (r as BrokerNav)!).ToArray();
        if (p_freqParam.RtFreq == RtFreq.HighFreq) // if it is highFreq timer, then add the recently asked assets.
        {
            List<BrokerNav> updatingNavAssets = new();
            var recentlyAskedNavAssets = m_lastRtPriceQueryTime.Where(r => r.Key.AssetId.AssetTypeID == AssetType.BrokerNAV && ((DateTime.UtcNow - r.Value) <= TimeSpan.FromSeconds(5 * 60))).Select(r => (r.Key as BrokerNav)!); // if there was a function call in the last 5 minutes
            foreach (var nav in recentlyAskedNavAssets)
            {
                // the virtual DC.NAV assets: replace them with the underlying sub-Navs
                if (nav.IsAggregatedNav) // add the underlying sub-Navs
                    updatingNavAssets.AddRange(AssetsCache.Assets.Where(r => r.AssetId.AssetTypeID == AssetType.BrokerNAV && !(r as BrokerNav)!.IsAggregatedNav && (r as BrokerNav)!.User == nav.User).Select(r => (r as BrokerNav)!));
                else
                    updatingNavAssets.Add(nav);
            }
            downloadAssets = downloadAssets.Concat(updatingNavAssets).ToArray();
        }
        if (downloadAssets.Length == 0)
            return;

        DownloadLastPriceNav(downloadAssets.ToList());
    }

    static (float LastValue, DateTime LastValueUtc) GetLastNavRtPrice(BrokerNav p_navAsset)
    {
        float lastValue;
        DateTime lastValueUtc;  // if there are 2 subNavs, we want the Minimum of the UTCs. To be conservative how old the aggregated Time is.
        if (p_navAsset.IsAggregatedNav)
        {
            lastValue = 0;
            lastValueUtc = DateTime.MaxValue;
            foreach (var asset in p_navAsset.AggregateNavChildren)
            {
                if (Single.IsNaN(asset.EstValue)) // if any of the SubNavs is NaN, because VBroker is not running or didn't return data, then return NaN for the aggregate to show it is invalid
                {
                    lastValue = Single.NaN; // signal an error
                    break;
                }
                lastValue += asset.EstValue;
                if (lastValueUtc > asset.EstValueTimeUtc)
                    lastValueUtc = asset.EstValueTimeUtc;
            }
            if (lastValueUtc == DateTime.MaxValue) // we failed to find any good value => indicate error as MinValue. To show data is too old.
                lastValueUtc = DateTime.MinValue;
        }
        else
        {
            lastValue = p_navAsset.EstValue;
            lastValueUtc = p_navAsset.EstValueTimeUtc;
        }

        return (lastValue, lastValueUtc);
    }

    // AggregatedNav data:
    // AggregatedNav history: DailyHist stores the AggregatedNav merged history properly. Although it increases RAM usage, it has to be done only once, at MemDb reload. Better to store it.
    // AggregatedNav realtime: AssetsCache has the realtime prices for subNavs. But it is not merged automatically, so AggregatedNav.LastValue is not up-to-date.
    // RT price has to be calculated from subNavs all the time. Two reasons:
    // 1. RT NAV price can arrive every 5 seconds. We don't want to do CPU intensive searches all the time to find the parentNav, then find all children, then aggregate RT prices
    // 2. Aggregating RT is actually not easy. As when a new RT-Sub1 price arrives, if we update the AggregatedNav RT, that might be false. Because what if 1 sec later the RT-Sub2 price arrives.
    // That also has to do the searches and aggregate all the children again. And all of these calculations maybe totally pointless if nobody watches the AggregatedNav.
    // So, better to aggregate RT prices only rarely when it is required by a user.
    void DownloadLastPriceNav(List<BrokerNav> p_navAssets)
    {
        Utils.Logger.Info("DownloadLastPriceNav() START");
        m_nNavDownload++;
        try
        {
            foreach (var navAsset in p_navAssets)
            {
                GatewayId gatewayId = navAsset.GatewayId;
                if (gatewayId == GatewayId.Unknown)
                    continue;
                List<BrAccSum>? accSums = BrokersWatcher.gWatcher.GetAccountSums(gatewayId);
                if (accSums == null)
                    continue;

                navAsset.EstValue = (float)accSums.GetValue(AccountSummaryTags.NetLiquidation);
            }
        }
        catch (Exception e)
        {
            Utils.Logger.Error(e, "DownloadLastPriceNav()");
        }
    }
}