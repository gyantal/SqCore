using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SqCommon;
using YahooFinanceApi;

namespace Fin.MemDb;

public partial class FinDb
{
    // private static string[] _qcOutput = { "AAPL, 19980102, Q", "TSLA, 20100629, Q", "SPY, 19980102, P", "STZ, 19980102, STZ:19980102, CBRNA:19991012, CDB:20000920, N", "HON, 19980102, HON:19980102, ALD:19991202, N" };
    // https://www.quantconnect.com/project/18836389 use the QC project SqMapFileHelper to get the tickerlist helper string from Log

    public static async void CreateTickers(string[] p_qcOutput)
    {
        // Step 1: Split the input strings and extract the tickers
        List<string> newTickers = new();
        List<string> newEntries = new();

        foreach (string value in p_qcOutput)
        {
            string ticker = value.Split(',')[0].Trim().ToUpper();
            newTickers.Add(ticker);
            newEntries.Add(value);
        }

        // Step 2: Collect the existing tickers from mapfiles
        string finDataDir = Utils.FinDataFolderPath + @"equity/usa/";
        string mapFilesDir = $"{finDataDir}map_files";
        List<string> existingTickers = new();

        foreach (string mapFilePath in Directory.GetFiles(mapFilesDir, "*.csv"))
        {
            if (mapFilePath.EndsWith("-qc.csv") || mapFilePath.EndsWith("-sq.csv"))
                continue;

            existingTickers.Add(Path.GetFileNameWithoutExtension(mapFilePath).ToUpper());
        }

        // Step 3: Identify new tickers and keep the corresponding entries
        List<string> uniqueNewEntries = newEntries
            .Where(entry => !existingTickers.Contains(entry.Split(',')[0].Trim().ToUpper()))
            .ToList();

        // Step 4: For each unique entry, pass the whole entry to CreateTicker
        foreach (string entry in uniqueNewEntries)
            await CreateTickerAsync(entry, mapFilesDir);

        // QC Cloud backtests are one-time only, but in SqCore they run for weeks. This price cache is useful intraday, but after our daily price crawler runs, we have to clear this price cache.
        QuantConnect.Lean.Engine.DataFeeds.TextSubscriptionDataSourceReader.BaseDataSourceCache.Clear();
    }
    public static async Task CreateTickerAsync(string entry, string mapFilesDir)
    {
        // Split the entry to extract relevant parts
        string[] parts = entry.Split(',').Select(p => p.Trim()).ToArray();
        string ticker = parts[0].ToUpper(); // The ticker symbol
        string initialDateStr = parts[1].Trim(); // The initial date in YYYYMMDD format
        string exchange = parts.Last().Trim().ToUpper(); // The exchange is the last element

        // Define the map file path
        string mapFilePath = Path.Combine(mapFilesDir, $"{ticker.ToLower()}.csv");

        // Create a StringBuilder to build the map file content
        StringBuilder contentBuilder = new StringBuilder();

        // Define startDateCurrTicker
        DateTime startDateCurrTicker = DateTime.MinValue;

        bool isTickerChanged = false;

        // List to store old tickers and their corresponding date ranges
        List<(string Ticker, DateTime StartDate, DateTime EndDate)> oldTickers = new();

        // Initial date to start with
        DateTime previousDate = DateTime.ParseExact(initialDateStr, "yyyyMMdd", CultureInfo.InvariantCulture);

        if (parts.Length == 3)
        {
            // Simple case: only ticker, initial date, and exchange
            contentBuilder.AppendLine($"{initialDateStr},{ticker.ToLower()},{exchange}");
            contentBuilder.AppendLine($"20501231,{ticker.ToLower()},{exchange}");
            startDateCurrTicker = DateTime.ParseExact(initialDateStr, "yyyyMMdd", CultureInfo.InvariantCulture); // Initial date as startDateCurrTicker. See comment in FinDb.Crawler.cs.
        }
        else if (parts.Length > 3)
        {
            // Complex case: ticker, initial date, exchange, and additional changes

            // First change corresponds to the initial date, but with the first ticker change
            string[] firstChange = parts[3].Split(':').Select(p => p.Trim()).ToArray();
            if (firstChange.Length == 2)
                contentBuilder.AppendLine($"{initialDateStr},{firstChange[0].ToLower()},{exchange}");

            // Process each ticker change starting from the fourth element
            for (int i = 3; i < parts.Length - 1; i++) // Process each ticker change
            {
                string[] changeParts = parts[i].Split(':').Select(p => p.Trim()).ToArray();

                if (changeParts.Length != 2)
                    throw new Exception($"Invalid ticker change format in entry: '{parts[i]}'. Expected format 'TICKER:YYYYMMDD'.");

                string changeTicker = changeParts[0].ToLower();
                string changeDateStr = changeParts[1];
                DateTime changeDate = DateTime.ParseExact(changeDateStr, "yyyyMMdd", CultureInfo.InvariantCulture);
                contentBuilder.AppendLine($"{changeDateStr},{changeTicker},{exchange}");

                // Determine the start date: use initialDate for the first item, otherwise use the end date of the last item in oldTickers
                DateTime tickerStartDate = oldTickers.Count == 0 ? previousDate.AddDays(1) : oldTickers[^1].EndDate.AddDays(1);
                // Add the ticker and its date range to the list
                oldTickers.Add((changeTicker, tickerStartDate, changeDate));

                if (i == parts.Length - 2)
                    startDateCurrTicker = DateTime.ParseExact(changeDateStr, "yyyyMMdd", CultureInfo.InvariantCulture).AddDays(1); // Initial date as startDateCurrTicker
            }

            // Add the final entry with the future date (20501231) and current ticker
            contentBuilder.AppendLine($"20501231,{ticker.ToLower()},{exchange}");

            isTickerChanged = true;
        }

        // Write the content to the map file
        File.WriteAllText(mapFilePath, contentBuilder.ToString().Trim());

        // Crawl data based on the provided initial date
        string finDataDir = Utils.FinDataFolderPath + @"equity/usa/";
        DateTime startDate = DateTime.ParseExact(initialDateStr, "yyyyMMdd", CultureInfo.InvariantCulture);
        DateTime endDate = DateTime.ParseExact("20501231", "yyyyMMdd", CultureInfo.InvariantCulture); // Set endDate to 20501231

        // Call CrawlPriceData to get the historical data
        bool isPriceOK = await CrawlPriceData(ticker, finDataDir, startDateCurrTicker, endDate);
        if (!isPriceOK)
        {
            Console.WriteLine($"Error processing price for {ticker}");
            return;
        }

        // Call CrawlFundamentalData to get fundamental data
        bool isFundamentalOK = await CrawlFundamentalData(ticker, finDataDir, startDate);
        if (!isFundamentalOK)
        {
            Console.WriteLine($"Error processing fundamental for {ticker}");
            return;
        }

        if (isTickerChanged)
        {
            // Re-download historical price data for the entire period using the custom historical data source
            var histResult = await HistPrice.g_HistPrice.GetHistAsync(ticker, HpDataNeed.OHLCV | HpDataNeed.AdjClose, startDate, startDateCurrTicker.AddDays(1));

            if (histResult.ErrorStr != null || histResult.Dates == null || histResult.AdjCloses == null || histResult.Opens == null || histResult.Closes == null || histResult.Highs == null || histResult.Lows == null || histResult.Volumes == null)
            {
                Console.WriteLine($"Error downloading historical data for {ticker}");
                return;
            }

            // Retrieve data for the specific start date
            int startIndex = Array.FindIndex(histResult.Dates, date => date.Date == startDateCurrTicker);
            if (startIndex == -1)
            {
                Console.WriteLine($"No data found for startDateCurrTicker: {startDateCurrTicker}");
                return;
            }

            // Calculate adjustment factor at startDateCurrTicker
            decimal adjFactor = (decimal)histResult.AdjCloses[startIndex] / (decimal)histResult.Closes[startIndex];

            // Open the factor file and read the first line
            string factorFilePath = Path.Combine(finDataDir, "factor_files", $"{ticker.ToLower()}.csv");
            string[] factorFileLines = File.ReadAllLines(factorFilePath);
            if (factorFileLines.Length == 0)
            {
                Console.WriteLine($"Error: Factor file for {ticker} is empty.");
                return;
            }

            // Extract the third element of the first line, which is the split factor
            string[] firstLineParts = factorFileLines[0].Split(',');
            if (firstLineParts.Length < 3 || !decimal.TryParse(firstLineParts[2], out decimal splitFactor))
            {
                Console.WriteLine($"Error: Cannot parse split factor from factor file for {ticker}.");
                return;
            }

            // Adjust the adjFactor by applying the split factor
            adjFactor *= splitFactor;

            // Use a HashSet to detect duplicate tickers
            HashSet<string> tickerSet = new();
            foreach ((string Ticker, DateTime StartDate, DateTime EndDate) oldTicker in oldTickers)
            {
                if (!tickerSet.Add(oldTicker.Ticker))
                    throw new InvalidOperationException("The same ticker appears on multiple occasions with different dates. The history file cannot be created. Resolve this manually.");
            }

            // Create the adjusted historical data for each old ticker
            foreach ((string Ticker, DateTime StartDate, DateTime EndDate) oldTicker in oldTickers)
            {
                StringBuilder oldContentBuilder = new();
                for (int i = 0; i < histResult.Dates.Length; i++)
                {
                    DateTime date = histResult.Dates[i].Date;
                    if (date < oldTicker.StartDate || date > oldTicker.EndDate)
                        continue;

                    // Calculate daily adjustment factor
                    decimal dailyAdjFactor = adjFactor / ((decimal)histResult.AdjCloses[i] / (decimal)histResult.Closes[i]);

                    // Adjust the OHLCV data
                    decimal open = Math.Round((decimal)histResult.Opens[i] / dailyAdjFactor * 10000);
                    decimal high = Math.Round((decimal)histResult.Highs[i] / dailyAdjFactor * 10000);
                    decimal low = Math.Round((decimal)histResult.Lows[i] / dailyAdjFactor * 10000);
                    decimal close = Math.Round((decimal)histResult.Closes[i] / dailyAdjFactor * 10000);
                    long volume = (long)Math.Round(histResult.Volumes[i] / dailyAdjFactor);

                    oldContentBuilder.AppendLine($"{date:yyyyMMdd} 00:00,{open},{high},{low},{close},{volume}");
                }

                // Create a ZIP file for each old ticker in the daily folder
                string zipFilePath = Path.Combine(finDataDir, "daily", $"{oldTicker.Ticker.ToLower()}.zip");
                if (File.Exists(zipFilePath))
                {
                    string backupFileName = Path.Combine(finDataDir, "daily", $"{oldTicker.Ticker.ToLower()}_{DateTime.UtcNow:yyyyMMdd}.zip");
                    File.Move(zipFilePath, backupFileName, true);
                }

                using FileStream zipToCreate = new(zipFilePath, FileMode.Create);
                using ZipArchive zipFile = new(zipToCreate, ZipArchiveMode.Create);
                ZipArchiveEntry innerCsvFile = zipFile.CreateEntry($"{oldTicker.Ticker.ToLower()}.csv");
                using StreamWriter tw = new(innerCsvFile.Open());
                tw.Write(oldContentBuilder.ToString().Trim());
            }
        }

        Console.WriteLine($"Data files created for Ticker: {ticker}.");
    }
}