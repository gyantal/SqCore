using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text.Json;
using System.Threading.Tasks;
using SqCommon;
using YahooFinanceApi;

namespace Fin.MemDb;

public enum FundamentalProperty
{
    CompanyReference_ShortName,
    CompanyReference_StandardName,
    CompanyProfile_MarketCap,
    CompanyProfile_SharesOutstanding
}

public partial class FinDb
{
    public static readonly Dictionary<FundamentalProperty, string> gFundamentalPropertyToStr = new() // matching "class CompanyProfile" JSON properties (we would need Reflection to get those strings from the JsonProperty attributes)
    {
        { FundamentalProperty.CompanyReference_ShortName, "2" },
        { FundamentalProperty.CompanyReference_StandardName, "3" },
        { FundamentalProperty.CompanyProfile_SharesOutstanding, "40000" },
        { FundamentalProperty.CompanyProfile_MarketCap, "40001" }
    };

    public static async Task<bool> CrawlFundamentalData(string p_ticker, string p_finDataDir, DateTime p_mapfileFirstDate, double p_shrsOutstSignifChgThresholdPct = 10.0) // p_shrsOutstSignifChgThresholdPct is a parameter, because it can be ticker dependent
    {
        // Query Yahoo Finance API for information about the given ticker
        // Companies have MarketCap, but ETFs have NetAssets (but YF doesn't update the NetAssets every day).
        // https://query1.finance.yahoo.com/v7/finance/quote?symbols=vxx&fields=symbol%2CshortName%2ClongName%2CmarketCap%2CsharesOutstanding%2CnetAssets&crumb=rd9ezFeBgc7
        IReadOnlyDictionary<string, YahooFinanceApi.Security> quotes = await Yahoo.Symbols([p_ticker])
            .Fields(new Field[] { Field.Symbol, Field.ShortName, Field.LongName, Field.RegularMarketPreviousClose, Field.SharesOutstanding, Field.MarketCap, Field.NetAssets, Field.RegularMarketPrice })
            .QueryAsync();

        // Attempt to retrieve the Security object for the specified ticker
        if (!quotes.TryGetValue(p_ticker, out YahooFinanceApi.Security? security))
        {
            Utils.Logger.Error($"Fundamental data for {p_ticker} could not be found.");
            return false;
        }

        long estimatedMarketCap = security.Fields.ContainsKey("MarketCap") ? security.MarketCap : (security.Fields.ContainsKey("NetAssets") ? (long)security.NetAssets : 0L); // Companies have MarketCap, but ETFs have NetAssets
        long estimatedSharesOutstanding = (long)(security.Fields.ContainsKey("SharesOutstanding") ? security.SharesOutstanding : estimatedMarketCap / security.RegularMarketPrice);

        // Check and create the necessary directory structure
        string fundamentalPath = Path.Combine(p_finDataDir, "fundamental", "fine", p_ticker.ToLower());
        bool directoryExists = Directory.Exists(fundamentalPath);

        if (!directoryExists)
            Directory.CreateDirectory(fundamentalPath);

        // Determine the zip file name based on whether the start date file exists
        string startDateZipFileName = $"{p_mapfileFirstDate:yyyyMMdd}.zip";
        string startDateZipFilePath = Path.Combine(fundamentalPath, startDateZipFileName);
        bool startDateZipFileExists = File.Exists(startDateZipFilePath);

        string newZipFileName = $"{DateTime.Now:yyyyMMdd}.zip";
        string newZipFilePath = Path.Combine(fundamentalPath, newZipFileName);

        if (!startDateZipFileExists)
        {
            await CreateNewFundamentalDataZipFile(p_ticker, security, startDateZipFilePath, true); // If the zip file for the start date does not exist, create it
            await CreateNewFundamentalDataZipFile(p_ticker, security, newZipFilePath, false);
            return true;
        }

        // If the directory existed and the start date file exists, check for the most recent .zip file
        string[] existingZipFiles = Directory.GetFiles(fundamentalPath, "*.zip");
        Array.Sort(existingZipFiles); // Sort the array by filename, which also sorts by date

        string lastZipFilePath = existingZipFiles[^1]; // Get the most recent zip file

        // Open and read the most recent zip file to check if updates are necessary
        using ZipArchive archive = ZipFile.OpenRead(lastZipFilePath);
        if (archive.Entries.Count <= 0)
            throw new SqException($"ERROR in CrawlFundamentalData(), ticker: {p_ticker}. Zip file is invalid. Delete the zip file ${lastZipFilePath}.");

        ZipArchiveEntry entry = archive.Entries[0]; // Directly access the first entry
        using StreamReader reader = new(entry.Open());
        string content = await reader.ReadToEndAsync();
        Dictionary<string, Dictionary<string, object>>? lastData = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, object>>>(content);

        // Convert and compare the ShortName, LongName and SharesOutstandings values
        string shortName = security.ShortName;
        string longName = security.LongName;
        bool isShortNameNeedsChange = lastData != null && shortName != string.Empty && lastData["CompanyReference"][gFundamentalPropertyToStr[FundamentalProperty.CompanyReference_ShortName]].ToString() != shortName;
        bool isLongNameNeedsChange = lastData != null && longName != string.Empty && lastData["CompanyReference"][gFundamentalPropertyToStr[FundamentalProperty.CompanyReference_StandardName]].ToString() != longName;
        bool isSharesOutstandingSignificantlyChanged = false;
        bool isEtf = false;
        if (isEtf) // ETFs don't have MarketCap, but NetAssets, but YF doesn't update the NetAssets based on the daily price. Even if VXX price changes -10%, on the next day the "NetAssets" that YF gives is the same. So, we cannot calculate sharesOutstanding based on that. So, if NetAsset didn't change since the last data, we don't have to update the file
        {
            // isSharesOutstandingSignificantlyChanged = true;
        }
        // Usual companies have MarketCap that YF refreshed daily, because they calculate that based on the lastPrice
        else if (lastData != null && lastData["CompanyProfile"].TryGetValue(gFundamentalPropertyToStr[FundamentalProperty.CompanyProfile_SharesOutstanding], out object? sharesOutstandingFromFile))
        {
            long lastSharesOutstanding = 0L;
            if (sharesOutstandingFromFile != null && long.TryParse(sharesOutstandingFromFile.ToString(), out long result))
                lastSharesOutstanding = result;
            // Check if lastSharesOutstanding is not zero to avoid division by zero
            if (lastSharesOutstanding != 0)
            {
                double changePercent = 100.0 * Math.Abs(estimatedSharesOutstanding - lastSharesOutstanding) / lastSharesOutstanding;
                isSharesOutstandingSignificantlyChanged = changePercent >= p_shrsOutstSignifChgThresholdPct;
            }
            else if (estimatedSharesOutstanding != 0) // Consider any non-zero current value as valid if the last value was zero
                isSharesOutstandingSignificantlyChanged = true;
        }
        if (lastData == null || isShortNameNeedsChange || isLongNameNeedsChange || isSharesOutstandingSignificantlyChanged)
            await CreateNewFundamentalDataZipFile(p_ticker, security, newZipFilePath, false);

        return true;
    }

