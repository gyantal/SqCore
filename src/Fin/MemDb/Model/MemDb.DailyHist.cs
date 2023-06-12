using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using SqCommon;
using StackExchange.Redis;
using YahooFinanceApi;

namespace Fin.MemDb;

// DailyHist RAM requirement: 1Year = 260*(2+4) = 1560B = 1.5KB,  5y data is: 5*260*(2+4) = 7.8K
// Max RAM requirement if need only AdjClose: 20years for 5K stocks: 5000*20*260*(2+4) = 160MB (only one data per day: DivSplitAdjClose.)
// Max RAM requirement if need O/H/L/C/AdjClose/Volume: 6x of previous = 960MB = 1GB
// 2020-01 FinTimeSeries SumMem: 2+10+10+4*5 = 42 years. 42*260*(2+4)= 66KB.                                With 5000 stocks, 30years: 5000*260*30*(2+4)= 235MB
// 2020-05 CompactFinTimeSeries SumMem: 2+10+10+4*5 = 42 years. Date + data: 2*260*10+42*260*(4)= 48KB.     With 5000 stocks, 30years: 2*260*30+5000*260*30*(4)= 156MB (a saving of 90MB)
// { // to minimize mem footprint, only load the necessary dates (not all history). Because we would like to have 2008-09 market crash in history, start from 2005-01
//     new Asset() { AssetId = new AssetId32Bits(AssetType.Stock, 1), PrimaryExchange = ExchangeId.NYSE, LastTicker = "SPY", ExpectedHistorySpan="Date: 2004-12-31"},    // history starts on 1993-01-29. Full history would be: 44KB,
//     new Asset() { AssetId = new AssetId32Bits(AssetType.Stock, 2), PrimaryExchange = ExchangeId.NYSE, LastTicker = "QQQ", ExpectedHistorySpan="Date: 2004-12-31"},    // history starts on 1999-03-10. Full history would be: 32KB.       // 2010-01-01 Friday is NewYear holiday, first trading day is 2010-01-04
//     new Asset() { AssetId = new AssetId32Bits(AssetType.Stock, 3), PrimaryExchange = ExchangeId.NYSE, LastTicker = "TLT", ExpectedHistorySpan="15y"},                 // history starts on 2002-07-30
//     new Asset() { AssetId = new AssetId32Bits(AssetType.Stock, 4), PrimaryExchange = ExchangeId.NYSE, LastTicker = "VXX", ExpectedHistorySpan="Date: 2018-01-25"},    // history starts on 2018-01-25 on YF, because VXX was restarted. The previously existed VXX.B shares are not on YF.
//     new Asset() { AssetId = new AssetId32Bits(AssetType.Stock, 5), PrimaryExchange = ExchangeId.NYSE, LastTicker = "UNG", ExpectedHistorySpan="15y"},                 // history starts on 2007-04-18
//     new Asset() { AssetId = new AssetId32Bits(AssetType.Stock, 6), PrimaryExchange = ExchangeId.NYSE, LastTicker = "USO", ExpectedHistorySpan="15y"},                 // history starts on 2006-04-10
//     new Asset() { AssetId = new AssetId32Bits(AssetType.Stock, 7), PrimaryExchange = ExchangeId.NYSE, LastTicker = "GLD", ExpectedHistorySpan="15y"}                  // history starts on 2004-11-18
//     };

// 2021-04-22: DailyHist is reloaded 3 times per day. For 20 stocks it takes 4secs. That is fine.
// Even 100x that (2000 stocks), would take only 400sec = 6min. That still would be fine, because it only runs 3 times per day and
// it runs in a background thread, so main functionality is not effected.
// If 3 minutes of YF price downloading is slow, we can parallelize it on 3 threads. (YF probably will not ban us with 3 parallel downloads), that can decrease loading time by 1/3rd. But don't parallelize too much, to not risk IP banning.
// If we want to persist data (for cases when YF is unavailable): full YF crawls are backup up to SQL file (at least, we start to use the fileDb. We can keep there for 30 days, and do backup from that).
// If we have persisted SQL (or Redis) data, we can do similar to SnifferQuant: One fullCrawl before marketOpen + quick last-date query after market-close.
// But until the whole YF data download takes 5 minutes, this separation of the last day is not necessary.
public partial class MemDb
{
    Timer? m_historicalDataReloadTimer; // forced reload In ET time zone: 4:00ET, 9:00ET, 16:30ET.
    DateTime m_lastHistoricalDataReload = DateTime.MinValue; // UTC
    TimeSpan m_lastHistoricalDataReloadTs;  // YF downloads. For 12 stocks, it is 3sec. so for 120 stocks 30sec, for 600 stocks 2.5min, for 1200 stocks 5min

    void InitAndScheduleHistoricalTimer()
    {
        m_historicalDataReloadTimer = new System.Threading.Timer(new TimerCallback(ReloadHistoricalDataTimer_Elapsed), this, TimeSpan.FromMilliseconds(-1.0), TimeSpan.FromMilliseconds(-1.0));
        SetNextReloadHistDataTriggerTime();
    }

    public static async void ReloadHistoricalDataTimer_Elapsed(object? p_state) // Timer is coming on a ThreadPool thread
    {
        if (p_state == null)
            throw new Exception("ReloadHistoricalDataTimer_Elapsed() received null object.");

        await ((MemDb)p_state).ReloadHistDataAndSetNewTimer();
    }

    public async Task<StringBuilder> ForceReloadHistData(bool p_isHtml) // print log to Console or HTML
    {
        StringBuilder sb = new();
        await ReloadHistDataAndSetNewTimer();

        ServerDiagnosticMemDb(sb, p_isHtml);
        return sb;
    }

    async Task ReloadHistDataAndSetNewTimer()
    {
        await UpdateDailyHist(m_memData, m_Db, true);
        SetNextReloadHistDataTriggerTime();
    }

