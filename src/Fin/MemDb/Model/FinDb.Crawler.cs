using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading.Tasks;
using NLog;
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
        FinDb.CrawlData().TurnAsyncToSyncTask();
    }
}

public partial class FinDb
{
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
                DateTime startDate = DateTime.ParseExact(mapFilesFirstRows[i], "yyyyMMdd", CultureInfo.InvariantCulture);
                DateTime startDateCurrTicker = DateTime.ParseExact(mapFilesLastButOneRows[i], "yyyyMMdd", CultureInfo.InvariantCulture).AddDays(1);
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

        return true;
    }

    public static async Task<bool> CrawlPriceData(string p_ticker, string p_finDataDir, DateTime p_startDate, DateTime p_endDate)
    {
        int nPotentialProblems = 0;
        IReadOnlyList<Candle?>? history = await Yahoo.GetHistoricalAsync(p_ticker, p_startDate, p_endDate, Period.Daily);
        if (history == null)
        {
            Utils.Logger.Error($"Cannot download YF data (ticker:{p_ticker}) after many tries.");
            return false;
        }

        // First, collect splits from YF, because daily prices (history data) have to be reverse adjusted with the splits. YF raw prices aren't adjusted with dividends, but are adjusted with splits.
        List<SqSplit> splitList = new();
        IReadOnlyList<SplitTick?> splitHistory = await Yahoo.GetSplitsAsync(p_ticker, p_startDate.AddDays(1), null); // YF 'chart' API gives the Time part too for dividends, splits. E.g. "2020-10-02 9:30".
        foreach (SplitTick? splitTick in splitHistory)
        {
            splitList.Add(new SqSplit() { ReferenceDate = splitTick!.DateTime.Date, SplitFactor = splitTick.AfterSplit / splitTick.BeforeSplit });  // YF 'chart' API gives the Time part too for dividends, splits. E.g. "2020-10-02 9:30". We need only the .Date part
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
        IReadOnlyList<DividendTick?> dividendHistory = await Yahoo.GetDividendsAsync(p_ticker, p_startDate.AddDays(1), null);
        Dictionary<DateTime, SqDivSplit> divSplitHistory = new();
        foreach (DividendTick? dividendTick in dividendHistory)
        {
            if (dividendTick == null)
                continue;
            DateTime date = dividendTick.DateTime.Date; // YF 'chart' API gives the Time part too for dividends, splits. E.g. "2020-10-02 9:30". We need only the .Date part

            // Markets were closed for a week after 9/11, but e.g. NVDA had a split on 2001-09-12. We need to use last known price on 2001-09-10. https://finance.yahoo.com/quote/NVDA/history/?period1=999302400&period2=1001376000
            // An alternative idea is to always use the Date of the last known price. But then we would not use the dividendSplit.Date at all. And what if YF has a Date data error. Then we prefer a warning of that.
            // Also, if YF has data error for prices (e.g. mistakenly doesn't have price data for the previous 3 months, then we wouldn't notice, and we would use the last price 3 months earlier. )
            // Decision: it is safer to regard only this 9/11 period as an exception. And having warning of faulty data.
            DateTime referencePriceDate = (date >= new DateTime(2001, 9, 11) && date <= new DateTime(2001, 9, 14)) ? new DateTime(2001, 9, 10) : date;
            if (!rawPrevClosesDict.TryGetValue(referencePriceDate, out SqPrice? refRawClose)) // If the date is present in both the dividend history and the raw closes dictionary, add the extended element to the dictionary.
            {
                nPotentialProblems++;
                continue;
            }

            divSplitHistory.Add(date, new SqDivSplit() { ReferenceDate = refRawClose.ReferenceDate, DividendValue = dividendTick.Dividend, SplitFactor = 1m, ReferenceRawPrice = refRawClose.Close });
        }

        foreach (SplitTick? splitTick in splitHistory)
        {
            if (splitTick == null)
                continue;

            DateTime date = splitTick.DateTime.Date; // YF 'chart' API gives the Time part too for dividends, splits. E.g. "2020-10-02 9:30". We need only the .Date part
            DateTime referencePriceDate = (date >= new DateTime(2001, 9, 11) && date <= new DateTime(2001, 9, 14)) ? new DateTime(2001, 9, 10) : date; // see notes "Markets were closed for a week after 9/11" above
            if (!rawPrevClosesDict.TryGetValue(referencePriceDate, out SqPrice? refRawClose))
            {
                nPotentialProblems++;
                continue;
            }

            // Check if the key exists in the dictionary.
            if (divSplitHistory.TryGetValue(date, out SqDivSplit? divSplit)) // If the key exists, update the splitFactor property of the existing DivSplitYF object.
                divSplit.SplitFactor = splitTick.BeforeSplit / splitTick.AfterSplit;
            else // If the date is present in both the split history and the raw closes dictionary, add the extended element to the dictionary.
                divSplitHistory.Add(date, new SqDivSplit() { ReferenceDate = refRawClose.ReferenceDate, DividendValue = 0m, SplitFactor = splitTick.BeforeSplit / splitTick.AfterSplit, ReferenceRawPrice = refRawClose.Close });
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
            divSplitHistoryCumList.Add(new FactorFileDivSplit() { ReferenceDate = currDivSplit.ReferenceDate, DividendFactorCum = Math.Round(cumDivFact, 8), SplitFactorCum = Math.Round(cumSplitFact, 8), ReferenceRawPrice = Math.Round(currDivSplit.ReferenceRawPrice, 4) });
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
        if (nPotentialProblems > 0)
            Utils.Logger.Error($"CrawlPriceData() #{nPotentialProblems} potential problems for {p_ticker}. Investigate!");

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
}