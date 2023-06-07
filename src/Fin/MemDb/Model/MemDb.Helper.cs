using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using SqCommon;

namespace Fin.MemDb;

public class AssetPriorClose
{
    public Asset Asset { get; set; }
    public DateTime Date { get; set; } = DateTime.MinValue;
    public float SdaPriorClose { get; set; } = 0;

    public AssetPriorClose(Asset p_asset, DateTime p_dateTime, float p_sdaPriorClose)
    {
        Asset = p_asset;
        Date = p_dateTime;
        SdaPriorClose = p_sdaPriorClose;
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
    public SqDateOnly Date { get; set; }
    public float SdaValue { get; set; }
}

public class AssetHistStat // this is sent to clients usually just once per day, OR when historical data changes, OR when the Period changes at the client
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
    // The date of the query. If p_dateExclLoc = Monday, then PriorCloseDate will be previous Friday.
    // Imagine we want to find the PriorClose price which was valid on day p_dateExclLoc
    // Different assets can have different PriorCloseDates. If there was an EU holiday on Friday, then the EU stock's PriorClose is Thursday, while an USA stock PriorClose is Friday
    // keep the order and the length of p_assets list. So, it can be indexed. p_assets[i] is the same item as result[i]
    // If an asset has no history, we return NaN as PriorClose price.
    // This requre more RAM than the other solution which only returns the filled rows, but it will save CPU time later, when the result is processed at the caller. He don't have to do double FORs to iterate.
    public IEnumerable<AssetPriorClose> GetSdaPriorClosesFromHist(IEnumerable<Asset> p_assets, DateTime p_dateExclLoc /* usually given as current time today */)
    {
        SqDateOnly lookbackEnd = p_dateExclLoc.Date.AddDays(-1); // if (p_dateExclLoc is Monday), -1 days is Sunday, but we have to find Friday before

        TsDateData<SqDateOnly, uint, float, uint> histData = DailyHist.GetDataDirect();
        SqDateOnly[] dates = histData.Dates;
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
        Debug.WriteLine($"MemDb.GetSdaPriorClosesFromHist().EndDate: {dates[iEndDay]}");

        var priorCloses = p_assets.Select(r =>
        {
            if (!histData.Data.TryGetValue(r.AssetId, out Tuple<Dictionary<TickType, float[]>, Dictionary<TickType, uint[]>>? assetHist))
                return new AssetPriorClose(r, DateTime.MinValue, float.NaN);  // the Asset might be in MemDb, but has no history at all.

            float[] sdaCloses = assetHist.Item1[TickType.SplitDivAdjClose];
            // If there was an EU holiday on Friday, then the EU stock's PriorClose is Thursday, while an USA stock PriorClose is Friday
            int j = iEndDay;
            do
            {
                float priorClose = sdaCloses[j];
                if (!float.IsNaN(priorClose))
                    return new AssetPriorClose(r, dates[j], priorClose);

                j++;
            }
            while (j < dates.Length);
            return new AssetPriorClose(r, DateTime.MinValue, float.NaN);
        });

        return priorCloses;
    }

    // Not really necessary function, because PriorClose can be queried directly from Asset
    public static IEnumerable<AssetPriorClose> GetSdaPriorCloses(IEnumerable<Asset> p_assets)
    {
        DateTime mockupPriorDate = DateTime.UtcNow.Date.AddDays(-1); // we get PriorClose from Asset directly. That comes from YF, which don't tell us the date of PriorClose
        var priorCloses = p_assets.Select(r =>
        {
            return new AssetPriorClose(r, mockupPriorDate, r.PriorClose);
        });

        return priorCloses;
    }