    // Called in Init_WT(), in ReloadDbDataIfChangedImpl() and in ReloadHistDataAndSetNewTimer() when polling for changes 3x every day
    internal async Task UpdateDailyHist(MemData p_memData, Db p_db, bool p_invokeHistReloadedEvent = false) // Create hist, but don't yet overwrite m_memData.DailyHist
    {
        DateTime startTime = DateTime.UtcNow;

        List<Asset> assetsNeedDailyHist = p_memData.AssetsCache.Assets.Where(r => (r.AssetId.AssetTypeID == AssetType.BrokerNAV) ||
            ((r.AssetId.AssetTypeID == AssetType.Stock || r.AssetId.AssetTypeID == AssetType.FinIndex) && (r.ExpectedHistoryStartDateLoc != DateTime.MaxValue))).ToList();   // out of 800 stocks, we only keep 20 history in MemDb

        var newDailyHist = await CreateDailyHist(p_memData, p_db, assetsNeedDailyHist);
        if (newDailyHist != null) // if it fails, keep the previous DailyHist, don't overwrite
        {
            p_memData.DailyHist = newDailyHist;  // swap pointer in atomic operation

            // Postprocess
            // 1. Asset.PriorClose are updated from the DailyHist, because real-time price API (e.g. IEX) usually gets only real-time prices, not PriorClose. Although YF RT query can get PriorClose too, but that is an exception.
            // Unfortunately, PriorClose is only filled for Historical assets (which is only 10% of the Assets), the rest of the Assets's PriorClose is not updated by this method.
            // So, it is questionable whether we need this method, because we have to query the RtDownloaders for PriorCloses anyway. We get PriorCloses in Rt-LowFreq timer for all assets.
            // Although leave this here, we need it, because Rt price service sometimes fully fail (either YF or Iex)
            PushHistSdaPriorClosesToAssets(p_memData, assetsNeedDailyHist);

            if (p_invokeHistReloadedEvent)
                EvOnlyHistoricalDataReloaded?.Invoke();
        }

        m_lastHistoricalDataReload = DateTime.UtcNow;
        m_lastHistoricalDataReloadTs = m_lastHistoricalDataReload - startTime;
        Console.WriteLine($"UpdateDailyHist() finished (#Assets: {p_memData.AssetsCache.Assets.Count}, #HistoricalAssets: {newDailyHist?.GetDataDirect().Data.Count ?? 0}) in {m_lastHistoricalDataReloadTs.TotalSeconds:0.000}sec");
    }

    // Called in Init_WT(), in ReloadDbDataIfChangedImpl() and in ReloadHistDataAndSetNewTimer() when polling for changes 3x every day
    // Historical data can partially come from our Redis-Sql DB or partially from YF
    internal static async Task<CompactFinTimeSeries<SqDateOnly, uint, float, uint>?> CreateDailyHist(MemData p_memData, Db p_db, List<Asset> p_assetsNeedDailyHist) // Create hist, but don't yet overwrite m_memData.DailyHist
    {
        Dictionary<AssetId32Bits, List<Split>> potentialMissingYfSplits = await GetPotentialMissingYfSplits(p_db, p_memData.AssetsCache);

        Utils.Logger.Info("CreateDailyHist() START");
        Console.WriteLine("*MemDb.DailyHist Download from YF starts.");
        try
        {
            var assetsDates = new Dictionary<uint, SqDateOnly[]>();
            var assetsAdjustedCloses = new Dictionary<uint, float[]>();
            foreach (var asset in p_assetsNeedDailyHist.Where(r => r.AssetId.AssetTypeID == AssetType.Stock || r.AssetId.AssetTypeID == AssetType.FinIndex))
            {
                (SqDateOnly[] dates, float[] adjCloses) = await CreateDailyHist_YF(asset, potentialMissingYfSplits);
                if (adjCloses.Length != 0)
                {
                    assetsDates[asset.AssetId] = dates;
                    assetsAdjustedCloses[asset.AssetId] = adjCloses;
                    Console.Write($"{asset.Symbol}, ");
                    Debug.Write($"{asset.SqTicker}, first: DateTime: {dates.First()}, Close: {adjCloses.First()}, last: DateTime: {dates.Last()}, Close: {adjCloses.Last()}");  // only writes to Console in Debug mode in vscode 'Debug Console'
                }
            }
            Console.WriteLine();    // after the last ticker, write an Enter.

            // NAV assets should be grouped by user, because we create a synthetic new aggregatedNAV. This aggregate should add up the RAW UnadjustedNAV (not adding up the adjustedNAV), so we have to create it at MemDbReload.
            var navAssets = p_assetsNeedDailyHist.Where(r => r.AssetId.AssetTypeID == AssetType.BrokerNAV).Select(r => (BrokerNav)r);
            var navAssetsByUser = navAssets.ToLookup(r => r.User); // ToLookup() uses User.Equals()

            foreach (IGrouping<User?, BrokerNav>? navAssetsOfUser in navAssetsByUser)
            {
                BrokerNav? aggNavAsset = navAssetsOfUser.First().AggregateNavParent;    // The first BrokerNav can contain it.
                CreateDailyHist_UserNavs(navAssetsOfUser, aggNavAsset, p_db, assetsDates, assetsAdjustedCloses);
            } // NAVs per user

            List<SqDateOnly> mergedDates = UnionAllDates(assetsDates);
            SqDateOnly[] mergedDatesArr = mergedDates.ToArray();
            var values = MergeCloses(assetsDates, assetsAdjustedCloses, mergedDatesArr);

            return new CompactFinTimeSeries<SqDateOnly, uint, float, uint>(mergedDatesArr, values);
        }
        catch (Exception e)
        {
            Utils.Logger.Error(e, "Exception in CreateDailyHist()");
            await HealthMonitorMessage.SendAsync($"Exception in SqCoreWebsite.C#.MemDb. Exception: '{e.ToStringWithShortenedStackTrace(1600)}'", HealthMonitorMessageID.SqCoreWebCsError);
        }
        return null;
    }

