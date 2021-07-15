using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using SqCommon;
using System.Threading.Tasks;
using System.Globalization;
using BrokerCommon;
using System.Diagnostics;

namespace FinTechCommon
{
    public class AssetLastClose
    {
        public Asset Asset { get; set; }
        public DateTime Date { get; set; } = DateTime.MinValue;
        public float SdaLastClose { get; set; } = 0;

        public AssetLastClose(Asset p_asset, DateTime p_dateTime, float p_sdaLastClose)
        {
            Asset = p_asset;
            Date = p_dateTime;
            SdaLastClose = p_sdaLastClose;
        }
    }

    public partial class MemDb
    {


        // ************** Helper methods for functions that are used frequently in the code-base
        // p_dateExclET: not UTC, but ET. The date of the query. If p_dateExclET = Monday, then LastCloseDate will be previous Friday.
        // Imagine we want to find the LastClose price which was valid on day p_dateExclET
        // Different assets can have different LastCloseDates. If there was an EU holiday on Friday, then the EU stock's LastClose is Thursday, while an USA stock LastClose is Friday
        // keep the order and the length of p_assets list. So, it can be indexed. p_assets[i] is the same item as result[i]
        // If an asset has no history, we return NaN as lastClose price.
        // This requre more RAM than the other solution which only returns the filled rows, but it will save CPU time later, when the result is processed at the caller. He don't have to do double FORs to iterate.
        public IEnumerable<AssetLastClose> GetSdaLastCloses(DateTime p_dateExclET, IEnumerable<Asset> p_assets)
        {
            DateOnly lookbackEnd = p_dateExclET.Date.AddDays(-1); // if (p_dateExclET is Monday), -1 days is Sunday, but we have to find Friday before

            TsDateData<DateOnly, uint, float, uint> histData = DailyHist.GetDataDirect();
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
            Debug.WriteLine($"MemDb.GetSdaLastCloses().EndDate: {dates[iEndDay]}");

            var lastCloses = p_assets.Select(r =>
            {
                if (!histData.Data.TryGetValue(r.AssetId, out Tuple<Dictionary<TickType, float[]>, Dictionary<TickType, uint[]>>? assetHist))
                    return new AssetLastClose(r, DateTime.MinValue, float.NaN);  // the Asset might be in MemDb, but has no history at all.

                float[] sdaCloses = assetHist.Item1[TickType.SplitDivAdjClose];
                // If there was an EU holiday on Friday, then the EU stock's LastClose is Thursday, while an USA stock LastClose is Friday
                int j = iEndDay;
                do
                {
                    float lastClose = sdaCloses[j];
                    if (!float.IsNaN(lastClose)) {
                        return new AssetLastClose(r, dates[j], lastClose);
                    }
                    j++;
                } while (j < dates.Length);
                return new AssetLastClose(r, DateTime.MinValue, float.NaN);

            });

            return lastCloses;
        }

    }

}