    private static async Task CreateNewFundamentalDataZipFile(string p_ticker, Security p_security, string p_zipFilePath, bool p_isStartDateFile)
    {
        // Prepare JSON data matching the specified format
        string shortName = p_security.Fields.ContainsKey("ShortName") ? p_security.ShortName : string.Empty;
        string longName = p_security.Fields.ContainsKey("LongName") ? p_security.LongName : string.Empty;
        long marketCap = p_security.Fields.ContainsKey("MarketCap") ? p_security.MarketCap : (p_security.Fields.ContainsKey("NetAssets") ? (long)p_security.NetAssets : 0L);
        long sharesOutstanding = (long)(p_security.Fields.ContainsKey("SharesOutstanding") ? p_security.SharesOutstanding : marketCap / p_security.RegularMarketPrice);

        var jsonData = new // inferred Anonymous Type, which is not a List or Tuple
        {
            CompanyReference = new Dictionary<string, string>
            {
                [gFundamentalPropertyToStr[FundamentalProperty.CompanyReference_ShortName]] = shortName,
                [gFundamentalPropertyToStr[FundamentalProperty.CompanyReference_StandardName]] = longName
            },
            // Intentional decision: for new tickers, we create 2 files. 1 for the old startdate, 1 for today. Putting concrete marketCap, sharesOutstanding values to the StartDate version would be cheating and not correct. We deem it to be too dangerous. So, we write 0 values in that case to signal N/A data.
            CompanyProfile = new Dictionary<string, long>
            {
                [gFundamentalPropertyToStr[FundamentalProperty.CompanyProfile_SharesOutstanding]] = p_isStartDateFile ? 0L : sharesOutstanding,
                [gFundamentalPropertyToStr[FundamentalProperty.CompanyProfile_MarketCap]] = p_isStartDateFile ? 0L : marketCap
            }
        };

        // Serialize the data to a JSON string without indentation and escaping
        string jsonString = JsonSerializer.Serialize(jsonData, Utils.g_noEscapesJsonSerializeOpt);

        // Create and write the JSON data to a file within a zip archive
        using (FileStream fileStream = new(p_zipFilePath, FileMode.Create))
        using (ZipArchive archive = new(fileStream, ZipArchiveMode.Create, true))
        {
            ZipArchiveEntry zipEntry = archive.CreateEntry($"{p_ticker.ToLower()}.json", CompressionLevel.Optimal);
            using StreamWriter streamWriter = new StreamWriter(zipEntry.Open());
            await streamWriter.WriteAsync(jsonString);
        }

        Utils.Logger.Info($"JSON for {p_ticker} has been successfully created and zipped.");
    }