    private static Dictionary<uint, Tuple<Dictionary<TickType, float[]>, Dictionary<TickType, uint[]>>> MergeCloses(Dictionary<uint, SqDateOnly[]> assetsDates, Dictionary<uint, float[]> assetsAdjustedCloses, SqDateOnly[] mergedDatesArr)
    {
        var values = new Dictionary<uint, Tuple<Dictionary<TickType, float[]>, Dictionary<TickType, uint[]>>>();
        foreach (var closes in assetsAdjustedCloses)
        {
            var assetId = closes.Key;
            var secDatesArr = assetsDates[assetId];
            var iSecDates = secDatesArr.Length - 1; // set index to last item

            var closesArr = new List<float>(mergedDatesArr.Length); // allocate more, even though it is not necessarily filled
            for (int i = 0; i < mergedDatesArr.Length; i++)
            {
                if (secDatesArr[iSecDates] == mergedDatesArr[i])
                {
                    closesArr.Add(closes.Value[iSecDates]);
                    if (iSecDates <= 0) // if first (oldest) item is already used, thatis the last item.
                        break;
                    iSecDates--;
                }
                else
                {
                    // There is no USA stock price data on specific USA stock market holidays (e.g. 2020-09-07: Labour day).
                    // However, brokerNAV values, Forex, commodity futures, London-based stocks can have data on that day.
                    // We most certainly have to contain that date in the global MemDb.Data.
                    // We have a choice, however: leave the USA-stock value Float.NaN or flow the last-day price into this non-existent date.
                    // We choose the Float.NaN, because otherwise users of MemDb would have no chance to know that this was a day with a non-existent price.
                    closesArr.Add(float.NaN);   // in a very rare cases prices might be missing from the front, or from the middle (if stock didn't trade on that day)
                }
            }

            var dict1 = new Dictionary<TickType, float[]>() { { TickType.SplitDivAdjClose, closesArr.ToArray() } };
            var dict2 = new Dictionary<TickType, uint[]>(); // we don't store volume now, but maybe in the future
            values.Add(assetId, new Tuple<Dictionary<TickType, float[]>, Dictionary<TickType, uint[]>>(dict1, dict2));
        }

        return values;
    }

    private static List<SqDateOnly> UnionAllDates(Dictionary<uint, SqDateOnly[]> assetsDates)
    {
        // Merge all dates into a big date array
        // We don't know which days are holidays, so we have to walk all the dates simultaneously, and put into merged date array all the existing dates.
        // walk backward, because the first item will be the latest, the yesterday.
        Dictionary<uint, int> idx = new();  // AssetId => to index of the walking where we are
        SqDateOnly minDate = SqDateOnly.MaxValue, maxDate = SqDateOnly.MinValue;
        foreach (var dates in assetsDates) // assume first date is the oldest, last day is yesterday
        {
            if (minDate > dates.Value.First())
                minDate = dates.Value.First();
            if (maxDate < dates.Value.Last())
                maxDate = dates.Value.Last();
            idx.Add(dates.Key, dates.Value.Length - 1);
        }

        List<SqDateOnly> mergedDates = new();
        SqDateOnly currDate = maxDate; // this maxDate is today, or yesterday. We walk backwards.
        while (true)
        {
            mergedDates.Add(currDate);

            SqDateOnly largestPrevDate = SqDateOnly.MinValue;
            foreach (var dates in assetsDates)
            {
                if (idx[dates.Key] >= 0 && dates.Value[idx[dates.Key]] == currDate)
                {
                    idx[dates.Key]--;    // decrease index
                }

                if (idx[dates.Key] >= 0 && dates.Value[idx[dates.Key]] > largestPrevDate)
                    largestPrevDate = dates.Value[idx[dates.Key]];
            }
            if (largestPrevDate == SqDateOnly.MinValue) // it was not updated, so all idx reached 0
                break;
            currDate = largestPrevDate;
        }
        Debug.WriteLine($"MemDb.DailyHist.UnionAllDates(): first: DateTime: {mergedDates.First()}, last: DateTime: {mergedDates.Last()}");
        return mergedDates;
    }

