using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json.Serialization;
using Fin.Base;
using QuantConnect;
using QuantConnect.Parameters;
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

public class PrtfRunResult // this is sent to clients PrtfMgr and PrtfVwr
{
    public PortfolioRunResultStatistics Pstat { get; set; } = new();
    public ChartData ChrtData { get; set; } = new();
    public List<PortfolioPosition> PrtfPoss { get; set; } = new();
    public List<SqLog> Logs { get; set; } = new();
}

public class ChartData
{
    public ChartResolution ChartResolution { get; set; } = ChartResolution.Daily;
    public string DateTimeFormat { get; set; } = "YYYYMMDD";  // "SecSince1970", "YYYYMMDD", "DaysFrom<YYYYDDMM>"
    public List<long> Dates { get; set; } = new List<long>();

    // PV values are usually $100,000, $100,350,..., but PV can be around $1000 too and in that case decimals become important and we cannot use 'int' for storing them.
    // So we have to use either decimal (16 bytes) or float (4 byte) or double (8 bytes)
    // We don't like decimal (16 bytes), but the reason to keep it is that QC gives decimal values. Converting it to int or float requires some CPU conversion. But we should do it. For saving RAM. (20KB float List instead of the 80KB decimal List)
    // If we use float (4 bytes), we should use the FloatJsonConverterToNumber4D attribute for generating maximum 4 decimals: (100.342222225 decimal => float 100.3422)
    [JsonConverter(typeof(FloatListJsonConverterToNumber4D))]
    public List<float> Values { get; set; } = new List<float>(); // if 7 digit mantissa precision is enough then use float, otherwise double. Float:  if PV values of 10M = 10,000,000 we don't have any decimals, which is fine usually
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

