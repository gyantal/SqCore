using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading.Tasks;
using NLog;
using QuantConnect.Data.Auxiliary;
using SqCommon;
using YahooFinanceApi;

namespace Fin.MemDb;

class SqPrice
{
    public DateTime ReferenceDate;
    public float Open;
    public float High;
    public float Low;
    public float Close;
    public long Volume;
}

class SqDivSplit
{
    public DateTime ReferenceDate;
    public double DividendValue;
    public double SplitFactor;
    public float ReferenceRawPrice;
}

class FactorFileDivSplit
{
    public DateTime ReferenceDate;
    public double DividendFactorCum;
    public double SplitFactorCum;
    public float ReferenceRawPrice;
}

public class FinDbCrawlerExecution : SqExecution
{
    public static SqExecution ExecutionFactoryCreate()
    {
        return new FinDbCrawlerExecution();
    }

    public override void Run() // try/catch is only necessary if there is a non-awaited async that continues later in a different tPool thread. See comment in SqExecution.cs
    {
        Utils.Logger.Info($"FinDbCrawlerExecution.Run() BEGIN, Trigger: '{Trigger?.Name ?? string.Empty}'");
        FinDb.CrawlData().TurnAsyncToSyncTask();
    }
}

public partial class FinDb
{
    private static bool g_isCheckHistoricalDataIntegrity = true;
    public static void ScheduleDailyCrawlerTask()
    {
        SqTask? sqTask = new()
        {
            Name = "FinDbDailyCrawler",
            ExecutionFactory = FinDbCrawlerExecution.ExecutionFactoryCreate,
        };
        sqTask.Triggers.Add(new SqTrigger()
        {
            Name = "EarlyMorningCrawler",
            SqTask = sqTask,
            TriggerType = TriggerType.Daily,
            Start = new RelativeTime() { Base = RelativeTimeBase.BaseOnAbsoluteTimeMidnightUtc, TimeOffset = TimeSpan.FromMinutes(5 * 60 + 15) },  // Activate every day 5:15 UTC,
        });
        SqTaskScheduler.gSqTasks.Add(sqTask);
    }

    public static async Task<bool> CrawlData() // print log to Console or HTML
    {
        // Determining the appropriate data directory where the map_files, price and factor files are located.
        string finDataDir = Utils.FinDataFolderPath + @"equity/usa/";

        string mapFilesDir = $"{finDataDir}map_files";
        // Collect tickers from file names. Furthermore, collect start and end dates from map_files.
        List<string> tickers = new();
        List<string> virtualTickers = new();
        List<string> mapFilesFirstRows = new();
        List<string> mapFilesLastButOneRows = new();
        List<string> mapFilesLastRows = new();
        foreach (string mapFilePath in Directory.GetFiles(mapFilesDir, "*.csv")) // Get an array of file paths in the directory with ".csv" extension.
        {
            if (mapFilePath.EndsWith("-qc.csv")) // Ignore files ending with '-qc.csv'. It is only the spy-qc.csv. We want keep the original QC SPY data.
                continue;

            if (mapFilePath.EndsWith("-sq.csv")) // Ignore files ending with '-sq.csv'.
            {
                string? sqFileName = Path.GetFileNameWithoutExtension(mapFilePath);
                int hyphenIndex = sqFileName.IndexOf('-');
                if (hyphenIndex != -1 && hyphenIndex < sqFileName.Length - 1)
                {
                    string virtualTicker = sqFileName[..hyphenIndex];
                    virtualTickers.Add(virtualTicker.ToUpper());
                }
                continue;
            }
            tickers.Add(Path.GetFileNameWithoutExtension(mapFilePath).ToUpper());
            string[] fileLines = File.ReadAllLines(mapFilePath);

            if (fileLines.Length > 1)
            {
                string firstRow = fileLines[0];
                int firstCommaIndex = firstRow.IndexOf(',');
                if (firstCommaIndex != -1)
                    mapFilesFirstRows.Add(firstRow[..firstCommaIndex]);
                string lastButOneRow = fileLines[^2];
                int lastButOneCommaIndex = lastButOneRow.IndexOf(',');
                if (lastButOneCommaIndex != -1)
                    mapFilesLastButOneRows.Add(lastButOneRow[..lastButOneCommaIndex]);
                string lastRow = fileLines[^1];
                int lastCommaIndex = lastRow.IndexOf(',');
                if (lastCommaIndex != -1)
                    mapFilesLastRows.Add(lastRow[..lastCommaIndex]);
            }
        }

        // Calling the CrawlData function that generates files per ticker.
        int nErrors = 0;
        for (int i = 0; i < tickers.Count; i++)
        {
            string ticker = tickers[i];
            try
            {
                // The first date in the map file represents the creation date of the original ticker.
                // The last date represents the delisting date or the end date of the last ticker.
                // Any intermediate dates (if present) indicate ticker changes: each represents the last day of a previous ticker.
                // The new ticker becomes active on the next calendar day.
                // Therefore, if there was no ticker change, the first date is the start date of the current ticker.
                // If there was a ticker change, the current ticker's start date is the day after the last but one date, so we increment it by one day in that case.
                DateTime startDate = DateTime.ParseExact(mapFilesFirstRows[i], "yyyyMMdd", CultureInfo.InvariantCulture);
                DateTime startDateCurrTicker = DateTime.ParseExact(mapFilesLastButOneRows[i], "yyyyMMdd", CultureInfo.InvariantCulture);
                if (startDate != startDateCurrTicker)
                    startDateCurrTicker = startDateCurrTicker.AddDays(1);
                DateTime endDate = DateTime.ParseExact(mapFilesLastRows[i], "yyyyMMdd", CultureInfo.InvariantCulture);
                if (endDate < DateTime.Today) // If not alive, don't create new factor file
                    continue;
                Utils.Logger.Info($"FinDb.CrawlData() START with ticker: {ticker}");
#if DEBUG
                Console.WriteLine($"FinDb.CrawlData() START with ticker: {ticker}");
#endif
                bool isPriceOK = await CrawlPriceData(ticker, finDataDir, startDateCurrTicker, endDate);
                if (!isPriceOK)
                    Utils.Logger.Error($"Error processing price for {ticker}");

                bool isFundamentalOK = await CrawlFundamentalData(ticker, finDataDir, startDate);
                if (!isFundamentalOK)
                    Utils.Logger.Error($"Error processing fundamental for {ticker}");
            }
            catch (System.Exception e)
            {
                nErrors++;
                Utils.Logger.Error($"ERROR in CrawlData(). Ticker: {ticker}. Exception: '{e.Message}'");
            }
        }

        // Process the virtual tickers. E.g. 'VXX-SQ'
        foreach (string ticker in virtualTickers)
        {
            Utils.Logger.Info($"FinDb.CrawlData() START with ticker: {ticker}-sq");
#if DEBUG
            Console.WriteLine($"FinDb.CrawlData() START with ticker: {ticker}-sq");
#endif
            try
            {
                CreateVirtualTickerDailyPriceFiles(ticker, finDataDir, out int lastHistDateInt);
                CreateVirtualTickerFactorFiles(ticker, finDataDir, lastHistDateInt);
            }
            catch (System.Exception e)
            {
                nErrors++;
                Utils.Logger.Error($"Error processing {ticker}. Exception: '{e.Message}'");
            }
        }

        if (nErrors > 0)
            HealthMonitorMessage.SendAsync($"FinDb.CrawlData() #{nErrors} errors", HealthMonitorMessageID.SqCoreWebCsError).TurnAsyncToSyncTask();

        // QC Cloud backtests are one-time only, but in SqCore they run for weeks. This price cache is useful intraday, but after our daily price crawler runs, we have to clear this price cache.
        QuantConnect.Lean.Engine.DataFeeds.TextSubscriptionDataSourceReader.BaseDataSourceCache.Clear();
        gFinDb.MapFileProvider.ClearCache();
        gFinDb.FactorFileProvider.ClearCache();
        CheckIfOverallDataIntegrityProblemOccuredAndSendEmail();

        return true;
    }