    // Backtests don't need the Statistical data (maxDD), just the prices. Dashboard.MarketHealth only needs the Statistical data, no historical prices. Dashboard.BrAccViewer needs both.
    public IEnumerable<AssetHist> GetSdaHistCloses(IEnumerable<Asset> p_assets, DateTime p_startIncLoc, DateTime p_endExclLoc /* usually given as current time today, and that today should not be included in the returned data */,
        bool p_valuesNeeded, bool p_statNeeded)
    {
        // p_endExclLoc is usually Today. It should NOT be in the returned result. E.g. if (p_dateExclLoc is Monday), -1 days is Sunday, but we have to find Friday before, or even Thursday if Friday was a holiday.
        // The caller code usually have no idea about these weekend or holiday days. The caller just gives Today as an EndDate. It would be too difficult to calculate the proper EndDate in the caller code.
        SqDateOnly lookbackEnd = p_endExclLoc.Date.AddDays(-1); // Double checked. Don't change this. See comment above.

        TsDateData<SqDateOnly, uint, float, uint> histData = DailyHist.GetDataDirect();
        SqDateOnly[] dates = histData.Dates;

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
        if (dates.Length != 0)
            Debug.WriteLine($"MemDb.GetSdaHistCloses().EndDate: {dates[iEndDay]}");

        int iStartDay = histData.IndexOfKeyOrAfter(new SqDateOnly(p_startIncLoc));      // the valid price at the weekend is the one on the previous Friday. After.
        if (iStartDay == -1 || iStartDay >= dates.Length) // If not found then fix the startDate as the first available date of history.
        {
            iStartDay = dates.Length - 1;
        }
        if (dates.Length != 0)
            Debug.WriteLine($"MemDb.GetSdaHistCloses().StartDate: {dates[iStartDay]}");

        IEnumerable<AssetHist> assetHists = p_assets.Select(r =>
        {
            float[] sdaCloses = histData.Data[r.AssetId].Item1[TickType.SplitDivAdjClose];
            // if startDate is not found, because e.g. we want to go back 3 years, while stock has only 2 years history
            int iiStartDay = (iStartDay < sdaCloses.Length) ? iStartDay : sdaCloses.Length - 1;
            if (Single.IsNaN(sdaCloses[iiStartDay]) // if that date in the global MemDb was an USA stock market holiday (e.g. President days is on monday), price is NaN for stocks, but valid value for NAV
                && ((iiStartDay + 1) <= sdaCloses.Length))
                iiStartDay++;   // that start 1 day earlier. It is better to give back more data, then less. Besides on that holiday day, the previous day price is valid.

            List<AssetHistValue>? values = p_valuesNeeded ? new List<AssetHistValue>() : null;

            // reverse marching from yesterday into past is not good, because we have to calculate running maxDD, maxDU.
            float max = float.MinValue, min = float.MaxValue, maxDD = float.MaxValue, maxDU = float.MinValue;
            int iStockEndDay = Int32.MinValue, iStockFirstDay = Int32.MinValue;
            for (int i = iiStartDay; i >= iEndDay; i--) // iEndDay is index 0 or 1. Reverse marching from yesterday iEndDay to deeper into the past. Until startdate iStartDay or until history beginning reached
            {
                float val = sdaCloses[i];
                if (Single.IsNaN(val))
                    continue;   // if that date in the global MemDb was an USA stock market holiday (e.g. President days is on monday), price is NaN for stocks, but valid value for NAV
                if (iStockFirstDay == Int32.MinValue)
                    iStockFirstDay = i;
                iStockEndDay = i;

                if (p_valuesNeeded && values != null)
                    values.Add(new AssetHistValue() { Date = dates[i], SdaValue = val });

                if (val > max)
                    max = val;
                if (val < min)
                    min = val;
                float dailyDD = val / max - 1;     // -0.1 = -10%. daily Drawdown = how far from High = loss felt compared to Highest
                if (dailyDD < maxDD) // dailyDD are a negative values, so we should do MIN-search to find the Maximum negative value
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
            AssetHist hist = new(r, periodStartDateInc, periodEndDateInc, stat, values);
            return hist;
        });

        return assetHists;
    }

    // Called by Strategies (UberTaa,RenewedUber,Sin, VolDragVisualizer) for latest, best prices. It has to assure that the real-time prices are not too old.
    public IEnumerable<(Asset Asset, List<AssetHistValue> Values)> GetSdaHistClosesAndLastEstValue(IEnumerable<Asset> p_assets, DateTime p_startIncLoc, bool p_makeRtLastValueUptodate = false)
    {
        if (p_makeRtLastValueUptodate) // if older than 30minutes realtime prices are sufficient, or if caller is sure that RT prices are sufficiently up-to-date (for example handled in HighFrequencyTimer) then don't need to spend another 45msec here.
        {
            // Before getting historical and RT prices from MemDb, we can force to update RT prices in MemDb.
            // Reason: LowFrequency RT update happens only in every 30 minutes. That is too old data, because this SIN page can be used for manual trading instruction.
            // But we don't want to update the RT prices for all of these 5-30 assets every time with 5 seconds frequency.
            // It would be unnecessary if a webapp report is used only once per month. At the end of the month rebalancing trading.
            // Therefore, we force the RT price update for only these 30 assets on Demand. When this page is accessed. It requires another 45msec, so it is slower, but it would be unnecessary to refresh all the universe ticker Rt prices all the time.
            // Update the RT prices of only those 30 stocks (45ms) that are in the SIN portfolio. Don't need to update all the 700 (later 2000) stocks in MemDb, that is done automatically by RtTimer in every 30min
            MemDb.DownloadPriorCloseAndLastPrice(p_assets.ToArray()).TurnAsyncToSyncTask();
        }

        TsDateData<SqDateOnly, uint, float, uint> histData = DailyHist.GetDataDirect();
        SqDateOnly[] dates = histData.Dates;

        // At 16:00, or even intraday: YF gives even the today last-realtime price with a today-date. We have to find any date backwards, which is NOT today. That is the PreviousClose.
        int iEndDay = 0;

        int iStartDay = histData.IndexOfKeyOrAfter(new SqDateOnly(p_startIncLoc));      // the valid price at the weekend is the one on the previous Friday. After.
        if (iStartDay == -1 || iStartDay >= dates.Length) // If not found then fix the startDate as the first available date of history.
        {
            iStartDay = dates.Length - 1;
        }
        Debug.WriteLine($"MemDb.GetSdaHistCloses().StartDate: {dates[iStartDay]}");

        IEnumerable<(Asset Asset, List<AssetHistValue> Values)> assetHistsAndLastEstValue = p_assets.Select(r =>
        {
            float[] sdaCloses = histData.Data[r.AssetId].Item1[TickType.SplitDivAdjClose];
            // if startDate is not found, because e.g. we want to go back 3 years, while stock has only 2 years history
            int iiStartDay = (iStartDay < sdaCloses.Length) ? iStartDay : sdaCloses.Length - 1;
            if (Single.IsNaN(sdaCloses[iiStartDay]) // if that date in the global MemDb was an USA stock market holiday (e.g. President days is on monday), price is NaN for stocks, but valid value for NAV
                && ((iiStartDay + 1) <= sdaCloses.Length))
                iiStartDay++;   // that start 1 day earlier. It is better to give back more data, then less. Besides on that holiday day, the previous day price is valid.

            List<AssetHistValue> result = new();

            // reverse marching from yesterday into past is not good, because we have to calculate running maxDD, maxDU.
            int iStockEndDay = Int32.MinValue, iStockFirstDay = Int32.MinValue;
            for (int i = iiStartDay; i >= iEndDay; i--) // iEndDay is index 0 or 1. Reverse marching from yesterday iEndDay to deeper into the past. Until startdate iStartDay or until history beginning reached
            {
                float val = sdaCloses[i];
                if (Single.IsNaN(val))
                    continue;   // if that date in the global MemDb was an USA stock market holiday (e.g. President days is on monday), price is NaN for stocks, but valid value for NAV
                if (iStockFirstDay == Int32.MinValue)
                    iStockFirstDay = i;
                iStockEndDay = i;

                result.Add(new AssetHistValue() { Date = dates[i], SdaValue = val });
            }

            DateTime estValueTimeLoc = r.EstValueTimeLoc;   // the priceHistory is in Local time zone. We have to convert real time DateTime UTC to local too
            if (r.EstValueTimeLoc != DateTime.MinValue) // DateTime.MinValue indicates that it never had real-time price
            {
                SqDateOnly estValueDateLoc = new(estValueTimeLoc.Date);
                // Premarket and regular market hours: adds a new date into the result list
                // After-market hours: avoid duplication. Don't add a new record, but overwrite the last one in history (if date matches). (check this whether the last Date in the list is the same as the realtime date)
                bool isOverwriteLastValue = (result.Count > 0) && (result[^1].Date == estValueDateLoc);
                if (isOverwriteLastValue)
                    result[^1].SdaValue = r.EstValue;
                else
                    result.Add(new AssetHistValue() { Date = estValueDateLoc, SdaValue = r.EstValue });
            }
            return (r, result);
        });

        return assetHistsAndLastEstValue;
    }
}