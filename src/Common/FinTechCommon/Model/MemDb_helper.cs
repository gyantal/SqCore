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

    public class AssetHist
    {
        public Asset Asset { get; set; }
        public DateTime PeriodStartDate { get; set; } = DateTime.MinValue;
        public DateTime PeriodEndDate { get; set; } = DateTime.MinValue;
        public List<AssetHistValue>? Values { get; set; } = null;
        public AssetHistStat? Stat { get; set; } = null;

        public AssetHist(Asset p_asset, DateTime p_startDateInc, DateTime p_endDateInc, AssetHistStat? p_stat, List<AssetHistValue>? p_values)
        {
            Asset = p_asset;
            Stat = p_stat;
            Values = p_values;
            PeriodStartDate = p_startDateInc;
            PeriodEndDate = p_endDateInc;
        }
    }

    public class AssetHistValue
    {
        public DateOnly Date { get; set; }
        public float SdaValue { get; set; }
    }

    public class AssetHistStat   // this is sent to clients usually just once per day, OR when historical data changes, OR when the Period changes at the client
    {
        public double PeriodStart { get; set; } = -100.0;
        public double PeriodEnd { get; set; } = -100.0;

        public double PeriodHigh { get; set; } = -100.0;
        public double PeriodLow { get; set; } = -100.0;
        public double PeriodMaxDD { get; set; } = -100.0;
        public double PeriodMaxDU { get; set; } = -100.0;
    }

    public partial class MemDb
    {

        // ************** Helper methods for functions that are used frequently in the code-base
        // p_dateExclLoc: not UTC, but ET (in USA stocks), CET (in EU stocks) or whatever is the local time at the exchange of the stock. 
        // The date of the query. If p_dateExclLoc = Monday, then LastCloseDate will be previous Friday.
        // Imagine we want to find the LastClose price which was valid on day p_dateExclLoc
        // Different assets can have different LastCloseDates. If there was an EU holiday on Friday, then the EU stock's LastClose is Thursday, while an USA stock LastClose is Friday
        // keep the order and the length of p_assets list. So, it can be indexed. p_assets[i] is the same item as result[i]
        // If an asset has no history, we return NaN as lastClose price.
        // This requre more RAM than the other solution which only returns the filled rows, but it will save CPU time later, when the result is processed at the caller. He don't have to do double FORs to iterate.
        public IEnumerable<AssetLastClose> GetSdaLastCloses(IEnumerable<Asset> p_assets, DateTime p_dateExclLoc /* usually given as current time today */)
        {
            DateOnly lookbackEnd = p_dateExclLoc.Date.AddDays(-1); // if (p_dateExclLoc is Monday), -1 days is Sunday, but we have to find Friday before

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

        // Backtests don't need the Statistical data (maxDD), just the prices. Dashboard.MarketHealth only needs the Statistical data, no historical prices
        public IEnumerable<AssetHist> GetSdaHistCloses(IEnumerable<Asset> p_assets, DateTime p_startIncLoc, DateTime p_endExclLoc /* usually given as current time today */,
            bool p_valuesNeeded, bool p_statNeeded)
        {
            DateOnly lookbackEnd = p_endExclLoc.Date.AddDays(-1); // if (p_dateExclLoc is Monday), -1 days is Sunday, but we have to find Friday before

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
            Debug.WriteLine($"MemDb.GetSdaHistCloses().EndDate: {dates[iEndDay]}");

            int iStartDay = histData.IndexOfKeyOrAfter(new DateOnly(p_startIncLoc));      // the valid price at the weekend is the one on the previous Friday. After.
            if (iStartDay == -1 || iStartDay >= dates.Length) // If not found then fix the startDate as the first available date of history.
            {
                iStartDay = dates.Length - 1;
            }
            Debug.WriteLine($"MemDb.GetSdaHistCloses().StartDate: {dates[iStartDay]}");

            IEnumerable<AssetHist> assetHists = p_assets.Select(r =>
            {
                float[] sdaCloses = histData.Data[r.AssetId].Item1[TickType.SplitDivAdjClose];
                // if startDate is not found, because e.g. we want to go back 3 years, while stock has only 2 years history
                int iiStartDay = (iStartDay < sdaCloses.Length) ? iStartDay : sdaCloses.Length - 1;
                if (Single.IsNaN(sdaCloses[iiStartDay]) // if that date in the global MemDb was an USA stock market holiday (e.g. President days is on monday), price is NaN for stocks, but valid value for NAV
                    && ((iiStartDay + 1) <= sdaCloses.Length))
                    iiStartDay++;   // that start 1 day earlier. It is better to give back more data, then less. Besides on that holiday day, the previous day price is valid.

                List<AssetHistValue>? values = (p_valuesNeeded) ? new List<AssetHistValue>() : null;

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

                    float val = sdaCloses[i];
                    if (p_valuesNeeded && values != null)
                        values.Add(new AssetHistValue() { Date = dates[i], SdaValue = val  });

                    if (val > max)
                        max = val;
                    if (val < min)
                        min = val;
                    float dailyDD = val / max - 1;     // -0.1 = -10%. daily Drawdown = how far from High = loss felt compared to Highest
                    if (dailyDD < maxDD)                        // dailyDD are a negative values, so we should do MIN-search to find the Maximum negative value
                        maxDD = dailyDD;                        // maxDD = maximum loss, pain felt over the period
                    float dailyDU = val / min - 1;     // daily DrawUp = how far from Low = profit felt compared to Lowest
                    if (dailyDU > maxDU)
                        maxDU = dailyDU;                        // maxDU = maximum profit, happiness felt over the period
                }

                // it is possible that both iStockFirstDay, iStockEndDay are left as Int32.MinValue, because there is no valid value at all in that range. Fine.
                AssetHistStat? stat = null;
                if (p_statNeeded)
                {
                    stat = new AssetHistStat()
                    {
                        PeriodStart = (iStockFirstDay >= 0) ? sdaCloses[iStockFirstDay] : Double.NaN,
                        PeriodEnd = (iStockEndDay >= 0) ? sdaCloses[iStockEndDay] : Double.NaN,
                        PeriodHigh = (max == float.MinValue) ? float.NaN : max,
                        PeriodLow = (min == float.MaxValue) ? float.NaN : min,
                        PeriodMaxDD = (maxDD == float.MaxValue) ? float.NaN : maxDD,
                        PeriodMaxDU = (maxDU == float.MinValue) ? float.NaN : maxDU
                    };

                }

                var periodStartDateInc = (iStockFirstDay >= 0) ? (DateTime)dates[iStockFirstDay] : DateTime.MaxValue;    // it may be not the 'asked' start date if asset has less price history
                var periodEndDateInc = (iStockEndDay >= 0) ? (DateTime)dates[iStockEndDay] : DateTime.MaxValue;        // by default it is the date of yesterday, but the user can change it
                AssetHist hist = new AssetHist(r, periodStartDateInc, periodEndDateInc, stat, values);
                return hist;
            });

            return assetHists;
        }

    }

}