using System;
using System.Collections.Generic;
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
        // Companies have MarketCap, but ETFs have NetAssets.
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

        long estimatedMarketCap = security.Fields.ContainsKey("MarketCap") ? security.MarketCap : (security.Fields.ContainsKey("NetAssets") ? (long)security.NetAssets : 0L);
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
        bool isShortNameNeedsChange = lastData != null && security.ShortName != string.Empty && lastData["CompanyReference"][gFundamentalPropertyToStr[FundamentalProperty.CompanyReference_ShortName]].ToString() != security.ShortName;
        bool isLongNameNeedsChange = lastData != null && security.LongName != string.Empty && lastData["CompanyReference"][gFundamentalPropertyToStr[FundamentalProperty.CompanyReference_StandardName]].ToString() != security.LongName;
        bool isSharesOutstandingSignificantlyChanged = false;
        if (lastData != null && lastData["CompanyProfile"].TryGetValue(gFundamentalPropertyToStr[FundamentalProperty.CompanyProfile_SharesOutstanding], out object? sharesOutstandingFromFile))
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
        // Serialize the data to a JSON string without indentation
        string jsonString = JsonSerializer.Serialize(jsonData, new JsonSerializerOptions { WriteIndented = false });

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

    // T Get<T>(DateTime time, SecurityIdentifier securityIdentifier, FundamentalProperty name);
    // How to call it:
    // public string ShortName => FundamentalService.Get<string>(_timeProvider.GetUtcNow(), _securityIdentifier, FundamentalProperty.CompanyReference_ShortName);
    // FundamentalService.Get<long>(_timeProvider.GetUtcNow(), _securityIdentifier, FundamentalProperty.CompanyProfile_MarketCap);
    // public static T Get<T> GetFundamentalData(string p_ticker, DateTime p_date, FundamentalProperty p_propertyName)
    // {

    // }
}