    private static async Task<Dictionary<AssetId32Bits, List<Split>>> GetPotentialMissingYfSplits(Db p_db, AssetsCache p_assetCache)
    {
        DateTime etNow = DateTime.UtcNow.FromUtcToEt();
        Dictionary<string, List<Split>> missingYfSplitSqTickers = p_db.GetMissingYfSplits();    // first get it from Redis Db, then Add items from Nasdaq
        Dictionary<AssetId32Bits, List<Split>> potentialMissingYfSplits = missingYfSplitSqTickers.ToDictionary(r => p_assetCache.GetAsset(r.Key).AssetId, r => r.Value);

        // https://www.nasdaq.com/market-activity/stock-splits

        // 2021-11-23:  Nasdaq API as split info data source problems.
        // Last working: https://api.nasdaq.com/api/calendar/splits?date=2021-11-12 only "symbol":"SRL" in it, data is not Null.
        // Next day: https://api.nasdaq.com/api/calendar/splits?date=2021-11-13 ""data":null", "Splits Calendar: No record found."
        // {"data":null,"message":null,"status":{"rCode":200,"bCodeMessage":[{"code":1002,"errorMessage":"Splits Calendar: No record found."}],"developerMessage":null}}
        // This doesn't seem to be an error. It seems Nasdaq doesn't have any split data at the moment. This is confirmed by going to the website:
        // https://www.nasdaq.com/market-activity/stock-splits  No error, but "0 RESULTS".
        // And yes, split events are rare these days. Other Split calendars show hardly any proper split in the next 30 days.
        // https://finance.yahoo.com/calendar/splits/
        // https://eresearch.fidelity.com/eresearch/conferenceCalls.jhtml?tab=splits
        // I don't want to reimplement the source, because Nasdaq "should" be a reliable data source.
        // Let's monitor it. If next time there is a real split in an important stock, like VXX, SVXY, UNG,
        // and if Nasdaq misses that, then introducing a secondary data source is justified.
        // Until that, I am not sure we need the complications of another data source maintenance.
        var url = $"https://api.nasdaq.com/api/calendar/splits?date={Utils.Date2hYYYYMMDD(etNow.AddDays(-7))}"; // go back 7 days before to include yesterdays splits
        string? nasdaqSplitsJson = await Utils.DownloadStringWithRetryAsync(url, 3, TimeSpan.FromSeconds(5), true);
        if (String.IsNullOrEmpty(nasdaqSplitsJson))
        { // Treat it as an unexpected error.
            StrongAssert.Fail(Severity.NoException, $"Error in Downloading '{url}'. Supervisors should be notified.");
            return potentialMissingYfSplits;
        }

        var ndqSplitsJson = JsonSerializer.Deserialize<Dictionary<string, object>>(nasdaqSplitsJson);
        using JsonDocument doc = JsonDocument.Parse(nasdaqSplitsJson);
        JsonElement ndqData = doc.RootElement.GetProperty("data");
        if (ndqData.ToString() == string.Empty)
        { // Treat it as an expected warning. Maybe it is not an error, but genuine that data: "0 RESULTS", there are no splits in the next 2 weeks.
            string msg = $"Warning. NasdaqSplits file: data is missing('0 RESULTS'). '{url}'. Monitor splits in important stocks.Implement secondary data source only if necessary.";
            Console.WriteLine(msg);
            Utils.Logger.Warn(msg);
            return potentialMissingYfSplits;
        }

        // https://api.nasdaq.com/api/calendar/splits?date=2021-11-12
        // {"data":{"headers":{"symbol":"SYMBOL","name":"COMPANY","ratio":"RATIO","payableDate":"PAYABLE ON","executionDate":"EX-DATE","announcedDate":"ANNOUNCED"},"rows":[{"symbol":"SRL","name":"Scully Royalty Ltd.","ratio":"8.000%","payableDate":"11/30/2021","executionDate":"11/12/2021","announcedDate":"N/A"}]},"message":null,"status":{"rCode":200,"bCodeMessage":null,"developerMessage":null}}
        var ndqSplits = ndqData.GetProperty("rows").EnumerateArray()
        .Select(row =>
        {
            string symbol = row.GetProperty("symbol").ToString() ?? string.Empty;
            string sqTicker = Asset.BasicSqTicker(AssetType.Stock, symbol);
            Asset? asset = p_assetCache.TryGetAsset(sqTicker);
            return new
            {
                Asset = asset,
                Row = row
            };
        }).Where(r =>
        {
            return r.Asset != null;
        }).Select(r =>
        {
            string executionDateStr = r.Row.GetProperty("executionDate").ToString() ?? string.Empty; // "executionDate":"3/28/2023"
            DateTime executionDate = DateTime.ParseExact(executionDateStr, "M/dd/yyyy", System.Globalization.CultureInfo.InvariantCulture);

            string ratioStr = r.Row.GetProperty("ratio").ToString() ?? string.Empty;
            var splitArr = ratioStr.Split(':');  // "ratio":"2 : 1" or "ratio":"5.000%" while YF "2:1", which is the same order.
            double beforeSplit = Double.MinValue, afterSplit = Double.MinValue;
            if (splitArr.Length == 2) // "ratio":"2 : 1"
                {
                afterSplit = Utils.InvariantConvert<double>(splitArr[0]);
                beforeSplit = Utils.InvariantConvert<double>(splitArr[1]);
            }
            else // "ratio":"5.000%", we can ignore it.
                {
                    // {"symbol":"GNTY","name":"Guaranty Bancshares, Inc.","ratio":"10.000%","payableDate":"02/12/2021","executionDate":"02/04/2021","announcedDate":"01/20/2021"}
                    // "...has declared a 10% stock dividend for shareholders of record as of February 5, 2021. As an example, each shareholder will receive one additional share of stock for every ten shares owned on the effective date of February 12, 2021.
                    // however, YF doesn't handle at all these stock-dividends (not cash dividend)
                    // YF builds it into the quote, so the quote price is smooth, but this cannot be queried as YF split, neither as cash-dividend.
                }
            var split = new Split() { Date = executionDate, Before = beforeSplit, After = afterSplit };
            return new
            {
                Asset = r.Asset!,
                Split = split
            };
        }).Where(r =>
        {
            return r.Split.Before != Double.MinValue && r.Split.After != Double.MinValue;
        });

        int nUsedNdqSplits = 0;
        foreach (var ndqSplitRec in ndqSplits) // iterate over Nasdaq splits and add it to potentialMissingYfSplits if that date doesn't exist
        {
            nUsedNdqSplits++;
            if (potentialMissingYfSplits.TryGetValue(ndqSplitRec!.Asset!.AssetId, out List<Split>? splitArr))
            {
                if (!splitArr.Exists(r => r.Date == ndqSplitRec.Split.Date))
                    splitArr.Add(ndqSplitRec.Split);
            }
            else
            {
                var newSplits = new List<Split>(1) { ndqSplitRec.Split };
                potentialMissingYfSplits.Add(ndqSplitRec!.Asset!.AssetId, newSplits);
            }
        }
        Utils.Logger.Info($"NasdaqSplits file was downloaded fine and it has {nUsedNdqSplits} useable records.");

        return potentialMissingYfSplits;
    }