        var histDataData = histData.Data; // get pointer once, as it will be used many times
        var priorCloses = p_assets.Select(r =>
        {
            if (!histDataData.TryGetValue(r.AssetId, out Tuple<Dictionary<TickType, float[]>, Dictionary<TickType, uint[]>>? assetHist))
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

    internal static void PushHistSdaPriorClosesToAssets(MemData p_memData, List<Asset> p_assetsNeedDailyHist)
    {
        SqDateOnly lookbackEnd = DateTime.UtcNow.FromUtcToEt().Date.AddDays(-1); // if (Now is Monday), -1 days is Sunday, but we have to find Friday before

        TsDateData<SqDateOnly, uint, float, uint> histData = p_memData.DailyHist.GetDataDirect();
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
        Debug.WriteLine($"MemDb.PushHistSdaPriorClosesToAssets().EndDate: {dates[iEndDay]}");

        var histDataData = histData.Data; // get pointer once, as it will be used many times
        foreach (Asset asset in p_assetsNeedDailyHist)
        {
            asset.PriorClose = float.NaN;
            if (!histDataData.TryGetValue(asset.AssetId, out Tuple<Dictionary<TickType, float[]>, Dictionary<TickType, uint[]>>? assetHist))
                continue;  // the Asset might be in MemDb, but has no history at all.

            float[] sdaCloses = assetHist.Item1[TickType.SplitDivAdjClose];
            // If there was an EU holiday on Friday, then the EU stock's PriorClose is Thursday, while an USA stock PriorClose is Friday
            int j = iEndDay;
            do
            {
                float priorClose = sdaCloses[j];
                if (!float.IsNaN(priorClose))
                {
                    asset.PriorClose = priorClose;
                    break;
                }
                j++;
            }
            while (j < dates.Length);
        }
    }

    // Not really necessary function, because PriorClose can be queried directly from Asset
    public static IEnumerable<AssetPriorClose> GetSdaPriorClosesFromAssetPriorClose(IEnumerable<Asset> p_assets)
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

        foreach (Asset asset in p_assets)
        {
            if (!histData.Data.TryGetValue(asset.AssetId, out Tuple<Dictionary<TickType, float[]>, Dictionary<TickType, uint[]>>? assetHistData))
            {
                Utils.Logger.Error($"MemDb.GetSdaHistCloses().Asset is missing from histData: {asset.SqTicker}");
                continue;   // the Asset might be in MemDb, but has no history at all.
            }
            float[] sdaCloses = assetHistData.Item1[TickType.SplitDivAdjClose];
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
            AssetHist hist = new(asset, periodStartDateInc, periodEndDateInc, stat, values);
            yield return hist;
        }
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
            MemDb.gMemDb.DownloadLastPrice(p_assets.ToArray()).TurnAsyncToSyncTask();
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

        foreach (Asset asset in p_assets)
        {
            if (!histData.Data.TryGetValue(asset.AssetId, out Tuple<Dictionary<TickType, float[]>, Dictionary<TickType, uint[]>>? assetHistData))
            {
                Utils.Logger.Error($"MemDb.GetSdaHistCloses().Asset is missing from histData: {asset.SqTicker}");
                continue;   // the Asset might be in MemDb, but has no history at all.
            }
            float[] sdaCloses = assetHistData.Item1[TickType.SplitDivAdjClose];

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

            DateTime estValueTimeLoc = asset.EstValueTimeLoc;   // the priceHistory is in Local time zone. We have to convert real time DateTime UTC to local too
            if (asset.EstValueTimeLoc != DateTime.MinValue) // DateTime.MinValue indicates that it never had real-time price
            {
                SqDateOnly estValueDateLoc = new(estValueTimeLoc.Date);
                // Premarket and regular market hours: adds a new date into the result list
                // After-market hours: avoid duplication. Don't add a new record, but overwrite the last one in history (if date matches). (check this whether the last Date in the list is the same as the realtime date)
                bool isOverwriteLastValue = (result.Count > 0) && (result[^1].Date == estValueDateLoc);
                if (isOverwriteLastValue)
                    result[^1].SdaValue = asset.EstValue;
                else
                    result.Add(new AssetHistValue() { Date = estValueDateLoc, SdaValue = asset.EstValue });
            }
            yield return (asset, result);
        }
    }

    public Trade? GetPortfolioTrade(int p_tradeHistoryId, int p_tradeId)
    {
        IEnumerable<Trade> tradeHistory = GetPortfolioTradeHistory(p_tradeHistoryId, null, null);
        foreach (Trade trade in tradeHistory)
        {
            if (trade.Id == p_tradeId)
                return trade;
        }
        return null;
    }

    public int InsertPortfolioTrade(int p_tradeHistoryId, Trade p_newTrade)
    {
        List<Trade>? tradeHistory = GetPortfolioTradeHistoryToList(p_tradeHistoryId, null, null); // assume tradeHistory is ordered by Trade.Time
        if (tradeHistory == null) // if id didn't exist before, create it and add the first item
            tradeHistory = new();

        int maxId = -1;
        foreach (Trade trade in tradeHistory)
        {
            if (trade.Id >= maxId)
                maxId = trade.Id;
        }
        int newTradeId = maxId + 1; // if tradeHistory is empty, maxId stays -1, newTradeId becomes 0. OK.
        p_newTrade.Id = newTradeId;

        // Slow approach: Add newTrade to the end of the list, then resort the whole list. If there are 5,000 trades, sorting takes a lot of time. For a list of 1000 items, sort takes 1000*1000= 1M steps.
        // tradeHistory.Add(p_newTrade);
        // tradeHistory = ReSortTradeHistoryByTime(tradeHistory);

        // Fast approach: Assume that the list is already sorted. We don't resort it. Find the index of insertion with BinarySearch(). For a list of 1000 items, BinarySearch takes log2(1000)= 8 steps. Then insert it at that index.
        int index = tradeHistory.BinarySearch(p_newTrade, new TradeComparer()); // return the index of an item if that p_newTrade.Time already in the list, or a negative number that is the bitwise complement of the index of the first element that is larger than p_newTrade.Time
        if (index < 0) // If it is negative, we have to do the bitwise complement to get the positive index.
            index = ~index; // This will give the correct index for insertion. This will be the index of the first element that is larger than p_newTrade.Time
        tradeHistory.Insert(index, p_newTrade);

        UpdatePortfolioTradeHistory(p_tradeHistoryId, tradeHistory, false); // no need to p_forceChronologicalOrder = true since we kept the previous order
        return newTradeId;
    }

    public bool DeletePortfolioTrade(int p_tradeHistoryId, int p_tradeId)
    {
        List<Trade>? tradeHistory = GetPortfolioTradeHistoryToList(p_tradeHistoryId, null, null); // assume tradeHistory is ordered by Trade.Time
        if (tradeHistory == null) // if id didn't exist before, that is unexpected. Raise an exception.
            throw new Exception($"DeletePortfolioTrade(), cannot find tradeHistoryId {p_tradeHistoryId}");

        for (int i = 0; i < tradeHistory.Count; i++)
        {
            if (tradeHistory[i].Id == p_tradeId)
            {
                tradeHistory.RemoveAt(i);
                UpdatePortfolioTradeHistory(p_tradeHistoryId, tradeHistory, false); // no need to p_forceChronologicalOrder = true since we kept the previous order
                return true;
            }
        }
        throw new Exception($"DeletePortfolioTrade(), cannot find tradeHistoryId {p_tradeHistoryId}");
    }

    public bool UpdatePortfolioTrade(int p_tradeHistoryId, int p_tradeId, Trade p_newTrade)
    {
        List<Trade>? tradeHistory = GetPortfolioTradeHistoryToList(p_tradeHistoryId, null, null); // assume tradeHistory is ordered by Trade.Time
        if (tradeHistory == null) // if id didn't exist before, that is unexpected. Raise an exception.
            throw new Exception($"UpdatePortfolioTrade(), cannot find tradeHistoryId {p_tradeHistoryId}");

        for (int i = 0; i < tradeHistory.Count; i++)
        {
            if (tradeHistory[i].Id == p_tradeId)
            {
                bool isResortByTimeNeeded = tradeHistory[i].Time != p_newTrade.Time; // if only e.g. the Price changed, but the Time didn't change, then we don't need the costly sort. O(N^2)
                tradeHistory[i] = p_newTrade;
                tradeHistory[i].Id = p_tradeId;
                if (isResortByTimeNeeded)
                    tradeHistory.Sort((a, b) => a.Time.CompareTo(b.Time));
                UpdatePortfolioTradeHistory(p_tradeHistoryId, tradeHistory, false); // no need to p_forceChronologicalOrder = true since we kept the previous order
                return true;
            }
        }
        throw new Exception($"UpdatePortfolioTrade(), cannot find tradeHistoryId {p_tradeHistoryId}");
    }

    // It is OK that we don't keep TradeHistories in RAM. It is intentional that we always reload the TradeHistory from RedisDb whenever somebody asks for it.
    // First, not having large RAM footprint is a faster WebServer program.
    // Second, this is only needed rarely. So it doesn't matter too much.
    // Third, the difference of getting it from Redis is not too high. From Linux server app to Linux RedisDb, it takes only 1ms (or 12 msec the first time because of C# Reflection serialization).
    // Fourth, we don't have to worry about whether it was changed during the last 2 hours, while WebServer didn't synch with RedisDb.
    public IEnumerable<Trade> GetPortfolioTradeHistory(int p_tradeHistoryId, DateTime? p_startIncLoc, DateTime? p_endIncLoc)
    {
        return m_Db.GetPortfolioTradeHistory(p_tradeHistoryId, p_startIncLoc, p_endIncLoc);
    }

    public List<Trade>? GetPortfolioTradeHistoryToList(int p_tradeHistoryId, DateTime? p_startIncLoc, DateTime? p_endIncLoc)
    {
        return m_Db.GetPortfolioTradeHistoryToList(p_tradeHistoryId, p_startIncLoc, p_endIncLoc); // assume tradeHistory is ordered by Trade.Time
    }

    public int InsertPortfolioTradeHistory(List<Trade> p_trades)
    {
        return m_Db.InsertPortfolioTradeHistory(p_trades);
    }

    public void DeletePortfolioTradeHistory(int p_tradeHistoryId)
    {
        m_Db.DeletePortfolioTradeHistory(p_tradeHistoryId);
    }

    public void UpdatePortfolioTradeHistory(int p_tradeHistoryId, List<Trade> p_trades, bool p_forceChronologicalOrder) // Update, OK.
    {
        m_Db.UpdatePortfolioTradeHistory(p_tradeHistoryId, p_trades, p_forceChronologicalOrder);
    }

    public void AppendPortfolioTradeHistory(int p_tradeHistoryId, List<Trade> p_newTrades, bool p_forceChronologicalOrder) // Helper
    {
        m_Db.AppendPortfolioTradeHistory(p_tradeHistoryId, p_newTrades, p_forceChronologicalOrder);
    }

    public void ReSortByTimePortfolioTradeHistory(int p_tradeHistoryId) // Helper: if we notice that a trade history is not ordered by time, we can call this function to fix it in RedisDb
    {
        List<Trade>? trades = m_Db.GetPortfolioTradeHistoryToList(p_tradeHistoryId, null, null);
        if (trades == null)
            return;
        m_Db.AppendPortfolioTradeHistory(p_tradeHistoryId, trades, true); // p_forceChronologicalOrder = true
    }

    public string? GetPortfolioRunResults(int p_portfolioId, DateTime? p_forcedStartTimeUtc, DateTime? p_forcedEndTimeUtc, out PrtfRunResult prtfRunResult)
    {
        prtfRunResult = new PrtfRunResult();
        // Step1: Getting the BackTestResults
        string? errMsg = null;
        if (MemDb.gMemDb.Portfolios.TryGetValue(p_portfolioId, out Portfolio? prtf))
            Console.WriteLine($"Portfolio Name: '{prtf.Name}'");
        else
            errMsg = $"Error. Portfolio id {p_portfolioId} not found in DB";

        if (errMsg == null)
        {
            bool returnOnlyTwrPv = true;
            errMsg = prtf!.GetPortfolioRunResult(returnOnlyTwrPv, SqResultStat.SqSimpleStat, p_forcedStartTimeUtc, p_forcedEndTimeUtc, out PortfolioRunResultStatistics stat, out List<DateValue> pv, out List<PortfolioPosition> prtfPos, out ChartResolution chartResolution, out List<SqLog> sqLogs);
            if (errMsg == null)
            {
                // Step2: Filling the ChartPoint Dates and Values to a list. A very condensed format. Dates are separated into its ChartDate List.
                // Instead of the longer [{"ChartDate": 1641013200, "Value": 101665}, {"ChartDate": 1641013200, "Value": 101665}, {"ChartDate": 1641013200, "Value": 101665}]
                // we send a shorter: { ChartDate: [1641013200, 1641013200, 1641013200], Value: [101665, 101665, 101665] }
                ChartData chartVal = new();
                chartVal.ChartResolution = chartResolution;
                DateTime startDate = DateTime.MinValue;
                DateTimeFormat dateTimeFormat;
                if (chartResolution == ChartResolution.Daily)
                {
                    dateTimeFormat = DateTimeFormat.DaysFromADate;
                    startDate = pv[0].Date.Date;
                    chartVal.DateTimeFormat = "DaysFrom" + startDate.ToYYYYMMDD(); // the standard choice in Production. It results in less data to be sent. Date strings will be only numbers such as 0,1,2,3,4,5,8 (skipping weekends)

                    // dateTimeFormat = DateTimeFormat.YYYYMMDD;
                    // chartVal.DateTimeFormat = "YYYYMMDD"; // YYYYMMDD is a better choice if we debug data sending. (to see it in the TXT message. Or to easily convert it to CSV in Excel)
                }
                else
                {
                    dateTimeFormat = DateTimeFormat.SecSince1970;
                    chartVal.DateTimeFormat = "SecSince1970"; // if it is higher resolution than daily, then we use per second resolution for data
                }

                foreach (DateValue item in pv)
                {
                    DateTime itemDate = item.Date.Date;

                    if (dateTimeFormat == DateTimeFormat.SecSince1970)
                    {
                        long unixTimeInSec = new DateTimeOffset(item.Date).ToUnixTimeSeconds();
                        chartVal.Dates.Add(unixTimeInSec);
                    }
                    else if (dateTimeFormat == DateTimeFormat.YYYYMMDD)
                    {
                        int dateInt = itemDate.Year * 10000 + itemDate.Month * 100 + itemDate.Day;
                        chartVal.Dates.Add(dateInt);
                    }
                    else // dateTimeFormat == DateTimeFormat.DaysFromADate
                    {
                        int nDaysFromStartDate = (int)(itemDate - startDate).TotalDays; // number of days since startDate
                        chartVal.Dates.Add(nDaysFromStartDate);
                    }

                    if (returnOnlyTwrPv)
                        chartVal.Values.Add((float)Math.Round(item.Value, 2)); // if we create a TWR chart starting from 100.0, then reduce float to 2 decimals to reduce JSON file size.
                    else
                        chartVal.Values.Add((int)item.Value); // To reduce JSON data size, if PV is RawPV, it is in USD, and usually they are big values like 100,000. Ignore decimal digits.
                }

                // Step3: Filling the Stats data
                PortfolioRunResultStatistics pStat = new()
                {
                    StartPortfolioValue = stat.StartPortfolioValue,
                    EndPortfolioValue = stat.EndPortfolioValue,
                    TotalReturn = stat.TotalReturn,
                    CAGR = stat.CAGR,
                    MaxDD = stat.MaxDD,
                    Sharpe = stat.Sharpe,
                    CagrSharpe = stat.CagrSharpe,
                    StDev = stat.StDev,
                    Ulcer = stat.Ulcer,
                    TradingDays = stat.TradingDays,
                    NTrades = stat.NTrades,
                    WinRate = stat.WinRate,
                    LossRate = stat.LossRate,
                    Sortino = stat.Sortino,
                    Turnover = stat.Turnover,
                    LongShortRatio = stat.LongShortRatio,
                    Fees = stat.Fees,
                    BenchmarkCAGR = stat.BenchmarkCAGR,
                    BenchmarkMaxDD = stat.BenchmarkMaxDD,
                    CorrelationWithBenchmark = stat.CorrelationWithBenchmark
                };

                // Step4: Filling the PrtfPoss data
                List<PortfolioPosition> prtfPoss = new();
                foreach (var item in prtfPos)
                {
                    prtfPoss.Add(new PortfolioPosition { SqTicker = item.SqTicker, Quantity = item.Quantity, AvgPrice = item.AvgPrice, BacktestLastPrice = item.BacktestLastPrice, EstPrice = item.EstPrice });
                }

                // Step5: Filling the Stats, ChartPoint vals, prtfPoss and logs in pfRunResults
                prtfRunResult = new()
                {
                    Pstat = pStat,
                    ChrtData = chartVal,
                    PrtfPoss = prtfPoss,
                    Logs = sqLogs
                };
            }
        }
        return errMsg;
    }
}