    public static string GetFundamentalDataStr(List<string> p_tickers, DateTime p_date, List<FundamentalProperty> p_propertyNames)
    {
        // Retrieve fundamental data for multiple tickers and serialize it to JSON
        Dictionary<string, Dictionary<FundamentalProperty, object>>? allData = GetFundamentalData(p_tickers, p_date, p_propertyNames);
        return JsonSerializer.Serialize(allData, Utils.g_camelJsonSerializeOpt);
    }

    // Parallel.ForEach implementation of GetFundamentalData().
    // Surprisingly, Parallel.ForEach in C# is slower than sequential when using magnetic HDD.
    // Using SSD, there is no speed difference, but we should then prefer the 1-threaded implementation for less complexity
    // First runs bencmmarks.
    // Single Thread version: 14,136.00 microsecs
    // Parallel with 10 threads, HDD: 28,189.60 microsecs. Explanation: Disks have mechanical limitations. Moving disk head to and back for 10 different files is less efficient then reading them sequentially.
    // Parallel with 10 threads, SSD: 14,830.00 microsecs. No mechanical moving head. Better than HDD. But it is not faster than single thread, because this is the max bandwidth data transfer rate of the SSD that is already used. It doesn't matter if that bandwith is used parallel or not.
    // Conclusion: Here the diskread takes 99% of the processing (that cannot be parallelized), and the CPU processing is only 1%. In these cases, Parallel.Foreach() doesn't improve, as we are bound by the hardware. There is simple not too much CPU work that can be done, that would be boosted by CPU parallelism.
    public static Dictionary<string, Dictionary<FundamentalProperty, object>> GetFundamentalDataParallel(string p_directoryPath, List<string> p_tickers, DateTime p_date, List<FundamentalProperty> p_propertyNames)
    {
        // Retrieve and aggregate fundamental data for a list of tickers using parallel processing
        Dictionary<string, Dictionary<FundamentalProperty, object>> results = new();
        Parallel.ForEach(p_tickers, new ParallelOptions { MaxDegreeOfParallelism = 10 }, ticker =>
        {
            Dictionary<FundamentalProperty, object> dataForTicker = GetFundamentalData(ticker, p_date, p_propertyNames); // Retrieve data for individual ticker
            lock (results) // Thread Safely add the data to the results dictionary
            {
                results[ticker] = dataForTicker;
            }
        });

        return results;
    }

    // Sequential, 1 thread implementation of GetFundamentalData(). Processing is hardware I/O bound. CPU parallelism doesn't improve overall speed. See GetFundamentalDataParallel() comments.
    public static Dictionary<string, Dictionary<FundamentalProperty, object>> GetFundamentalData(List<string> p_tickers, DateTime p_date, List<FundamentalProperty> p_propertyNames)
    {
        Dictionary<string, Dictionary<FundamentalProperty, object>> results = new();
        foreach (string ticker in p_tickers)
        {
            Dictionary<FundamentalProperty, object> dataForTicker = GetFundamentalData(ticker, p_date, p_propertyNames);
            results[ticker] = dataForTicker;
        }
        return results;
    }

