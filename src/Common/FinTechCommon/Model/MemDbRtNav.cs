using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SqCommon;
using YahooFinanceApi;

namespace FinTechCommon
{
    public partial class MemDb
    {
        RtFreqParam m_highNavFreqParam = new RtFreqParam() { RtFreq = RtFreq.HighFreq, FreqRthSec = 60, FreqOthSec = 10 * 60 }; // 1min RTH, 10 min OTH
        RtFreqParam m_lowNavFreqParam = new RtFreqParam() { RtFreq = RtFreq.LowFreq, FreqRthSec = 1 * 60 * 60, FreqOthSec = 3 * 60 * 60 }; // 1h RTH, 3h OTH

        uint m_nNavDownload = 0;

        void InitNavRt_WT()    // WT : WorkThread
        {
            m_highNavFreqParam.Timer = new System.Threading.Timer(new TimerCallback(RtNavTimer_Elapsed), m_highNavFreqParam, TimeSpan.FromMilliseconds(-1.0), TimeSpan.FromMilliseconds(-1.0));
            m_lowNavFreqParam.Timer = new System.Threading.Timer(new TimerCallback(RtNavTimer_Elapsed), m_lowNavFreqParam, TimeSpan.FromMilliseconds(-1.0), TimeSpan.FromMilliseconds(-1.0));
        }

        public void ServerDiagnosticNavRealtime(StringBuilder p_sb)
        {
            p_sb.Append($"NavRealtime: m_nNavDownload: {m_nNavDownload} <br>");
            IEnumerable<Asset>? recentlyAskedNavAssets = m_lastRtPriceQueryTime.Where(r => r.Key.AssetId.AssetTypeID == AssetType.BrokerNAV && ((DateTime.UtcNow - r.Value) <= TimeSpan.FromSeconds(5 * 60))).Select(r => r.Key); //  if there was a function call in the last 5 minutes
            p_sb.Append($"NavRealtime (HighFreq-RTH:{(int)(m_highNavFreqParam.FreqRthSec / 60)}min-OTH:{(int)(m_highNavFreqParam.FreqOthSec / 60)}min): NTimerPassed: {m_highNavFreqParam.NTimerPassed}, fix Assets:'{String.Join(',', m_highNavFreqParam.Assets.Select(r => r.LastTicker))}', recently asket Assets:'{String.Join(',', recentlyAskedNavAssets.Select(r => r.LastTicker))}' <br>");
            p_sb.Append($"NavRealtime (LowFreq-RTH:{(int)(m_lowNavFreqParam.FreqRthSec / 60)}min-OTH:{(int)(m_lowNavFreqParam.FreqOthSec / 60)}min): NTimerPassed: {m_lowNavFreqParam.NTimerPassed}, fix Assets:'{String.Join(',', m_lowNavFreqParam.Assets.Select(r => r.LastTicker))}' <br>");
        }

        void OnReloadAssetData_ReloadRtNavDataAndSetTimer()
        {
            Utils.Logger.Info("ReloadRtNavDataAndSetTimer() START");
            m_highNavFreqParam.Assets = new Asset[0];
            m_lowNavFreqParam.Assets = AssetsCache.Assets.Where(r => r.AssetId.AssetTypeID == AssetType.BrokerNAV && !r.IsAggregatedNav && !m_highNavFreqParam.Assets.Contains(r)).ToArray()!;
            RtNavTimer_Elapsed(m_highNavFreqParam);
            RtNavTimer_Elapsed(m_lowNavFreqParam);
            Utils.Logger.Info("ReloadRtNavDataAndSetTimer() END");
        }