    private static async Task<(SqDateOnly[] Dates, float[] AdjCloses)> CreateDailyHist_YF(Asset asset, Dictionary<AssetId32Bits, List<Split>> potentialMissingYfSplits)
    {
        SqDateOnly[] dates = Array.Empty<SqDateOnly>();  // to avoid "Possible multiple enumeration of IEnumerable" warning, we have to use Arrays, instead of Enumerable, because we will walk this lists multiple times, as we read it backwards
        float[] adjCloses = Array.Empty<float>();

        if (asset.ExpectedHistoryStartDateLoc == DateTime.MaxValue) // if Initial value was not overwritten. For Dead stocks, like "S/VXX*20190130"
            return (dates, adjCloses);

        string yfTicker = string.Empty;
        if (asset is Stock stock)
            yfTicker = stock.YfTicker;
        else if (asset is FinIndex finIndex)
            yfTicker = finIndex.YfTicker;
        if (String.IsNullOrEmpty(yfTicker))
            throw new SqException($"CreateDailyHist_YF() exception. Cannot download YF data (ticker:{asset.SqTicker}) has no yfTicker.");
        // https://github.com/lppkarl/YahooFinanceApi
        // YF: all the Open/High/Low/Close are always already adjusted for Splits (so, we don't have to adjust it manually);  In addition: AdjClose also adjusted for Divididends.
        // YF gives back both the onlySplit(butNotDividend)-adjusted row.Close, and SplitAndDividendAdjusted row.AdjustedClose (checked with MO dividend and USO split).
        // checked the YF returned data by stream.ReadToEnd(): it is a CSV structure, with columns. The line "Apr 29, 2020  1:8 Stock Split" is Not in the data.
        // https://finance.yahoo.com/quote/USO/history?p=USO The YF website queries the splits separately when it inserts in-between the rows.
        // Therefore, we have to query the splits separately from YF.
        // The startTime & endTime here defaults to EST timezone
        IReadOnlyList<Candle?>? history = await Yahoo.GetHistoricalAsync(yfTicker, asset.ExpectedHistoryStartDateLoc, DateTime.Now, Period.Daily) // if asked 2010-01-01 (Friday), the first data returned is 2010-01-04, which is next Monday. So, ask YF 1 day before the intended
            ?? throw new SqException($"CreateDailyHist_YF() exception. Cannot download YF data (ticker:{asset.SqTicker}) after many tries.");

        dates = history.Select(r => new SqDateOnly(r!.DateTime)).ToArray();

        // YF sends this weird Texts, which are converted to Decimals, so we don't lose TEXT conversion info.
        // AAPL:    DateTime: 2016-01-04 00:00:00, Open: 102.610001, High: 105.370003, Low: 102.000000, Close: 105.349998, Volume: 67649400, AdjustedClose: 98.213585  (original)
        //          DateTime: 2016-01-04 00:00:00, Open: 102.61, High: 105.37, Low: 102, Close: 105.35, Volume: 67649400, AdjustedClose: 98.2136
        // we have to round values of 102.610001 to 2 decimals (in normal stocks), but some stocks price is 0.00452, then that should be left without conversion.
        // AdjustedClose 98.213585 is exactly how YF sends it, which is correct. Just in YF HTML UI, it is converted to 98.21. However, for calculations, we may need better precision.
        // In general, round these price data Decimals to 4 decimal precision.

        // for penny stocks, IB and YF considers them for max. 4 digits. UWT price (both in IB ask-bid, YF history) 2020-03-19: 0.3160, 2020-03-23: 2302
        // sec.AdjCloseHistory = history.Select(r => (double)Math.Round(r.AdjustedClose, 4)).ToList();
        // YF API gives 6 digits precision. ATVI 2021-06-29: 95.045044
        // YF CSV download gives 5 digits precision.  ATVI 2021-06-29: 95.04504
        // YF HTML UI gives only 2 digits precision.  ATVI 2021-06-29: 95.05
        // We don't want to lose valuable precision data, so keep the best precision, which is 6 digits. Especiall with penny stocks, where prices can be 0.0003, 0.0004.
        adjCloses = history.Select(r => RowExtension.IsEmptyRow(r!) ? float.NaN : (float)Math.Round(r!.AdjustedClose, 6)).ToArray();

        if (asset.AssetId.AssetTypeID == AssetType.Stock) // FinIndex like VIX, FTSE, SP500 don't have splits, so no costly YF download is necessary
        {
            // 2020-04-29: USO split: Split-adjustment was not done in YF. For these exceptional cases, so we need an additional data-source for double check
            // First just add the manual data source from Redis/Sql database, and let’s see how unreliable YF is in the future.
            // If it fails frequently, then we can implement query-ing another website, like https://www.nasdaq.com/market-activity/stock-splits
            var splitHistoryYF = await Yahoo.GetSplitsAsync(yfTicker, asset.ExpectedHistoryStartDateLoc, DateTime.Now);

            DateTime etNow = DateTime.UtcNow.FromUtcToEt();  // refresh etNow
                                                                // var missingYfSplitDb = new SplitTick[1] { new SplitTick() { DateTime = new DateTime(2020, 06, 08), BeforeSplit = 8, AfterSplit = 1 }};
            potentialMissingYfSplits!.TryGetValue(asset.AssetId, out List<Split>? missingYfSplitDb);

            // if any missingYfSplitDb record is not found in splitHistoryYF, we assume YF is wrong (and our DB is right (probably custom made)),
            // so we do this extra split-adjustment assuming it is not in the YF quote history
            // This can fixes YF data errors. For example ProShares UltraPro QQQ (TQQQ), https://finance.yahoo.com/quote/TQQQ/history?p=TQQQ on 2022-01-13 (whole day from 0:0 to 24:0 it was)
            // Date         Open    High    Low     Close*  Adj Close**
            // Jan 13, 2022 77.13   77.55   0.02    70.80   70.80
            // Jan 12, 2022 153.97  155.93  149.88  152.68  152.68  // obviously these required the Multiplier: 0.5, but YF didn't have the Split in their Database. We have it from Nasdaq however.
            // Jan 11, 2022 143.43  151.03  141.04  150.96  150.96
            // This automatic Nasdaq Split data fixes the YF missing split problem for that given day. Good.
            // But note that stock has a Stock.PriorClose value directly in the Stock object.
            // And clients (like MarketDashboard.BrokerAccountViewer uses Stock.PriorClose, instead of the historical closeprice of yesterday).
            // That Stock.PriorClose comes from YF realtime API. That was fixed at 9:30 ET, at market open. So, the Stock.PriorClose was properly split-adjusted at 14:30
            // And this historical Close values was also properly split-adjusted (because we got the missing split from Nasdaq)
            // So, in SqCore: TQQQ historical prices was good immediately at 0:00 in the morning. And Stock.PriorClose was good at 9:30ET (when YF fixed the YF real-time API)
            // This is good. We don't need more development here. What is important that after market open, it should be OK. And it is OK. Both historical and Stock.PriorClose. After 9:30 ET.
            for (int i = 0; missingYfSplitDb != null && i < missingYfSplitDb.Count; i++)
            {
                var missingSplitDb = missingYfSplitDb[i];

                // ignore future data of https://api.nasdaq.com/api/calendar/splits?date=2021-04-14
                if (missingSplitDb.Date > etNow) // interpret missingSplitDb.Date in in ET time zone.
                    continue;
                if (splitHistoryYF.FirstOrDefault(r => r!.DateTime == missingSplitDb.Date) != null) // if that date exists in YF already, do nothing; assume YF uses it
                    continue;

                // VXX reverse split Before:After: "4:1" (Every 4 before becomes 1 after), multiplier is 4/1 = 4.
                // so we should increase the YF historical prices if YF missing the split. YF contains VXX prices $10, but in the morning (after the reverse split), prices are $40.
                // So, we should increase those $10 prices that YF wrongly has.
                double multiplier = (double)missingSplitDb.Before / (double)missingSplitDb.After;
                // USO split date from YF: "Apr 29, 2020" Time: 00:00. Means that very early morning, 9:30 hours before market open. That is the inflection point.
                // Split adjust (multiply) everything before that time, but do NOT split adjust that exact date.
                SqDateOnly missingSplitDbDate = new(missingSplitDb.Date);
                for (int j = 0; j < dates.Length; j++)
                {
                    if (dates[j] < missingSplitDbDate)
                        adjCloses[j] = (float)((double)adjCloses[j] * multiplier);
                    else
                        break;  // dates are in increasing order. So, once we passed the critical date, we can safely exit
                }
            }
        }
        return (dates, adjCloses);
    }