    private static void CheckIfOverallDataIntegrityProblemOccuredAndSendEmail()
    {
        if (!g_isCheckHistoricalDataIntegrity)
            return;
        // Email alert for suspicious (non-recent) data discrepancies across all tickers. Check the dataChanges<Today>.csv file.
        try
            {
                string dataChangesDir = Path.Combine(Utils.FinDataFolderPath, "equity/usa/data_changes_log");
                if (!Directory.Exists(dataChangesDir))
                    return;

                string[] files = Directory.GetFiles(dataChangesDir, $"dataChanges{DateTime.UtcNow:yyMMdd}.csv");
                string todayFile = (files.Length > 0) ? files[0] : string.Empty;
                if (!File.Exists(todayFile))
                    return;

                string[] lines = File.ReadAllLines(todayFile);
                if (lines.Length > 1)
                {
                    DateTime sevenDaysAgo = DateTime.UtcNow.Date.AddDays(-7);
                    List<string> suspiciousDiffs = new();

                    // start from line index 1 to skip header
                    for (int i = 1; i < lines.Length; i++)
                    {
                        string line = lines[i];
                        // Expected format: RunDate;Ticker;FileType;EventDate;ChangeType;Field;OldValue;NewValue
                        string[] parts = line.Split(';');
                        if (parts.Length < 6)
                            continue;

                        string eventDateStr = parts[3];
                        if (!DateTime.TryParseExact(eventDateStr, "yyyyMMdd", null, System.Globalization.DateTimeStyles.None, out DateTime eventDate))
                            continue;

                        bool isRecent = eventDate >= sevenDaysAgo;

                        // Alert for any change affecting data older than 7 days
                        if (!isRecent)
                            suspiciousDiffs.Add(line);
                    }

                    if (suspiciousDiffs.Count > 0)
                    {
                        // Construct email body manually (limit to 200 lines)
                        StringBuilder sb = new StringBuilder();
                        sb.AppendLine($"Found {suspiciousDiffs.Count} old data discrepancies (older than 7 days):");
                        sb.AppendLine();

                        int maxLines = Math.Min(200, suspiciousDiffs.Count);
                        for (int i = 0; i < maxLines; i++)
                            sb.AppendLine(suspiciousDiffs[i]);

                        if (suspiciousDiffs.Count > 200)
                            sb.AppendLine("... (truncated)");

                        new Email()
                        {
                            Body = sb.ToString(),
                            IsBodyHtml = false,
                            Subject = $"FinDb Data Integrity Alert — {suspiciousDiffs.Count} anomalies detected",
                            ToAddresses = Utils.Configuration["Emails:Balazs"]!
                        }.Send();

                        Utils.Logger.Warn($"FinDb.CrawlData(): Sent integrity alert with {suspiciousDiffs.Count} discrepancies.");
                    }
                    else
                    {
                        Utils.Logger.Info("FinDb.CrawlData(): No old data discrepancies found. No email alert sent.");
                    }
                }
            }
            catch (Exception ex)
            {
                Utils.Logger.Error(ex, "Error while sending FinDb aggregated data integrity email.");
            }
    }