        public void RtNavTimer_Elapsed(object p_state)    // Timer is coming on a ThreadPool thread
        {
            RtFreqParam freqParam = (RtFreqParam)p_state;
            Utils.Logger.Info($"MemDbRt.RtNavTimer_Elapsed({freqParam.RtFreq}). BEGIN.");
            try
            {
                UpdateNavRt(freqParam);
            }
            catch (System.Exception e)  // Exceptions in timers crash the app.
            {
                Utils.Logger.Error(e, $"MemDbRt.RtNavTimer_Elapsed({freqParam.RtFreq}) exception.");
            }
            SetTimerRt(freqParam);
            Utils.Logger.Info($"MemDbRt.RtNavTimer_Elapsed({freqParam.RtFreq}). END");
        }
        private void UpdateNavRt(RtFreqParam p_freqParam)
        {
            // NAV Tickers GA.IM.NAV, DC.IM.NAV, DC.ID.NAV, DC.NAV , but select only the Non-virtual non-Aggregate ones.
            // Most of the time, DC watches he "DC.NAV" real-time only. In that case, don't update Agy's "GA.IM.NAV" in every 60 seconds.
            p_freqParam.NTimerPassed++;
            Asset[] downloadAssets = p_freqParam.Assets;
            if (p_freqParam.RtFreq == RtFreq.HighFreq)  // if it is highFreq timer, then add the recently asked assets.
            {
                List<Asset> updatingNavAssets = new List<Asset>();
                var recentlyAskedNavAssets = m_lastRtPriceQueryTime.Where(r => r.Key.AssetId.AssetTypeID == AssetType.BrokerNAV && ((DateTime.UtcNow - r.Value) <= TimeSpan.FromSeconds(5 * 60))).Select(r => r.Key); //  if there was a function call in the last 5 minutes
                foreach (var nav in recentlyAskedNavAssets)
                {
                    // the virtual DC.NAV assets: replace them with the underlying sub-Navs
                    if (nav.IsAggregatedNav)    // add the underlying sub-Navs
                        updatingNavAssets.AddRange(AssetsCache.Assets.Where(r => r.AssetId.AssetTypeID == AssetType.BrokerNAV && !r.IsAggregatedNav && r.User == nav.User));
                    else
                        updatingNavAssets.Add(nav);
                }
                downloadAssets = p_freqParam.Assets.Concat(updatingNavAssets).ToArray();
            }
            if (downloadAssets.Length == 0)
                return;

            DownloadLastPriceNav(downloadAssets.ToList());
        }
        
        (float, DateTime) GetLastNavRtPrice(Asset p_navAsset)
        {
            float lastValue;
            DateTime lastValueUtc;  // if there are 2 subNavs, we want the Minimum of the UTCs. To be conservative how old the aggregated Time is.
            if (p_navAsset.IsAggregatedNav)
            {
                lastValue = 0;
                lastValueUtc = DateTime.MaxValue;
                foreach (var asset in AssetsCache.Assets)
                {
                    if (asset.User == p_navAsset.User && !asset.IsAggregatedNav)
                    {
                        if (Single.IsNaN(asset.LastValue))  // if any of the SubNavs is NaN, because VBroker is not running or didn't return data, then return NaN for the aggregate to show it is invalid
                        {
                            lastValue = Single.NaN; // signal an error
                            break;
                        }
                        lastValue += asset.LastValue;
                        if (lastValueUtc > asset.LastValueUtc)
                            lastValueUtc = asset.LastValueUtc;
                    }
                }
                if (lastValueUtc == DateTime.MaxValue)  // we failed to find any good value => indicate error as MinValue. To show data is too old.
                    lastValueUtc = DateTime.MinValue;
            }
            else
            {
                lastValue = p_navAsset.LastValue;
                lastValueUtc = p_navAsset.LastValueUtc;
            }
            
            return (lastValue, lastValueUtc);
        }


        void DownloadLastPriceNav(List<Asset> p_navAssets)
        {
            Utils.Logger.Info("DownloadLastPriceNav() START");
            m_nNavDownload++;
            try
            {
                DownloadLastPriceNavFromVbServer(p_navAssets, VBrokerServer.AutoVb);
                DownloadLastPriceNavFromVbServer(p_navAssets, VBrokerServer.ManualVb);
            }
            catch (Exception e)
            {
                Utils.Logger.Error(e, "DownloadLastPriceNav()");
            }
        }