    private static void CreateDailyHist_UserNavs(IGrouping<User?, BrokerNav> navAssetsOfUser, BrokerNav? aggNavAsset, Db p_db, Dictionary<uint, SqDateOnly[]> assetsDates, Dictionary<uint, float[]> assetsAdjustedCloses)
    {
        User user = navAssetsOfUser.Key!;
        List<SqDateOnly[]> navsDates = new();
        List<double[]> navsUnadjustedCloses = new();
        List<KeyValuePair<SqDateOnly, double>[]> navsDeposits = new();
        List<BrokerNav> navAssetsWithHistQuotes = new();
        foreach (var navAsset in navAssetsOfUser)
        {
            var dailyNavStr = p_db.GetAssetQuoteRaw(navAsset.AssetId); // 47K text data from 9.5K brotli data, starts with FormatString: "D/C,20090102/16460,20090105/16826,..."
            if (dailyNavStr == null)
                continue; // "DC.IM", "DC.ID" quote history is in RedisDb,but "DC.TM" TradeStation NAV is not. We exit here, and aggregate only the valid ones.

            int iFirstComma = dailyNavStr.IndexOf(',');
            string formatString = dailyNavStr[..iFirstComma];  // "D/C" for Date/Closes
            if (formatString != "D/C")
                continue;

            navAssetsWithHistQuotes.Add(navAsset);
            var dailyNavStrSplit = dailyNavStr[(iFirstComma + 1)..].Split(',', StringSplitOptions.RemoveEmptyEntries);
            SqDateOnly[] dates = dailyNavStrSplit.Select(r => new SqDateOnly(Int32.Parse(r[..4]), Int32.Parse(r.Substring(4, 2)), Int32.Parse(r.Substring(6, 2)))).ToArray();
            double[] unadjustedClosesNav = dailyNavStrSplit.Select(r => Double.Parse(r[9..])).ToArray();

            KeyValuePair<SqDateOnly, double>[] deposits = p_db.GetAssetBrokerNavDeposit(navAsset.AssetId);

            // CreateAdjNavAndIntegrate(navAsset, dates, unadjustedClosesNav, deposits, assetsDates, assetsAdjustedCloses);
            float[] adjCloses = CalcAdjNavUsingDeposits(dates, unadjustedClosesNav, deposits);
            assetsDates[navAsset.AssetId] = dates;
            assetsAdjustedCloses[navAsset.AssetId] = adjCloses;
            Debug.WriteLine($"{navAsset.SqTicker}, first: DateTime: {dates.First()}, Close: {adjCloses.First()}, last: DateTime: {dates.Last()}, Close: {adjCloses.Last()}");  // only writes to Console in Debug mode in vscode 'Debug Console'

            navsDates.Add(dates);
            navsUnadjustedCloses.Add(unadjustedClosesNav);
            navsDeposits.Add(deposits);
        } // All NAVs of the user

        // if more than 2 NAVs for the user has valid history, then there is a virtual synthetic aggregatedNAV that has to be updated
        if (aggNavAsset == null || navAssetsWithHistQuotes.Count < 2)
            return;

        // merging Lists. Union and LINQ has very bad performance. Use Dict. https://stackoverflow.com/questions/4031262/how-to-merge-2-listt-and-removing-duplicate-values-from-it-in-c-sharp
        var aggDatesDict = new Dictionary<SqDateOnly, double>();
        for (int iNav = 0; iNav < navsDates.Count; iNav++)
        {
            for (int j = 0; j < navsDates[iNav].Length; j++)
            {
                if (!aggDatesDict.ContainsKey(navsDates[iNav][j]))
                    aggDatesDict[navsDates[iNav][j]] = navsUnadjustedCloses[iNav][j];
                else
                    aggDatesDict[navsDates[iNav][j]] += navsUnadjustedCloses[iNav][j];
            }
        }
        var aggDatesSortedDict = aggDatesDict.OrderBy(p => p.Key); // if you need to sort it once, don't use SortedDictionary() https://www.c-sharpcorner.com/article/performance-sorteddictionary-vs-dictionary/
        var aggDates = aggDatesSortedDict.Select(r => r.Key).ToArray();    // it is ordered from earliest (2011) to latest (2020) exactly how the dates come from DB
        var aggUnadjustedCloses = aggDatesSortedDict.Select(r => r.Value).ToArray();

        var aggDepositsDict = new Dictionary<SqDateOnly, double>();
        for (int iNav = 0; iNav < navsDates.Count; iNav++)
        {
            foreach (var deposit in navsDeposits[iNav])
            {
                if (!aggDepositsDict.ContainsKey(deposit.Key))
                    aggDepositsDict[deposit.Key] = deposit.Value;
                else
                    aggDepositsDict[deposit.Key] += deposit.Value;
            }
        }
        var aggDepositsSortedDict = aggDepositsDict.OrderBy(p => p.Key); // if you need to sort it once, don't use SortedDictionary() https://www.c-sharpcorner.com/article/performance-sorteddictionary-vs-dictionary/
        float[] aggAdjCloses = CalcAdjNavUsingDeposits(aggDates, aggUnadjustedCloses, aggDepositsSortedDict.ToArray());
        assetsDates[aggNavAsset.AssetId] = aggDates;
        assetsAdjustedCloses[aggNavAsset.AssetId] = aggAdjCloses;
        Debug.WriteLine($"{aggNavAsset.SqTicker}, first: DateTime: {aggDates.First()}, Close: {aggAdjCloses.First()}, last: DateTime: {aggDates.Last()}, Close: {aggAdjCloses.Last()}");  // only writes to Console in Debug mode in vscode 'Debug Console'
    }