    public static async Task<bool> CrawlPriceData(string p_ticker, string p_finDataDir, DateTime p_startDate, DateTime p_endDate)
    {
        int nPotentialProblems = 0;
        string todayDateStr = DateTime.UtcNow.ToString("yyyyMMdd"); // e.g., "20250429"

        // Fetch data using the IHistPrice interface with all necessary flags for OHLCV, dividends, and splits.
        var histResult = await HistPrice.g_HistPrice.GetHistAsync(p_ticker, HpDataNeed.AdjClose | HpDataNeed.Split | HpDataNeed.Dividend | HpDataNeed.OHLCV, p_startDate, p_endDate);

        // Check if there was an error in fetching data
        if (histResult.ErrorStr != null)
        {
            Utils.Logger.Error($"Cannot download data for {p_ticker}: after many tries.");
            return false;
        }

        // Extract data from the result
        SqDateOnly[]? dates = histResult.Dates;
        float[]? adjCloses = histResult.AdjCloses;
        HpSplit[]? splits = histResult.Splits;
        HpDividend[]? dividends = histResult.Dividends;
        float[]? opens = histResult.Opens;
        float[]? closes = histResult.Closes;
        float[]? highs = histResult.Highs;
        float[]? lows = histResult.Lows;
        long[]? volumes = histResult.Volumes;

        // Process the raw price data with SqDateOnly to ensure compatibility
        if (dates == null || opens == null || highs == null || lows == null || closes == null || volumes == null) // Ensure dates and other data arrays are not null before processing
        {
            Utils.Logger.Error($"CrawlPriceDataNew(): No date data received for {p_ticker}.");
            return false;
        }
        List<SqPrice> rawClosesFromYfList = new();
        for (int i = 0; i < dates.Length; i++)
        {
            rawClosesFromYfList.Add(new SqPrice
            {
                ReferenceDate = dates[i].Date, // Use the Date property to get DateTime from SqDateOnly
                Open = opens[i],
                High = highs[i],
                Low = lows[i],
                Close = closes[i],
                Volume = volumes[i]
            });
        }

        // Reverse adjust historical data with the splits. Going backwards in time, starting from 'today'.
        if (splits?.Length > 0)
        {
            double splitMultiplier = 1.0;
            int lastSplitIdx = splits.Length - 1;
            DateTime watchedSplitDate = splits[lastSplitIdx].DateTime.Date;  // YF 'chart' API gives the Time part too for dividends, splits. E.g. "2020-10-02 9:30". We need only the .Date part

            for (int i = rawClosesFromYfList.Count - 1; i >= 0; i--)
            {
                SqPrice dailyData = rawClosesFromYfList[i];
                DateTime date = dailyData.ReferenceDate;

                if (date < watchedSplitDate)
                {
                    splitMultiplier *= (double)splits[lastSplitIdx].AfterSplit / splits[lastSplitIdx].BeforeSplit;
                    lastSplitIdx--;
                    watchedSplitDate = (lastSplitIdx == -1) ? DateTime.MinValue : splits[lastSplitIdx].DateTime.Date;  // YF 'chart' API gives the Time part too for dividends, splits. E.g. "2020-10-02 9:30". We need only the .Date part
                }

                dailyData.Open *= (float)splitMultiplier;
                dailyData.High *= (float)splitMultiplier;
                dailyData.Low *= (float)splitMultiplier;
                dailyData.Close *= (float)splitMultiplier;
                dailyData.Volume = (long)Math.Round(dailyData.Volume / splitMultiplier);
            }
        }

        string zipFilePath = Path.GetFullPath(p_finDataDir + $"daily{Path.DirectorySeparatorChar}{p_ticker.ToLower()}.zip");
        List<string> priceChangeLogs = new();
        if (g_isCheckHistoricalDataIntegrity)
        {
            // Data validation and consistency checks, if g_isCheckHistoricalDataIntegrity:
            // The code compares newly fetched price and event data against previously stored files to detect any discrepancies.
            // It logs changes in OHLCV values, dividends, and splits (new, removed, or modified records) to daily change logs file: e.g. (all tickers into one file) dataChanges251007.csv
            // A comparison utililty function, CompareEventFiles(),  can also diff archived event files between two exact dates for manual validation.
            // Missing factor file's reference prices or suspicious gaps are flagged as potential data quality issues.

            // HistoricalDataIntegrityCheck, Step1: Read the previous price file (if exists) for comparison
            Dictionary<DateTime, SqPrice> oldPriceData = new();
            if (File.Exists(zipFilePath))
            {
                string oldCsvContent = ReadCsvFromZip(zipFilePath, $"{p_ticker.ToLower()}.csv");
                using StringReader reader = new(oldCsvContent);
                string? line;
                while ((line = reader.ReadLine()) != null)
                {
                    string[] parts = line.Split(',');
                    if (parts.Length >= 6 && DateTime.TryParseExact(parts[0], "yyyyMMdd HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime date))
                    {
                        oldPriceData[date] = new SqPrice
                        {
                            ReferenceDate = date,
                            Open = float.Parse(parts[1]) / 10000f,
                            High = float.Parse(parts[2]) / 10000f,
                            Low = float.Parse(parts[3]) / 10000f,
                            Close = float.Parse(parts[4]) / 10000f,
                            Volume = long.Parse(parts[5])
                        };
                    }
                }
            }

            // Compare new price data with old price data
            HashSet<DateTime> newPriceDates = new();

            // Collect new price dates into HashSet
            for (int i = 0; i < rawClosesFromYfList.Count; i++)
            {
                newPriceDates.Add(rawClosesFromYfList[i].ReferenceDate);
            }

            // Check for existing, but modified prices (existing dates in both old and new data)
            for (int i = 0; i < rawClosesFromYfList.Count; i++)
            {
                SqPrice newPrice = rawClosesFromYfList[i];
                if (oldPriceData.TryGetValue(newPrice.ReferenceDate, out SqPrice? oldPrice))
                {
                    if (Math.Abs(newPrice.Open - oldPrice.Open) >= 1e-2)
                        priceChangeLogs.Add($"{todayDateStr};{p_ticker};Price;{newPrice.ReferenceDate:yyyyMMdd};PriceChange;Open;{oldPrice.Open};{newPrice.Open}");
                    if (Math.Abs(newPrice.High - oldPrice.High) >= 1e-2)
                        priceChangeLogs.Add($"{todayDateStr};{p_ticker};Price;{newPrice.ReferenceDate:yyyyMMdd};PriceChange;High;{oldPrice.High};{newPrice.High}");
                    if (Math.Abs(newPrice.Low - oldPrice.Low) >= 1e-2)
                        priceChangeLogs.Add($"{todayDateStr};{p_ticker};Price;{newPrice.ReferenceDate:yyyyMMdd};PriceChange;Low;{oldPrice.Low};{newPrice.Low}");
                    if (Math.Abs(newPrice.Close - oldPrice.Close) >= 1e-2)
                        priceChangeLogs.Add($"{todayDateStr};{p_ticker};Price;{newPrice.ReferenceDate:yyyyMMdd};PriceChange;Close;{oldPrice.Close};{newPrice.Close}");
                    if (oldPrice.Volume > 0 && Math.Abs(newPrice.Volume - oldPrice.Volume) / oldPrice.Volume > 0.05)
                        priceChangeLogs.Add($"{todayDateStr};{p_ticker};Price;{newPrice.ReferenceDate:yyyyMMdd};PriceChange;Volume;{oldPrice.Volume};{newPrice.Volume}");
                }
            }

            // Check for removed prices (dates in old data but not in new data)
            foreach (KeyValuePair<DateTime, SqPrice> oldPriceEntry in oldPriceData)
            {
                if (!newPriceDates.Contains(oldPriceEntry.Key))
                {
                    SqPrice oldPrice = oldPriceEntry.Value;
                    priceChangeLogs.Add($"{todayDateStr};{p_ticker};Price;{oldPriceEntry.Key:yyyyMMdd};RemovedPrice;Open;{oldPrice.Open};0");
                    priceChangeLogs.Add($"{todayDateStr};{p_ticker};Price;{oldPriceEntry.Key:yyyyMMdd};RemovedPrice;High;{oldPrice.High};0");
                    priceChangeLogs.Add($"{todayDateStr};{p_ticker};Price;{oldPriceEntry.Key:yyyyMMdd};RemovedPrice;Low;{oldPrice.Low};0");
                    priceChangeLogs.Add($"{todayDateStr};{p_ticker};Price;{oldPriceEntry.Key:yyyyMMdd};RemovedPrice;Close;{oldPrice.Close};0");
                    priceChangeLogs.Add($"{todayDateStr};{p_ticker};Price;{oldPriceEntry.Key:yyyyMMdd};RemovedPrice;Volume;{oldPrice.Volume};0");
                }
            }
        } // g_isCheckHistoricalDataIntegrity

        // Create a zip file. But before that, check that if there is already a zip for this ticker, then archive it with the first three characters of today.
        // TEMP: save all price/event files forever temporarily. In the future 7 archive files using day-of-the-week will be enough.
        // string dayOfWeek = DateTime.UtcNow.AddDays(-1).DayOfWeek.ToString()[..3].ToLower();
        if (File.Exists(zipFilePath))
        {
            string backupFileName = p_finDataDir + $"daily{Path.DirectorySeparatorChar}{p_ticker.ToLower()}_{todayDateStr}.zip";
            File.Move(zipFilePath, backupFileName, true);
        }
        // Create a dictionary of SqPrices to find dates faster.
        // Previous days date and closing prices have to be collected for factor file. These will be reference date and reference price.
        // YF vs. required QC price format: 2000-01-01 123.4567 vs. 20200101 00:00 1234567
        Dictionary<DateTime, SqPrice> rawPrevClosesDict = new();
        float prevClosePrice = 0f;
        DateTime prevDayDate = DateTime.MinValue;

        using FileStream zipToCreate = new(zipFilePath, FileMode.Create);
        using ZipArchive zipFile = new(zipToCreate, ZipArchiveMode.Create);
        ZipArchiveEntry innerCsvFile = zipFile.CreateEntry($"{p_ticker.ToLower()}.csv");
        using StreamWriter tw = new(innerCsvFile.Open());

        for (int days = 0; days < rawClosesFromYfList.Count; days++)
        {
            SqPrice dailyData = rawClosesFromYfList[days];
            string date = dailyData.ReferenceDate.ToString("yyyyMMdd HH:mm");
            double open = Math.Round(dailyData.Open * 10000);
            double high = Math.Round(dailyData.High * 10000);
            double low = Math.Round(dailyData.Low * 10000);
            double close = Math.Round(dailyData.Close * 10000);
            decimal volume = dailyData.Volume;

            rawPrevClosesDict.Add(dailyData.ReferenceDate, new SqPrice() { ReferenceDate = prevDayDate, Close = prevClosePrice });
            prevClosePrice = dailyData.Close;
            prevDayDate = dailyData.ReferenceDate;

            string line = $"{date},{open},{high},{low},{close},{volume}";
            tw.WriteLine(line);
        }
        tw.Close();

        // Prepare for factor file creation
        Dictionary<DateTime, SqDivSplit> divSplitHistory = new();

        // Process dividends if they are not null
        if (dividends != null)
        {
            foreach (HpDividend dividendTick in dividends)
            {
                DateTime date = dividendTick.DateTime.Date; // YF 'chart' API gives the Time part too for dividends, splits. E.g. "2020-10-02 9:30". We need only the .Date part
                // Markets were closed for a week after 9/11, but e.g. NVDA had a split on 2001-09-12. We need to use last known price on 2001-09-10. https://finance.yahoo.com/quote/NVDA/history/?period1=999302400&period2=1001376000
                // An alternative idea is to always use the Date of the last known price. But then we would not use the dividendSplit.Date at all. And what if YF has a Date data error. Then we prefer a warning of that.
                // Also, if YF has data error for prices (e.g. mistakenly doesn't have price data for the previous 3 months, then we wouldn't notice, and we would use the last price 3 months earlier. )
                // Decision: it is safer to regard only this 9/11 period as an exception. And having warning of faulty data.
                DateTime referencePriceDate = (date >= new DateTime(2001, 9, 11) && date <= new DateTime(2001, 9, 14)) ? new DateTime(2001, 9, 10) : date;
                if (!rawPrevClosesDict.TryGetValue(referencePriceDate, out SqPrice? refRawClose))
                {
                    nPotentialProblems++;
                    continue;
                }
                divSplitHistory.Add(date, new SqDivSplit { ReferenceDate = refRawClose.ReferenceDate, DividendValue = dividendTick.Amount, SplitFactor = 1.0, ReferenceRawPrice = refRawClose.Close });
            }
        }

        // Process splits if they are not null
        if (splits != null)
        {
            foreach (HpSplit splitTick in splits)
            {
                DateTime date = splitTick.DateTime.Date; // YF 'chart' API gives the Time part too for dividends, splits. E.g. "2020-10-02 9:30". We need only the .Date part
                if (date <= p_startDate) // ignore split, if split date is before startdate. E.g. GOOG 2014-04-03 startdate when there was the ticker change, and YF gives a split on that starting day too.
                    continue;

                DateTime referencePriceDate = date >= new DateTime(2001, 9, 11) && date <= new DateTime(2001, 9, 14) ? new DateTime(2001, 9, 10) : date; // see notes "Markets were closed for a week after 9/11" above

                if (!rawPrevClosesDict.TryGetValue(referencePriceDate, out SqPrice? refRawClose))
                {
                    nPotentialProblems++;
                    continue;
                }
                // Check if the key exists in the dictionary.
                if (divSplitHistory.TryGetValue(date, out SqDivSplit? divSplit)) // If the key exists, update the splitFactor property of the existing DivSplitYF object.
                    divSplit.SplitFactor = (double)splitTick.BeforeSplit / splitTick.AfterSplit;
                else // If the date is present in both the split history and the raw closes dictionary, add the extended element to the dictionary.
                    divSplitHistory.Add(date, new SqDivSplit { ReferenceDate = refRawClose.ReferenceDate, DividendValue = 0.0, SplitFactor = (double)splitTick.BeforeSplit / splitTick.AfterSplit, ReferenceRawPrice = refRawClose.Close });
            }
        }

        // Create a list from the dictionary so that it can be sorted by date.
        List<SqDivSplit> divSplitHistoryList = new();
        foreach (SqDivSplit divSplit in divSplitHistory.Values)
        {
            divSplitHistoryList.Add(divSplit);
        }

        divSplitHistoryList.Sort((SqDivSplit x, SqDivSplit y) => x.ReferenceDate.CompareTo(y.ReferenceDate));

        List<string> eventChangeLogs = new();
        if (g_isCheckHistoricalDataIntegrity)
        {
            // HistoricalDataIntegrityCheck, Step2: Read the previous event file (if exists) for comparison
            string tickerEventFilePath = p_finDataDir + $"event_files{Path.DirectorySeparatorChar}{p_ticker.ToLower()}_events.csv";
            List<SqDivSplit> oldEventData = new();
            if (File.Exists(tickerEventFilePath))
            {
                string[] lines = File.ReadAllLines(tickerEventFilePath);
                for (int i = 1; i < lines.Length; i++) // Skip header
                {
                    string[] parts = lines[i].Split(',');
                    if (parts.Length >= 4 && DateTime.TryParseExact(parts[0], "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime date))
                    {
                        oldEventData.Add(new SqDivSplit
                        {
                            ReferenceDate = date,
                            DividendValue = double.Parse(parts[1]),
                            SplitFactor = double.Parse(parts[2]),
                            ReferenceRawPrice = float.Parse(parts[3])
                        });
                    }
                }
            }

            // Create event file with pure (not comulative) dividend and split data
            if (!Directory.Exists(p_finDataDir + "event_files"))
                Directory.CreateDirectory(p_finDataDir + "event_files");

            if (File.Exists(tickerEventFilePath))
            {
                string backupFileName = p_finDataDir + $"event_files{Path.DirectorySeparatorChar}{p_ticker.ToLower()}_events_{todayDateStr}.csv";
                File.Move(tickerEventFilePath, backupFileName, true);
            }

            using TextWriter eventFileTextWriter = new StreamWriter(tickerEventFilePath);
            eventFileTextWriter.WriteLine($"# QueryDate: {todayDateStr}");
            eventFileTextWriter.WriteLine("Date,DividendValue,SplitFactor,ReferenceRawPrice");
            foreach (SqDivSplit divSplit in divSplitHistoryList)
            {
                string line = $"{divSplit.ReferenceDate:yyyyMMdd},{divSplit.DividendValue},{divSplit.SplitFactor},{divSplit.ReferenceRawPrice}";
                eventFileTextWriter.WriteLine(line);
            }
            eventFileTextWriter.Close();

            // Compare new event data with old event data
            Dictionary<DateTime, SqDivSplit> oldEventDict = new();
            foreach (SqDivSplit eventData in oldEventData)
            {
                oldEventDict.Add(eventData.ReferenceDate, eventData);
            }

            // Check for new or modified events
            foreach (SqDivSplit newEvent in divSplitHistoryList)
            {
                if (oldEventDict.TryGetValue(newEvent.ReferenceDate, out SqDivSplit? oldEvent))
                {
                    if (Math.Abs(newEvent.DividendValue - oldEvent.DividendValue) > 1e-3)
                        eventChangeLogs.Add($"{todayDateStr};{p_ticker};Event;{newEvent.ReferenceDate:yyyyMMdd};EventChange;DividendValue;{oldEvent.DividendValue};{newEvent.DividendValue}");
                    if (Math.Abs(newEvent.SplitFactor - oldEvent.SplitFactor) > 1e-3)
                        eventChangeLogs.Add($"{todayDateStr};{p_ticker};Event;{newEvent.ReferenceDate:yyyyMMdd};EventChange;SplitFactor;{oldEvent.SplitFactor};{newEvent.SplitFactor}");
                    if (Math.Abs(newEvent.ReferenceRawPrice - oldEvent.ReferenceRawPrice) > 1e-2)
                        eventChangeLogs.Add($"{todayDateStr};{p_ticker};Event;{newEvent.ReferenceDate:yyyyMMdd};EventChange;ReferenceRawPrice;{oldEvent.ReferenceRawPrice};{newEvent.ReferenceRawPrice}");
                }
                else
                {
                    eventChangeLogs.Add($"{todayDateStr};{p_ticker};Event;{newEvent.ReferenceDate:yyyyMMdd};NewEvent;DividendValue;0;{newEvent.DividendValue}");
                    eventChangeLogs.Add($"{todayDateStr};{p_ticker};Event;{newEvent.ReferenceDate:yyyyMMdd};NewEvent;SplitFactor;0;{newEvent.SplitFactor}");
                    eventChangeLogs.Add($"{todayDateStr};{p_ticker};Event;{newEvent.ReferenceDate:yyyyMMdd};NewEvent;ReferenceRawPrice;0;{newEvent.ReferenceRawPrice}");
                }
            }

            // Check for removed events
            HashSet<DateTime> newEventDates = new HashSet<DateTime>();
            foreach (SqDivSplit newEvent in divSplitHistoryList)
            {
                newEventDates.Add(newEvent.ReferenceDate);
            }
            foreach (SqDivSplit oldEvent in oldEventData)
            {
                if (!newEventDates.Contains(oldEvent.ReferenceDate))
                {
                    eventChangeLogs.Add($"{todayDateStr};{p_ticker};Event;{oldEvent.ReferenceDate:yyyyMMdd};RemovedEvent;DividendValue;{oldEvent.DividendValue};0");
                    eventChangeLogs.Add($"{todayDateStr};{p_ticker};Event;{oldEvent.ReferenceDate:yyyyMMdd};RemovedEvent;SplitFactor;{oldEvent.SplitFactor};0");
                    eventChangeLogs.Add($"{todayDateStr};{p_ticker};Event;{oldEvent.ReferenceDate:yyyyMMdd};RemovedEvent;ReferenceRawPrice;{oldEvent.ReferenceRawPrice};0");
                }
            }
        } // g_isCheckHistoricalDataIntegrity

        // Accumulate splits and dividends for factor file. Dividends have to be reverse adjusted with splits!
        List<FactorFileDivSplit> divSplitHistoryCumList = new();
        double cumDivFact = 1.0;
        double cumSplitFact = 1.0;
        for (int ticks = divSplitHistoryList.Count - 1; ticks >= 0; ticks--)
        {
            SqDivSplit currDivSplit = divSplitHistoryList[ticks];
            if (currDivSplit.DividendValue > 0)
                cumDivFact *= 1 - currDivSplit.DividendValue / cumSplitFact / currDivSplit.ReferenceRawPrice;
            if (currDivSplit.SplitFactor > 0)
                cumSplitFact *= currDivSplit.SplitFactor;
            divSplitHistoryCumList.Add(new FactorFileDivSplit() { ReferenceDate = currDivSplit.ReferenceDate, DividendFactorCum = Math.Round(cumDivFact, 8), SplitFactorCum = Math.Round(cumSplitFact, 8), ReferenceRawPrice = (float)Math.Round(currDivSplit.ReferenceRawPrice, 4) });
        }

        divSplitHistoryCumList.Sort((FactorFileDivSplit x, FactorFileDivSplit y) => x.ReferenceDate.CompareTo(y.ReferenceDate));

        // File path for factor files. Create the factor file. But before that, check that if there is already a csv for this ticker, then archive it with the first three characters of today.
        string tickerFactorFilePath = p_finDataDir + $"factor_files{Path.DirectorySeparatorChar}{p_ticker.ToLower()}.csv";
        if (File.Exists(tickerFactorFilePath))
        {
            string backupFileName = p_finDataDir + $"factor_files{Path.DirectorySeparatorChar}{p_ticker.ToLower()}_{todayDateStr.Substring(2, 6)}.csv";
            File.Move(tickerFactorFilePath, backupFileName, true);
        }

        // (List<SqDivSplit> Dividends, List<SqDivSplit> Splits) oldData = ReverseEngineerCSV(p_ticker);
        // List<FactorFileDivSplit> finalData = DiffFunction(oldData, divSplitHistoryCumList);

        TextWriter factFileTextWriter = new StreamWriter(tickerFactorFilePath);

        string firstLine = (divSplitHistoryCumList.Count > 0) ? $"{p_startDate:yyyyMMdd},{divSplitHistoryCumList[0].DividendFactorCum},{divSplitHistoryCumList[0].SplitFactorCum},1" : $"{rawClosesFromYfList[0].ReferenceDate:yyyyMMdd},1,1,1";
        factFileTextWriter.WriteLine(firstLine);
        for (int ticks = 0; ticks < divSplitHistoryCumList.Count; ticks++)
        {
            FactorFileDivSplit divSplit = divSplitHistoryCumList[ticks];
            string line = $"{divSplit.ReferenceDate:yyyyMMdd},{divSplit.DividendFactorCum},{divSplit.SplitFactorCum},{divSplit.ReferenceRawPrice}";
            factFileTextWriter.WriteLine(line);
        }
        string lastLine = $"{p_endDate:yyyyMMdd},1,1,0";
        factFileTextWriter.WriteLine(lastLine);
        factFileTextWriter.Close();
        if (nPotentialProblems > 0)
            Utils.Logger.Error($"CrawlPriceData() #{nPotentialProblems} potential problems for {p_ticker}. Investigate!");

        if (g_isCheckHistoricalDataIntegrity)
        {
            // HistoricalDataIntegrityCheck, Step3:  Write data integrity check results to log file
            if (!Directory.Exists(p_finDataDir + "data_changes_log"))
                Directory.CreateDirectory(p_finDataDir + "data_changes_log");
            string logFilePath = Path.Combine(p_finDataDir, $"data_changes_log{Path.DirectorySeparatorChar}dataChanges{DateTime.UtcNow:yyMMdd}.csv"); // e.g., "dataChanges250505.csv"
            string logCompareDir = Path.Combine(p_finDataDir, "data_changes_log");

            // Create log file with header if it doesn't exist
            if (!File.Exists(logFilePath))
                File.WriteAllText(logFilePath, "RunDate;Ticker;FileType;EventDate;ChangeType;Field;OldValue;NewValue" + Environment.NewLine);

            List<string> allChangeLogs = new();
            foreach (string priceLog in priceChangeLogs)
            {
                allChangeLogs.Add(priceLog);
            }
            foreach (string eventLog in eventChangeLogs)
            {
                allChangeLogs.Add(eventLog);
            }

            if (allChangeLogs.Count > 0)
            {
                File.AppendAllLines(logFilePath, allChangeLogs);
                Utils.Logger.Info($"Logged {allChangeLogs.Count} changes for {p_ticker} in {logFilePath}");
            }

            if (nPotentialProblems > 0)
                Utils.Logger.Error($"CrawlPriceData() #{nPotentialProblems} potential problems for {p_ticker}. Investigate!");

            // CompareEventFiles(p_ticker, p_finDataDir, "20250929", "20250930", logCompareFilePath); // Helps identify differences between two event files for specific dates.
        } // g_isCheckHistoricalDataIntegrity
        return true;
    }

    public static void CreateVirtualTickerDailyPriceFiles(string p_ticker, string p_finDataDir, out int p_lastHistDateInt) // p_ticker is "VXX" (not "VXX-SQ")
    {
        // Read the content of ticker-sq-history.csv
        string sqHistoryContent = ReadCsvFromZip(Path.GetFullPath(p_finDataDir + $"daily{Path.DirectorySeparatorChar}{p_ticker.ToLower()}-sq-history.zip"), $"{p_ticker.ToLower()}-sq-history.csv");
        p_lastHistDateInt = 0;

        // Find the largest number in the first column of ticker-sq-history.csv
        using (StringReader sqHistReader = new(sqHistoryContent))
        {
            string? lastLine = null;
            string? currentLine;

            while ((currentLine = sqHistReader.ReadLine()) != null)
                lastLine = currentLine;

            // set 'lastHistDate' based on the last line of ticker-sq-history.csv
            if (!string.IsNullOrEmpty(lastLine))
            {
                int histWhiteSpaceIndex = lastLine.IndexOf(' ');
                if (!(histWhiteSpaceIndex != -1 && int.TryParse(lastLine[..histWhiteSpaceIndex], out p_lastHistDateInt)))
                    throw new SqException($"Error. Cannot interpret lastHistDateInt in {p_ticker.ToLower()}.csv.");
            }
        }

        // Read the content of ticker.csv
        string tickerContent = ReadCsvFromZip(p_finDataDir + $"daily{Path.DirectorySeparatorChar}{p_ticker.ToLower()}.zip", $"{p_ticker.ToLower()}.csv");

        string filteredTickerContent;
        // Filter rows from ticker.csv where the number in the first column is greater than lastSqHistoryNumber
        using (StringReader tickerReader = new(tickerContent))
        using (StringWriter writer = new())
        {
            string? line;
            bool foundThreshold = false;

            while ((line = tickerReader.ReadLine()) != null)
            {
                // Find the first comma in the line
                int tickerWhiteSpaceIndex = line.IndexOf(' ');

                if (tickerWhiteSpaceIndex >= 0)
                {
                    // Try to parse the number
                    if (int.TryParse(line[..tickerWhiteSpaceIndex], out int number))
                    {
                        if (number >= p_lastHistDateInt && !foundThreshold)
                        {
                            foundThreshold = true;
                            writer.WriteLine(line);
                        }
                        else if (!foundThreshold)
                            continue; // Skip the row if we are before the threshold
                    }
                }

                writer.WriteLine(line); // Non-valid rows are added to the result
            }
            filteredTickerContent = writer.ToString();
        }

        // Merge
        string mergedCsvContent = sqHistoryContent + filteredTickerContent;

        // Create a zip file. But before that, check that if there is already a zip for this ticker, then archive it with the first three characters of today.
        string dayOfWeek = DateTime.UtcNow.AddDays(-1).DayOfWeek.ToString()[..3].ToLower();
        string zipFilePath = Path.GetFullPath(p_finDataDir + $"daily{Path.DirectorySeparatorChar}{p_ticker.ToLower()}-sq.zip");
        if (File.Exists(zipFilePath))
        {
            string backupFileName = p_finDataDir + $"daily{Path.DirectorySeparatorChar}{p_ticker.ToLower()}-sq_{dayOfWeek}.zip";
            File.Move(zipFilePath, backupFileName, true);
        }

        using FileStream zipToCreate = new(zipFilePath, FileMode.Create);
        using ZipArchive zipFile = new(zipToCreate, ZipArchiveMode.Create);
        ZipArchiveEntry innerCsvFile = zipFile.CreateEntry($"{p_ticker.ToLower()}-sq.csv");
        using StreamWriter tw = new(innerCsvFile.Open());

        tw.Write(mergedCsvContent);
        tw.Close();
    }

    public static void CreateVirtualTickerFactorFiles(string p_ticker, string p_finDataDir, int p_lastHistDateInt)
    {
        // Define the file paths for the two CSV files to merge
        string histCsvFilePath = Path.Combine(p_finDataDir, $"factor_files{Path.DirectorySeparatorChar}{p_ticker.ToLower()}-sq-history.csv");
        string actualCsvFilePath = Path.Combine(p_finDataDir, $"factor_files{Path.DirectorySeparatorChar}{p_ticker.ToLower()}.csv");

        // 1. Read splitMultiplier and dividendHist from the Actual CSV (VXX.csv)
        // List<string>? actualCsvLines = File.ReadLines(actualCsvFilePath).Skip(1).ToList(); // We can skip the first line in VXX.csv, because it only specifies the VXX startdate, but we don't use that data
        List<string> actualCsvLines = new();
        foreach (string line in File.ReadLines(actualCsvFilePath))
        {
            // Find the first comma in the line
            int commaIndex = line.IndexOf(',');
            if (commaIndex >= 0)
            {
                // Try to parse the number
                if (int.TryParse(line[..commaIndex], out int dateInt) && dateInt > p_lastHistDateInt)
                    actualCsvLines.Add(line);
            }
        }
        if (actualCsvLines.Count == 0)
            throw new SqException($"Error. Factor file {p_ticker.ToLower()}.csv should have at least 2 lines.");

        // Read the second row of the second CSV to get the dividendHist and splitMultiplier value
        double dividendHist = 1.0;
        double splitMultiplier = 1.0;

        string actualCsvSecondRow = actualCsvLines[0]; // we skipped the first row at file reading, so this is the second row
        int firstCommaIndex = actualCsvSecondRow.IndexOf(',');
        int secondCommaIndex = actualCsvSecondRow.IndexOf(',', firstCommaIndex + 1);
        int thirdCommaIndex = actualCsvSecondRow.IndexOf(',', secondCommaIndex + 1);
        if (thirdCommaIndex != -1)
        {
            if (double.TryParse(actualCsvSecondRow[(firstCommaIndex + 1)..secondCommaIndex], out double actualCsvDivValue))
                dividendHist = actualCsvDivValue;
            if (double.TryParse(actualCsvSecondRow[(secondCommaIndex + 1)..thirdCommaIndex], out double actualCsvSplitValue))
                splitMultiplier = actualCsvSplitValue;
        }

        List<string> allCsvLines = new();

        // 2. Read and modify the lines from the Historical CSV (VXX-sq-history.csv) file using splitMultiplier and dividendHist
        foreach (string line in File.ReadLines(histCsvFilePath))
        {
            string newLine = string.Empty;
            int histFirstCommaIndex = line.IndexOf(',');
            int histSecondCommaIndex = line.IndexOf(',', histFirstCommaIndex + 1);
            int histThirdCommaIndex = line.IndexOf(',', histSecondCommaIndex + 1);
            if (histThirdCommaIndex != -1)
            {
                string lineStart = line[..histFirstCommaIndex];
                if (double.TryParse(line[(histSecondCommaIndex + 1)..histThirdCommaIndex], out double splitValue))
                {
                    splitValue *= splitMultiplier;
                    newLine = lineStart + "," + dividendHist.ToString() + "," + splitValue.ToString() + line[histThirdCommaIndex..];
                }
            }
            allCsvLines.Add(newLine); // add the historical to the allCsvLines
        }

        allCsvLines.AddRange(actualCsvLines);  // add the actual to the allCsvLines

        // 3. Write VXX-sq.csv
        // File path for factor files. Create the factor file. But before that, check that if there is already a csv for this ticker, then archive it with the first three characters of today.
        string tickerFactorFilePath = p_finDataDir + $"factor_files{Path.DirectorySeparatorChar}{p_ticker.ToLower()}-sq.csv";
        if (File.Exists(tickerFactorFilePath))
        {
            string dayOfWeek = DateTime.UtcNow.AddDays(-1).DayOfWeek.ToString()[..3].ToLower();
            string backupFileName = p_finDataDir + $"factor_files{Path.DirectorySeparatorChar}{p_ticker.ToLower()}-sq_{dayOfWeek}.csv";
            File.Move(tickerFactorFilePath, backupFileName, true);
        }
        File.WriteAllLines(tickerFactorFilePath, allCsvLines);
    }

    static string ReadCsvFromZip(string p_zipFilePath, string p_csvFileName)
    {
        using FileStream zipFile = File.OpenRead(p_zipFilePath);
        using ZipArchive archive = new(zipFile, ZipArchiveMode.Read);
        ZipArchiveEntry? entry = archive.GetEntry(p_csvFileName);

        if (entry != null)
        {
            using Stream entryStream = entry.Open();
            using StreamReader reader = new(entryStream);
            return reader.ReadToEnd();
        }
        else
            throw new FileNotFoundException($"The '{p_csvFileName}' file is not found in the '{p_zipFilePath}' archive.");
    }

    // Utility function to compare 2 dates event files for a specific ticker
    public static void CompareEventFiles(string p_ticker, string p_finDataDir, string p_date1, string p_date2, string p_outputDir)
    {
        // Construct file paths for the two event files
        string eventFilePath1 = Path.Combine(p_finDataDir, $"event_files{Path.DirectorySeparatorChar}{p_ticker.ToLower()}_events_{p_date1}.csv");
        string eventFilePath2 = Path.Combine(p_finDataDir, $"event_files{Path.DirectorySeparatorChar}{p_ticker.ToLower()}_events_{p_date2}.csv");

        // Check if both files exist
        if (!File.Exists(eventFilePath1) || !File.Exists(eventFilePath2))
        {
            Utils.Logger.Error($"One or both event files not found: {eventFilePath1}, {eventFilePath2}");
            return;
        }

        // Read the first event file
        List<SqDivSplit> eventData1 = new();
        string[] lines1 = File.ReadAllLines(eventFilePath1);
        for (int i = 2; i < lines1.Length; i++) // Skip header
        {
            string[] parts = lines1[i].Split(',');
            if (parts.Length >= 4 && DateTime.TryParseExact(parts[0], "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime date))
            {
                try
                {
                    eventData1.Add(new SqDivSplit
                    {
                        ReferenceDate = date,
                        DividendValue = double.Parse(parts[1]),
                        SplitFactor = double.Parse(parts[2]),
                        ReferenceRawPrice = float.Parse(parts[3])
                    });
                }
                catch (Exception e)
                {
                    Utils.Logger.Error($"Error parsing event file {eventFilePath1}, line {i + 1}: {lines1[i]}. Exception: {e.Message}");
                }
            }
            else
            {
                Utils.Logger.Error($"Invalid format in event file {eventFilePath1}, line {i + 1}: {lines1[i]}");
            }
        }

        // Read the second event file
        List<SqDivSplit> eventData2 = new();
        string[] lines2 = File.ReadAllLines(eventFilePath2);
        for (int i = 2; i < lines2.Length; i++) // Skip header
        {
            string[] parts = lines2[i].Split(',');
            if (parts.Length >= 4 && DateTime.TryParseExact(parts[0], "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime date))
            {
                try
                {
                    eventData2.Add(new SqDivSplit
                    {
                        ReferenceDate = date,
                        DividendValue = double.Parse(parts[1]),
                        SplitFactor = double.Parse(parts[2]),
                        ReferenceRawPrice = float.Parse(parts[3])
                    });
                }
                catch (Exception e)
                {
                    Utils.Logger.Error($"Error parsing event file {eventFilePath2}, line {i + 1}: {lines2[i]}. Exception: {e.Message}");
                }
            }
            else
            {
                Utils.Logger.Error($"Invalid format in event file {eventFilePath2}, line {i + 1}: {lines2[i]}");
            }
        }

        // Prepare for comparison and logging
        List<string> changeLogs = new();
        Dictionary<DateTime, SqDivSplit> eventDict1 = new();
        for (int i = 0; i < eventData1.Count; i++)
        {
            eventDict1.Add(eventData1[i].ReferenceDate, eventData1[i]);
        }

        // Check for new or modified events (based on eventData2)
        for (int i = 0; i < eventData2.Count; i++)
        {
            SqDivSplit event2 = eventData2[i];
            if (eventDict1.TryGetValue(event2.ReferenceDate, out SqDivSplit? event1))
            {
                if (Math.Abs(event2.DividendValue - event1.DividendValue) > 1e-3)
                    changeLogs.Add($"{p_date1};{p_date2};{p_ticker};Event;{event2.ReferenceDate:yyyyMMdd};EventChange;DividendValue;{event1.DividendValue};{event2.DividendValue}");
                if (Math.Abs(event2.SplitFactor - event1.SplitFactor) > 1e-3)
                    changeLogs.Add($"{p_date1};{p_date2};{p_ticker};Event;{event2.ReferenceDate:yyyyMMdd};EventChange;SplitFactor;{event1.SplitFactor};{event2.SplitFactor}");
                if (Math.Abs(event2.ReferenceRawPrice - event1.ReferenceRawPrice) > 1e-2)
                    changeLogs.Add($"{p_date1};{p_date2};{p_ticker};Event;{event2.ReferenceDate:yyyyMMdd};EventChange;ReferenceRawPrice;{event1.ReferenceRawPrice};{event2.ReferenceRawPrice}");
            }
            else
            {
                changeLogs.Add($"{p_date1};{p_date2};{p_ticker};Event;{event2.ReferenceDate:yyyyMMdd};NewEvent;DividendValue;0;{event2.DividendValue}");
                changeLogs.Add($"{p_date1};{p_date2};{p_ticker};Event;{event2.ReferenceDate:yyyyMMdd};NewEvent;SplitFactor;0;{event2.SplitFactor}");
                changeLogs.Add($"{p_date1};{p_date2};{p_ticker};Event;{event2.ReferenceDate:yyyyMMdd};NewEvent;ReferenceRawPrice;0;{event2.ReferenceRawPrice}");
            }
        }

        // Check for removed events (based on eventData1)
        HashSet<DateTime> eventDates2 = new();
        for (int i = 0; i < eventData2.Count; i++)
        {
            eventDates2.Add(eventData2[i].ReferenceDate);
        }
        for (int i = 0; i < eventData1.Count; i++)
        {
            SqDivSplit event1 = eventData1[i];
            if (!eventDates2.Contains(event1.ReferenceDate))
            {
                changeLogs.Add($"{p_date1};{p_date2};{p_ticker};Event;{event1.ReferenceDate:yyyyMMdd};RemovedEvent;DividendValue;{event1.DividendValue};0");
                changeLogs.Add($"{p_date1};{p_date2};{p_ticker};Event;{event1.ReferenceDate:yyyyMMdd};RemovedEvent;SplitFactor;{event1.SplitFactor};0");
                changeLogs.Add($"{p_date1};{p_date2};{p_ticker};Event;{event1.ReferenceDate:yyyyMMdd};RemovedEvent;ReferenceRawPrice;{event1.ReferenceRawPrice};0");
            }
        }

        // Write the log if there are differences
        if (changeLogs.Count > 0)
        {
            // Construct the output file name using the two dates
            string logFileName = $"event_diff_{p_ticker}_{p_date1}_{p_date2}.csv";
            string fullLogFilePath = Path.Combine(p_outputDir, logFileName);

            if (!File.Exists(fullLogFilePath))
                File.WriteAllText(fullLogFilePath, "Date1;Date2;Ticker;FileType;EventDate;ChangeType;Field;OldValue;NewValue" + Environment.NewLine);
            File.AppendAllLines(fullLogFilePath, changeLogs);
            Utils.Logger.Info($"Logged {changeLogs.Count} differences between {eventFilePath1} and {eventFilePath2} in {fullLogFilePath}");
        }
    }
}