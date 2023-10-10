using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading.Tasks;
using SqCommon;
using YahooFinanceApi;

namespace Fin.MemDb;

class SqPrice
{
    public DateTime ReferenceDate;
    public decimal Open;
    public decimal High;
    public decimal Low;
    public decimal Close;
    public long Volume;
}
class SqSplit
{
    public DateTime ReferenceDate;
    public decimal SplitFactor;
}

class SqDivSplit
{
    public DateTime ReferenceDate;
    public decimal DividendValue;
    public decimal SplitFactor;
    public decimal ReferenceRawPrice;
}

class FactorFileDivSplit
{
    public DateTime ReferenceDate;
    public decimal DividendFactorCum;
    public decimal SplitFactorCum;
    public decimal ReferenceRawPrice;
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
        Console.WriteLine($"FinDbCrawlerExecution.Run() BEGIN, Trigger: '{Trigger?.Name ?? string.Empty}'");

        Console.WriteLine(FinDb.CrawlData(false).TurnAsyncToSyncTask().ToString());
    }
}

public partial class FinDb
{
    public static void ScheduleDailyCrawlerTask()
    {
        var sqTask = new SqTask()
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

    public static async Task<StringBuilder> CrawlData(bool p_isLogHtml) // print log to Console or HTML
    {
        // Determining the appropriate data directory where the map_files, price and factor files are located.
        string finDataDir = OperatingSystem.IsWindows() ?
            AppDomain.CurrentDomain.BaseDirectory + @"..\..\..\..\..\Fin\Data\equity\usa\" :
            AppDomain.CurrentDomain.BaseDirectory + @"../FinData/equity/usa/";
        finDataDir = Path.GetFullPath(finDataDir); // GetFullPath removes the unnecessary back marching ".."

        string mapFilesDir = $"{finDataDir}map_files";
        // Collect tickers from file names. Furthermore, collect start and end dates from map_files.
        List<string> tickers = new();
        List<string> virtualTickers = new();
        List<string> mapFilesLastButOneRows = new();
        List<string> mapFilesLastRows = new();
        foreach (string mapFilePath in Directory.GetFiles(mapFilesDir, "*.csv")) // Get an array of file paths in the directory with ".csv" extension.
        {
            if (mapFilePath.EndsWith("-qc.csv")) // Ignore files ending with '-qc.csv'. It is only the spy-qc.csv. We want keep the original QC SPY data.
                continue;

            if (mapFilePath.EndsWith("-sq.csv")) // Ignore files ending with '-sq.csv'.
            {
                var sqFileName = Path.GetFileNameWithoutExtension(mapFilePath);
                int hyphenIndex = sqFileName.IndexOf('-');
                if (hyphenIndex != -1 && hyphenIndex < sqFileName.Length - 1)
                {
                    string virtualTicker = sqFileName[..hyphenIndex];
                    virtualTickers.Add(virtualTicker);
                }
                continue;
            }
            tickers.Add(Path.GetFileNameWithoutExtension(mapFilePath));
            string[] fileLines = File.ReadAllLines(mapFilePath);

            if (fileLines.Length > 1)
            {
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
        StringBuilder logSb = new();
        for (int i = 0; i < tickers.Count; i++)
        {
            string ticker = tickers[i];
            DateTime startDate = DateTime.ParseExact(mapFilesLastButOneRows[i], "yyyyMMdd", CultureInfo.InvariantCulture).AddDays(1);
            DateTime endDate = DateTime.ParseExact(mapFilesLastRows[i], "yyyyMMdd", CultureInfo.InvariantCulture);
            bool isOK = await CrawlData(ticker, p_isLogHtml, logSb, finDataDir, startDate, endDate);
            if (!isOK)
                logSb.AppendLine($"Error processing {ticker}");
        }

        // sdsd
        foreach (string ticker in virtualTickers)
        {
            CreateVirtualTickerDailyPriceFiles(ticker, finDataDir);
            // CreateVirtualTickerFactorFiles(ticker, finDataDir);
        }

        return logSb;
    }

    public static async Task<bool> CrawlData(string p_ticker, bool p_isLogHtml, StringBuilder p_logSb, string p_finDataDir, DateTime p_startDate, DateTime p_endDate)
    {
        Console.WriteLine($"FinDb.CrawlData() START with ticker: {p_ticker}");

        IReadOnlyList<Candle?>? history = await Yahoo.GetHistoricalAsync(p_ticker, p_startDate, p_endDate, Period.Daily);
        if (history == null)
        {
            if (p_isLogHtml)
                p_logSb.AppendLine($"Cannot download YF data (ticker:{p_ticker}) after many tries.</br>");
            else
                p_logSb.AppendLine($"Cannot download YF data (ticker:{p_ticker}) after many tries.");
            return false;
        }

        // First, collect splits from YF, because daily prices (history data) have to be reverse adjusted with the splits. YF raw prices aren't adjusted with dividends, but are adjusted with splits.
        List<SqSplit> splitList = new();
        IReadOnlyList<SplitTick?> splitHistory = await Yahoo.GetSplitsAsync(p_ticker, null, null);
        foreach (SplitTick? splitTick in splitHistory)
        {
            splitList.Add(new SqSplit() { ReferenceDate = splitTick!.DateTime, SplitFactor = splitTick.AfterSplit / splitTick.BeforeSplit });
        }

        // Collect YF daily price data (OHLC and Volume) - split adjusted!
        List<SqPrice> rawClosesFromYfList = new();
        foreach (Candle? candle in history)
        {
            rawClosesFromYfList.Add(new SqPrice() { ReferenceDate = candle!.DateTime, Open = candle.Open, High = candle.High, Low = candle.Low, Close = candle.Close, Volume = candle.Volume });
        }

        // Reverse adjust historical data with the splits. Going backwards in time, starting from 'today'.
        if (splitList.Count != 0)
        {
            decimal splitMultiplier = 1m;
            int lastSplitIdx = splitList.Count - 1;
            DateTime watchedSplitDate = splitList[lastSplitIdx].ReferenceDate;

            for (int i = rawClosesFromYfList.Count - 1; i >= 0; i--)
            {
                SqPrice dailyData = rawClosesFromYfList[i];
                DateTime date = dailyData.ReferenceDate;
                if (date < watchedSplitDate)
                {
                    splitMultiplier *= splitList[lastSplitIdx].SplitFactor;
                    lastSplitIdx--;
                    watchedSplitDate = (lastSplitIdx == -1) ? DateTime.MinValue : splitList[lastSplitIdx].ReferenceDate;
                }

                dailyData.Open *= splitMultiplier;
                dailyData.High *= splitMultiplier;
                dailyData.Low *= splitMultiplier;
                dailyData.Close *= splitMultiplier;
                dailyData.Volume = (long)Math.Round(dailyData.Volume / splitMultiplier);
            }
        }

        // Create a zip file. But before that, check that if there is already a zip for this ticker, then archive it with the first three characters of today.
        string dayOfWeek = DateTime.UtcNow.AddDays(-1).DayOfWeek.ToString()[..3].ToLower();
        string zipFilePath = Path.GetFullPath(p_finDataDir + $"daily{Path.DirectorySeparatorChar}{p_ticker.ToLower()}.zip");
        if (File.Exists(zipFilePath))
        {
            string backupFileName = p_finDataDir + $"daily{Path.DirectorySeparatorChar}{p_ticker.ToLower()}_{dayOfWeek}.zip";
            File.Move(zipFilePath, backupFileName, true);
        }
        // Create a dictionary of SqPrices to find dates faster.
        // Previous days date and closing prices have to be collected for factor file. These will be reference date and reference price.
        // YF vs. required QC price format: 2000-01-01 123.4567 vs. 20200101 00:00 1234567
        Dictionary<DateTime, SqPrice> rawPrevClosesDict = new();
        decimal prevClosePrice = 0m;
        DateTime prevDayDate = DateTime.MinValue;

        using FileStream zipToCreate = new(zipFilePath, FileMode.Create);
        using ZipArchive zipFile = new(zipToCreate, ZipArchiveMode.Create);
        ZipArchiveEntry innerCsvFile = zipFile.CreateEntry($"{p_ticker.ToLower()}.csv");
        using StreamWriter tw = new(innerCsvFile.Open());

        for (int days = 0; days < rawClosesFromYfList.Count; days++)
        {
            SqPrice dailyData = rawClosesFromYfList[days];
            string date = dailyData.ReferenceDate.ToString("yyyyMMdd HH:mm");
            decimal open = Math.Round(dailyData.Open * 10000);
            decimal high = Math.Round(dailyData.High * 10000);
            decimal low = Math.Round(dailyData.Low * 10000);
            decimal close = Math.Round(dailyData.Close * 10000);
            decimal volume = dailyData.Volume;

            rawPrevClosesDict.Add(dailyData.ReferenceDate, new SqPrice() { ReferenceDate = prevDayDate, Close = prevClosePrice });
            prevClosePrice = dailyData.Close;
            prevDayDate = dailyData.ReferenceDate;

            string line = $"{date},{open},{high},{low},{close},{volume}";
            tw.WriteLine(line);
        }
        tw.Close();

        // Download dividend history from YF. After that collect dividends and splits into a dictionary.
        IReadOnlyList<DividendTick?> dividendHistory = await Yahoo.GetDividendsAsync(p_ticker, null, null);
        Dictionary<DateTime, SqDivSplit> divSplitHistory = new();
        foreach (DividendTick? dividendTick in dividendHistory)
        {
            if (dividendTick != null && rawPrevClosesDict.TryGetValue(dividendTick.DateTime, out SqPrice? refRawClose)) // If the date is present in both the dividend history and the raw closes dictionary, add the extended element to the dictionary.
                divSplitHistory.Add(dividendTick.DateTime, new SqDivSplit() { ReferenceDate = refRawClose.ReferenceDate, DividendValue = dividendTick.Dividend, SplitFactor = 1m, ReferenceRawPrice = refRawClose.Close });
        }

        foreach (SplitTick? splitTick in splitHistory)
        {
            if (splitTick == null || !rawPrevClosesDict.TryGetValue(splitTick.DateTime, out SqPrice? refRawClose))
                continue;
            // Check if the key exists in the dictionary.
            if (divSplitHistory.TryGetValue(splitTick.DateTime, out SqDivSplit? divSplit)) // If the key exists, update the splitFactor property of the existing DivSplitYF object.
                divSplit.SplitFactor = splitTick.BeforeSplit / splitTick.AfterSplit;
            else // If the date is present in both the split history and the raw closes dictionary, add the extended element to the dictionary.
                divSplitHistory.Add(splitTick.DateTime, new SqDivSplit() { ReferenceDate = refRawClose.ReferenceDate, DividendValue = 0m, SplitFactor = splitTick.BeforeSplit / splitTick.AfterSplit, ReferenceRawPrice = refRawClose.Close });
        }

        // Create a list from the dictionary so that it can be sorted by date.
        List<SqDivSplit> divSplitHistoryList = new();
        foreach (SqDivSplit divSplit in divSplitHistory.Values)
        {
            divSplitHistoryList.Add(divSplit);
        }

        divSplitHistoryList.Sort((SqDivSplit x, SqDivSplit y) => x.ReferenceDate.CompareTo(y.ReferenceDate));

        // Accumulate splits and dividends for factor file. Dividends have to be reverse adjusted with splits!
        List<FactorFileDivSplit> divSplitHistoryCumList = new();
        decimal cumDivFact = 1m;
        decimal cumSplitFact = 1m;
        for (int ticks = divSplitHistoryList.Count - 1; ticks >= 0; ticks--)
        {
            SqDivSplit currDivSplit = divSplitHistoryList[ticks];
            if (currDivSplit.DividendValue > 0)
                cumDivFact *= 1 - decimal.Divide(currDivSplit.DividendValue / cumSplitFact, currDivSplit.ReferenceRawPrice);
            if (currDivSplit.SplitFactor > 0)
                cumSplitFact *= currDivSplit.SplitFactor;
            divSplitHistoryCumList.Add(new FactorFileDivSplit() { ReferenceDate = currDivSplit.ReferenceDate, DividendFactorCum = Math.Round(cumDivFact, 8), SplitFactorCum = Math.Round(cumSplitFact, 8), ReferenceRawPrice = currDivSplit.ReferenceRawPrice });
        }

        divSplitHistoryCumList.Sort((FactorFileDivSplit x, FactorFileDivSplit y) => x.ReferenceDate.CompareTo(y.ReferenceDate));

        // File path for factor files. Create the factor file. But before that, check that if there is already a csv for this ticker, then archive it with the first three characters of today.
        string tickerFactorFilePath = p_finDataDir + $"factor_files{Path.DirectorySeparatorChar}{p_ticker.ToLower()}.csv";
        if (File.Exists(tickerFactorFilePath))
        {
            string backupFileName = p_finDataDir + $"factor_files{Path.DirectorySeparatorChar}{p_ticker.ToLower()}_{dayOfWeek}.csv";
            File.Move(tickerFactorFilePath, backupFileName, true);
        }
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

        return true;
    }
    public static void CreateVirtualTickerDailyPriceFiles(string p_ticker, string p_finDataDir) // p_ticker is "VXX" (not "VXX-SQ")
    {
        // Read the content of ticker-sq-history.csv
        string sqHistoryContent = ReadCsvFromZip(Path.GetFullPath(p_finDataDir + $"daily{Path.DirectorySeparatorChar}{p_ticker}-sq-history.zip"), $"{p_ticker}-sq-history.csv");
        int lastHistDate = 0;

        // Find the largest number in the first column of ticker-sq-history.csv
        using (StringReader sqHistReader = new(sqHistoryContent))
        {
            string? lastLine = null;
            string? currentLine;

            while ((currentLine = sqHistReader.ReadLine()) != null)
                lastLine = currentLine;

            if (!string.IsNullOrEmpty(lastLine))
            {
                int histWhiteSpaceIndex = lastLine.IndexOf(' ');
                if (histWhiteSpaceIndex != -1)
                {
                    if (int.TryParse(lastLine[..histWhiteSpaceIndex], out lastHistDate))
                    {
                        // 'lastHistDate' is correctly set based on the last line of ticker-sq-history.csv
                    }
                }
            }
        }

        // Read the content of ticker.csv
        string tickerContent = ReadCsvFromZip(p_finDataDir + $"daily{Path.DirectorySeparatorChar}{p_ticker}.zip", $"{p_ticker}.csv");

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
                        if (number >= lastHistDate && !foundThreshold)
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
}