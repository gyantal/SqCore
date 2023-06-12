using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Fin.BrokerCommon;
using SqCommon;
using YahooFinanceApi;

namespace Fin.MemDb;
public class RtPriceDownloaderIb
{
    uint m_nDownload = 0;

    public uint NumDownload { get => m_nDownload; }

    public void DownloadLastPriceOptions(Asset[] p_options) // faster execution if instead of Option[] and casting, we allow Asset[], because we don't have to cast it runtime all the time
    {
        m_nDownload++;
        // MktData[] mktDatas = p_options.Select(r => new MktData(r.MakeIbContract()!) { AssetObj = r}).Take(1).ToArray();  // For Debug.
        MktData[] mktDatas = p_options.Select(r => new MktData(r.MakeIbContract()!) { AssetObj = r }).ToArray();
        BrokersWatcher.gWatcher.CollectIbMarketData(mktDatas, true);

        foreach (var mktData in mktDatas)
        {
            Option option = (Option)mktData.AssetObj!;  // throws exception if asset is not an option. OK. We want to catch those cases. Monitor log files.

            double newPriorClose = mktData.PriorClosePrice * option.Multiplier; // it will be NaN if mktData.PriorClosePrice is NaN
            if (!double.IsNaN(newPriorClose)) // If it is not given by IB, don't overwrite current value by NaN. QQQ 20220121Put100: its value is very low. Bid=None, Ask = 0.02. No wonder its PriorClose = 0.0. But Ib gives proper 0.0 value 80% of the time, with snapshot data 20% of the time it is not filled and left as NaN.
                option.PriorClose = (float)newPriorClose;

            // Do not want to see ugly "NaN" values on the UI, because that catches the eye too quickly. Better to send the client "-1". That is known that it is impossible value for PriorClose, EstPrice
            // Treat EstPrice = "-1.00" as error, as NaN. Not available data. Then, we can use the PriorClose as EstPrice. That solves everything. (On the UI the P&L Today will be 0 at these lines. Fine.)
            float proposedEstValue;
            if (double.IsNaN(mktData.EstPrice) || mktData.EstPrice == -1.0) // If EstValue is not given by IB, use the PriorClose, but that can also be NaN
                proposedEstValue = (float)mktData.PriorClosePrice * option.Multiplier;
            else
                proposedEstValue = (float)mktData.EstPrice * option.Multiplier;

            if (!float.IsNaN(proposedEstValue)) // If it is not given by IB (either by PriorClose or EstPrice), don't overwrite current value by NaN.
                option.EstValue = proposedEstValue;

            option.IbCompDelta = mktData.IbComputedDelta;
        }
    }
}