        private void DownloadLastPriceNavFromVbServer(List<Asset> p_navAssets, VBrokerServer p_vbServer)
        {
            List<Asset> acceptableNavAssets = new List<Asset>();
            List<string> bAccStrArr = new List<string>();
            foreach (var nav in p_navAssets)
            {
                if (p_vbServer == VBrokerServer.AutoVb && nav.AssetId.SubTableID == 1)
                {
                    acceptableNavAssets.Add(nav);
                    bAccStrArr.Add("Gyantal");
                }
                else if (p_vbServer == VBrokerServer.ManualVb)
                { 
                    if (nav.AssetId.SubTableID == 2)
                    {
                        acceptableNavAssets.Add(nav);
                        bAccStrArr.Add("Charmat");
                    } else if (nav.AssetId.SubTableID == 3)
                    {
                        acceptableNavAssets.Add(nav);
                        bAccStrArr.Add("DeBlanzac");
                    }
                }
            }
            if (acceptableNavAssets.Count == 0)
                return;

            string vbServerIp = p_vbServer == VBrokerServer.AutoVb ? ServerIp.AtsVirtualBrokerServerPublicIpForClients : ServerIp.StandardLocalhostWithIP;
            string brAccStr = String.Join(',', bAccStrArr.ToArray());

            string msg = $"?v=1&secTok={TcpMessage.GenerateSecurityToken()}&bAcc={brAccStr}&data=AccSum";
            Task<string?> vbMessageTask = TcpMessage.Send(msg, TcpMessageID.GetAccountsInfo, vbServerIp, ServerIp.DefaultVirtualBrokerServerPort);

            string? vbReplyStr = vbMessageTask.Result;
            if (vbMessageTask.Exception != null || String.IsNullOrEmpty(vbReplyStr))
            {
                string errorMsg = $"Error. Check that both the IB's TWS and the VirtualBroker are running on Manual/Auto Trading Server! Start them manually if needed!";
                Utils.Logger.Error(errorMsg);
                return;
            }
            vbReplyStr = vbReplyStr.Replace("\\\"", "\"");
            Utils.Logger.Info($"UpdateRtNavFromVbServer(). Received '{vbReplyStr}'");
            var vbReply = Utils.LoadFromJSON<List<BrAccJsonHelper>>(vbReplyStr);
            foreach (var brAccInfo in vbReply) // Charmat,DeBlanzac grouped together or Gyantal
            {
                string? brAcc = brAccInfo.BrAcc;
                double nav = Double.NegativeInfinity;
                foreach (var accSum in brAccInfo.AccSums!)  // in theory it can return 2 AccInfo if user has 2 accounts, but probably it is a failed implementation. However, I keep it for current compatibility with SqLab VBroker.
                {
                    if (accSum["Tag"] != "NetLiquidation")
                        continue;
                    string navStr = accSum["Value"];
                    if (!Double.TryParse(navStr, out nav))
                        nav = Double.NegativeInfinity;
                    break;
                }
                if (nav == Double.NegativeInfinity)
                    continue;

                var subTableId = brAcc switch
                {
                    "Gyantal" => 1,
                    "Charmat" => 2,
                    "DeBlanzac" => 3,
                    _ => Int32.MinValue,
                };
                if (subTableId == Int32.MinValue)
                    continue;

                var brAccNav = p_navAssets.Where(r => r.AssetId.SubTableID == subTableId).FirstOrDefault();
                if (brAccNav != null) {
                    brAccNav.LastValue = (int)Math.Round(nav, MidpointRounding.AwayFromZero); // 0.5 is rounded to 1, -0.5 is rounded to -1. Good.
                }
            }
        }

    }

}