    // if a property is missing from the data file or there is a problem converting to the proper type, then we don't put that property as a Key into the dictionary at all.
    // If a property is missing from data file => don't raise exception, don't log Warning
    // If data conversion fails (for example we expect a long, and there is a double in it), we log a Warning.
    public static Dictionary<FundamentalProperty, object> GetFundamentalData(string p_ticker, DateTime p_date, List<FundamentalProperty> p_propertyNames)
    {
        // Find the closest zip file for the given date and ticker, and throw if not found
        string? zipFilePath = FindClosestDateZipFile(Utils.FinDataFolderPath + @"equity/usa/fundamental/fine/", p_ticker, p_date) ?? throw new FileNotFoundException($"No zip file found for ticker {p_ticker}.");
        using ZipArchive archive = ZipFile.OpenRead(zipFilePath);
        ZipArchiveEntry entry = archive.Entries[0] ?? throw new FileNotFoundException($"JSON file not found in the zip file for ticker {p_ticker}.");
        using Stream stream = entry.Open();
        using StreamReader reader = new(stream);
        string jsonDataStr = reader.ReadToEnd();
        // Deserialize JSON string into a nested dictionary structure and throw if it fails
        Dictionary<string, Dictionary<string, JsonElement>> jsonData = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, JsonElement>>>(jsonDataStr) ?? throw new JsonException("Failed to deserialize JSON data."); // JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, object>>>() version gives back JsonElements in place of those objects

        Dictionary<FundamentalProperty, object> results = new();
        foreach (FundamentalProperty property in p_propertyNames)
        {
            // e.g. extract the category 'CompanyReference' from the FundamentalProperty enum 'CompanyReference_ShortName' by splitting its name string at the '_' underscore
            string key = gFundamentalPropertyToStr[property]; // Convert enum to its corresponding string key
            string propertyName = property.ToString();
            int underscoreIndex = propertyName.IndexOf('_');
            string category = underscoreIndex > -1 ? propertyName[..underscoreIndex] : propertyName; // Determine the category by substring before the underscore

            // Attempt to retrieve the value from the nested dictionary and add to results if found
            if (jsonData.TryGetValue(category, out Dictionary<string, JsonElement>? catValues) && catValues.TryGetValue(key, out JsonElement jsonElement))
            {
                object? valueObj = null;
                if (property == FundamentalProperty.CompanyProfile_SharesOutstanding || property == FundamentalProperty.CompanyProfile_MarketCap)
                {
                    if (jsonElement.TryGetInt64(out long valueLong))
                        valueObj = valueLong;
                }
                else
                    valueObj = jsonElement.GetString();

                if (valueObj != null)
                    results[property] = valueObj;
                else
                    Utils.Logger.Warn($"Failed to convert JSON value for {p_ticker}:{property}");
            }
        }

        return results;
    }
    private static string? FindClosestDateZipFile(string p_directoryPath, string p_ticker, DateTime p_targetDate)
    {
        string tickerDirectoryPath = Path.Combine(p_directoryPath, p_ticker.ToLower());

        if (!Directory.Exists(tickerDirectoryPath))
            return null;

        // Find all zip files in the directory and identify the closest file date before (or equal) the target date
        DateTime maxDate = DateTime.MinValue; // maxDate limited by p_targetDate
        foreach (string fileName in Directory.EnumerateFiles(tickerDirectoryPath, "*.zip"))
        {
            string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);

            // Parse the file name to date and check if it's the closest one before the target date
            if (DateTime.TryParseExact(fileNameWithoutExtension, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime fileDate))
            {
                if (fileDate > maxDate && fileDate <= p_targetDate) // '=' is allowed for p_targetDate checking. The daily morning crawler creates fundamental files with the date of that morning. That file should be used on that day.
                    maxDate = fileDate;
            }
        }

        return tickerDirectoryPath + @"/" + maxDate.ToYYYYMMDD() + ".zip";
    }
}