    private static float[] CalcAdjNavUsingDeposits(SqDateOnly[] dates, double[] unadjustedClosesNav, KeyValuePair<SqDateOnly, double>[] deposits)
    {
        // ************************ Stock dividend ajustment and NAV deposit adjustment
        // https://www.investopedia.com/ask/answers/06/adjustedclosingprice.asp
        // "The adjusted closing price is often used when examining historical returns "
        // For example, let's assume that the closing price for one share of XYZ Corp. is $20 on Thursday. After the close on Thursday, XYZ Corp. announces a dividend distribution of $1.50 per share. The adjusted closing price for the stock would then be $18.50 ($20-$1.50). So, we take away the dividend from the All Previous prices.
        // > this is correct, but this will not work iteratively, the subtraction should be converted to an equivalent multiplication.
        // So, multiplier = (originalPrice-dividend)/originalPrice, which is (20-1.50)/20
        // we use this multiplier on original price 20, that gives 20 * multiplier = 20-1.5=18.5 fine.
        // And this multiplier can be used iteratively.

        // >Another example: imagine there was a 50% increase in the previous day, then a 10% dividend.
        // 100, 100, 200, 200, 180+20, 180, 180
        // We calculate daily%returns: 0%, 0%, 100%, 0%, 0%
        // If we just simply take away -20 from all prevous prices, 80, 80 (?), 180, 180, 180, so daily return: 0% 0%, 125% (which is incorrect), 0%, 0%
        // To do it correctly, we have to reformulate that daily %return stays the same as before. The ? is not 80, it is something different.
        // So, multiplier = (originalPrice-dividend)/originalPrice = 200-20/200=90%.
        // Then we don't subtract -20 from each previous day's values, but we multiply each previous day's values by 90%, to obtain:
        // 90, 90, 180, 180, 180  which gives the daily returns of 0%, 100%, 0%. Correct.

        // >For adjusting NAV values by deposit/withdrawal, the same can be done.
        // If there was a withdrawal (negative value), we consider it as dividend.
        // So, when original NAV in $M was 9, 9, 10, 9+1 withdrawal, 9, 11 then the %return is not lastNAV/firstNAV = 11/9, which is 22%
        // But multiplier = (10-1)/10 = 90%, and using that
        // AdjustedNAV is 8.1, 8.1, 9, 9, 11, therefore %return = 11/8.1= 35.8%
        // That gives that every daily %return is the same as before.
        // Same can be said for positive Deposits. If we add deposit, NAV is increased, but we don't want to see that as a performance measure %return.

        // >It is like doing a Time-Weighted-Return per day. Basically, we calculate the synthetic daily%returns that every day gives back the daily %return.
        // Then we aggregate these in a way that the final price is the current NAV.

        // >For example, if we had a NAV value of 4.4M, then added a deposit of 4M, then the first multiplier = 1.9, almost 2.
        // Then we use that 1.9 multiplier for all the previous NAVs. Good. It is not additive. TWR is multiplicative of the periods.
        // Virtually we create a synthetic virtual NAV, that was not properly as it was in real life. However, if we calculate daily %Chg on
        // virtual synthetic NAV, then that daily %Chg will give back exactly the daily %changes how it happened in real life.
        // So, the virtualNAV values will not be real life, but the %Chg-es will be real life.
        // If we don't do this NAV adjustments, then the NAV values will be real and correct and how it happened in real life, but then
        // the calculated daily %Changes will not be how it happend. (because there will be a daily doubling on a given day.
        // That would be a 200% daily performance, which is what happened, but that would be not a good measure to evaluate the performance of this fund manager.)

        // >2021-09-09 experience with DeBlanzad deposit adjustements: Success, but it didn't help in return (only in minValue, maxDD changed).
        // The TWR calculation more or less stayed the same. The TWR calculation is multiplicative, DeBlan lost too much in percentage -30% when it was at NAV 5M.
        // That -30% return is multiplicative in final TWR calculation.
        // >First understand that the TWR is the right calculation for long-term return. There is no other way mathematically.
        // I would like to evaluate a virtual Fund manager based on TWR, the aggregation of monthly return.
        // Imagine: for 11 months, fund manager return is -10% with 1M NAV start. That is a monthly 0.9 multiplier.
        // Then extra 10M NAV cash deposit comes in on 1st December, so now he has 10.1M NAV. He does 50% on that during that single 1 month December.
        // How should we evaluate his performance?
        // TWR-return: 0.9*0.9*....0.9*1.5. Should we say he did That Yes. That would be correct to evaluate his performance.
        // How to do in an additive way? start: 1M, end:15M.
        // But we cannot just subtract 10M from the previous months, because they become negative.
        // Similarly we cannot just add 10M to the previous months, because then the January NAV would become from 11M to 10.9M, but that is not exual to his poort -10% performance in January.
        // So, there is no way that the additional or subtractional method would work. The only option is multiplicative on the periods. That is exactly the TWR.
        // >See what happened with the DeBlanzac account in 2021.
        // 2021-01-01 NAV: 10M.  Then we removed -5M in February, and added +4M in July. On 2021-09-09 we have NAV of 8.3M.
        // A. The naive thinking is that we removed -1M net, but we had 10M in January, so we should have 9M now for breakeven. But because we have 8.3M now, it means we are in a -7.8% loss this year.
        // B. However, TWR calculates this year performance as -16%. Rightly. But why?
        // 10M becomes => 5M => then came a -30% loss (-1.5M) and NAV become 3.5M => +4M deposit increased NAV to 7.5M. Currently the NAV is 8.3M (11%). So, how would you evaluate it?
        // I would say we had a -30% performance in the first period on NAV value 5M, then we had a +11% performance on NAV value 7.5M.
        // TWR calculation for the periods : 0.7*1.11 = current -16% shown in SqCore.
        // >The NAV values should be adjusted by deposits/withdrawal, otherwise the minValue, MaxDD will be wrong.

        double multiplier = 1.0;    // cummulative multiplier
        float[] adjCloses = new float[dates.Length];
        int iDeposits = deposits.Length - 1;
        for (int i = dates.Length - 1; i >= 0; i--) // go from yesterday backwards, because adjustment is a multiplier.
        {
            SqDateOnly date = dates[i];
            adjCloses[i] = (float)(unadjustedClosesNav![i] * multiplier);
            if (iDeposits >= 0 && date == deposits[iDeposits].Key)
            {
                // >Deposit: Date/Value
                // 20200323/-1000000  // assume it was taken in the middle of the day. The end of the day NAV doesn't contain that.
                // >NAV: Date/Value
                // 20200319/10270249
                // 20200320/10522203
                // 20200323/9630437 // decreased, don't change that day. But add the withdrawal to it, as if that would have been the correct price.
                // 20200324/9590033
                // >Then OriginalNAV_on_20200323 = (9630437 + 1000000)
                // multiplier = (OriginalNAV-dividend)/OriginalNAV = 9630437 / (9630437 + 1000000) =  0.90593048997 = 90.5%.
                // Warning. Don't multiply the 20200323 date NAV with this (because that is already the 9.6M reduced value, not the 10.5M big value), but multiply all prevous days.
                // At the end multiplier becomes: 1.16; because Deposits are more.
                // >The result of this deposit-adjustment:
                // Unadjusted: from 9.8M to 11.7M, a 18% return in 2020. -15% maxDD
                // Adjusted: from 8.9M to 11.7M, a 31% return in 2020. -9.8% maxDD
                double sumDeposit = 0.0;    // aggregate deposits on that day.
                while (iDeposits >= 0 && date == deposits[iDeposits].Key) // it can be that for the same date, there are many deposits + withdrawal records. DeBlan: 04/30/2015: -200K, +450K on the same day
                {
                    sumDeposit += deposits[iDeposits].Value;
                    iDeposits--;
                }
                double unadjustedActualNav = unadjustedClosesNav![i] - sumDeposit; // if it is a negative withdrawal, it will add it
                double mult = (unadjustedActualNav + sumDeposit) / unadjustedActualNav;
                multiplier *= mult;
            }
        }
        return adjCloses;
    }

    private void SetNextReloadHistDataTriggerTime()
    {
        // reload times should be relative to ET (Eastern Time), because that is how USA stock exchanges work.
        // Reload History approx in UTC: at 9:00 (when IB resets its own timers and pre-market starts)(in the 3 weeks when summer-winter DST difference, it is 8:00), at 14:00 (30min before market open, last time to get correct data, because sometimes YF fixes data late), 21:30 (30min after close)
        // In ET time zone, these are: 4:00ET, 9:00ET, 16:30ET. IB starts premarket trading at 4:00. YF starts to have premarket data from 4:00ET.
        DateTime etNow = DateTime.UtcNow.FromUtcToEt();  // refresh etNow
        int nowTimeOnlySec = etNow.Hour * 60 * 60 + etNow.Minute * 60 + etNow.Second;
        int targetTimeOnlySec;
        if (nowTimeOnlySec < 4 * 60 * 60)
            targetTimeOnlySec = 4 * 60 * 60;
        else if (nowTimeOnlySec < 9 * 60 * 60) // Market opens 9:30ET, but reload data 30min before
            targetTimeOnlySec = 9 * 60 * 60;
        else if (nowTimeOnlySec < 16 * 60 * 60 + 30 * 60) // Market closes at 16:00ET, but reload data 30min after
            targetTimeOnlySec = 16 * 60 * 60 + 30 * 60;
        else
            targetTimeOnlySec = 24 * 60 * 60 + 4 * 60 * 60; // next day 4:00

        DateTime targetDateEt = etNow.Date.AddSeconds(targetTimeOnlySec);
        Utils.Logger.Info($"m_reloadHistoricalDataTimer set next targetdate: {targetDateEt.ToSqDateTimeStr()} ET");
        m_historicalDataReloadTimer?.Change(targetDateEt - etNow, TimeSpan.FromMilliseconds(-1.0));     // runs only once.
    }
    public static (SqDateOnly[] Dates, float[] AdjCloses) GetSelectedStockTickerHistData(SqDateOnly lookbackStart, SqDateOnly lookbackEnd, string yfTicker) // send startdate and end date
    {
        SqDateOnly[] dates = Array.Empty<SqDateOnly>();  // to avoid "Possible multiple enumeration of IEnumerable" warning, we have to use Arrays, instead of Enumerable, because we will walk this lists multiple times, as we read it backwards
        float[] adjCloses = Array.Empty<float>();

        IReadOnlyList<Candle?>? history = Yahoo.GetHistoricalAsync(yfTicker, lookbackStart, lookbackEnd, Period.Daily).Result // if asked 2010-01-01 (Friday), the first data returned is 2010-01-04, which is next Monday. So, ask YF 1 day before the intended
            ?? throw new Exception($"ReloadHistoricalDataAndSetTimer() exception. Cannot download YF data (ticker:{yfTicker}) after many tries.");

        dates = history.Select(r => new SqDateOnly(r!.DateTime)).ToArray();
        adjCloses = history.Select(r => RowExtension.IsEmptyRow(r!) ? float.NaN : (float)Math.Round(r!.AdjustedClose, 4)).ToArray();
        return (dates, adjCloses);
    }
}