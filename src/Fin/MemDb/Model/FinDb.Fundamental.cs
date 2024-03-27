using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
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

    public static async Task<bool> CrawlFundamentalData(string p_ticker, string p_finDataDir, DateTime p_mapfileFirstDate)
    {
        // Query Yahoo Finance API for information about the given ticker
        // Companies have MarketCap, but ETFs have NetAssets.
        // https://query1.finance.yahoo.com/v7/finance/quote?symbols=vxx&fields=symbol%2CshortName%2ClongName%2CmarketCap%2CsharesOutstanding%2CnetAssets&crumb=rd9ezFeBgc7
        IReadOnlyDictionary<string, YahooFinanceApi.Security> quotes = await Yahoo.Symbols([p_ticker])
            .Fields(new Field[] { Field.Symbol, Field.ShortName, Field.LongName, Field.RegularMarketPreviousClose, Field.SharesOutstanding, Field.MarketCap, Field.NetAssets })
            .QueryAsync();

        // Attempt to retrieve the Security object for the specified ticker
        if (!quotes.TryGetValue(p_ticker, out YahooFinanceApi.Security? security))
        {
            Utils.Logger.Error($"Fundamental data for {p_ticker} could not be found.");
            return false;
        }

        // Check and create the necessary directory structure
        string fundamentalPath = Path.Combine(p_finDataDir, "fundamental", "fine", p_ticker.ToLower());
        bool directoryExists = Directory.Exists(fundamentalPath);

        if (!directoryExists)
            Directory.CreateDirectory(fundamentalPath);

        // Determine the zip file name based on whether the start date file exists
        string startDateZipFileName = $"{p_mapfileFirstDate:yyyyMMdd}.zip";
        string startDateZipFilePath = Path.Combine(fundamentalPath, startDateZipFileName);
        bool startDateFileExists = File.Exists(startDateZipFilePath);

        string zipFileName = startDateFileExists ? $"{DateTime.Now:yyyyMMdd}.zip" : startDateZipFileName;
        string zipFilePath = Path.Combine(fundamentalPath, zipFileName);

        bool needToCreateNewFile = !startDateFileExists; // Assume a new file is needed if the start date file does not exist

        // If the directory existed and the start date file exists, check for the most recent .zip file
        if (directoryExists && !needToCreateNewFile)
        {
            string[] existingZipFiles = Directory.GetFiles(fundamentalPath, "*.zip");
            Array.Sort(existingZipFiles); // Sort the array by filename, which also sorts by date

            string lastZipFilePath = existingZipFiles[^1]; // Get the most recent zip file

            // Open and read the most recent zip file to check if updates are necessary
            using ZipArchive archive = ZipFile.OpenRead(lastZipFilePath);
            if (archive.Entries.Count > 0)
            {
                ZipArchiveEntry entry = archive.Entries[0]; // Directly access the first entry
                using StreamReader reader = new(entry.Open());
                string content = await reader.ReadToEndAsync();
                Dictionary<string, Dictionary<string, object>>? lastData = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, object>>>(content);

                // Convert and compare the ShortName and LongName values
                // bool isShortNameNeedsChange = (security.ShortName != string.Empty && lastData["CompanyReference"][gFundamentalPropertyToStr[FundamentalProperty.CompanyReference_ShortName]] as string != security.ShortName);
                if (lastData == null ||
                    (security.ShortName != string.Empty && lastData["CompanyReference"][gFundamentalPropertyToStr[FundamentalProperty.CompanyReference_ShortName]].ToString() != security.ShortName) ||
                    (security.LongName != string.Empty && lastData["CompanyReference"][gFundamentalPropertyToStr[FundamentalProperty.CompanyReference_StandardName]].ToString() != security.LongName))
                {
                    needToCreateNewFile = true; // No need to create a new file if the names match
                }
            }
        }

        if (needToCreateNewFile)
        {
            // Prepare JSON data matching the specified format
            long marketCap = security.Fields.ContainsKey("MarketCap") ? security.MarketCap : (security.Fields.ContainsKey("NetAssets") ? (long)security.NetAssets : 0L);
            var jsonData = new // inferred Anonymous Type, which is not a List or Tuple
            {
                CompanyReference = new Dictionary<string, string>
                {
                    [gFundamentalPropertyToStr[FundamentalProperty.CompanyReference_ShortName]] = security.Fields.ContainsKey("ShortName") ? security.ShortName : string.Empty,
                    [gFundamentalPropertyToStr[FundamentalProperty.CompanyReference_StandardName]] = security.Fields.ContainsKey("LongName") ? security.LongName : string.Empty
                },
                CompanyProfile = new Dictionary<string, long>
                {
                    [gFundamentalPropertyToStr[FundamentalProperty.CompanyProfile_SharesOutstanding]] = security.Fields.ContainsKey("SharesOutstanding") ? security.SharesOutstanding : 0L,
                    [gFundamentalPropertyToStr[FundamentalProperty.CompanyProfile_MarketCap]] = marketCap
                }
            };
            // Serialize the data to a JSON string without indentation
            string jsonString = JsonSerializer.Serialize(jsonData, new JsonSerializerOptions { WriteIndented = false });

            // Create and write the JSON data to a file within a zip archive
            using (FileStream fileStream = new(zipFilePath, FileMode.Create))
            using (ZipArchive archive = new(fileStream, ZipArchiveMode.Create, true))
            {
                ZipArchiveEntry zipEntry = archive.CreateEntry($"{p_ticker.ToLower()}.json", CompressionLevel.Optimal);
                using StreamWriter streamWriter = new StreamWriter(zipEntry.Open());
                streamWriter.Write(jsonString);
            }

            Utils.Logger.Info($"JSON for {p_ticker} has been successfully created and zipped.");
        }
        else
        {
             Utils.Logger.Info("No changes detected, no new file created.");
        }

        return true;
    }

    // T Get<T>(DateTime time, SecurityIdentifier securityIdentifier, FundamentalProperty name);
    // How to call it:
    // public string ShortName => FundamentalService.Get<string>(_timeProvider.GetUtcNow(), _securityIdentifier, FundamentalProperty.CompanyReference_ShortName);
    // FundamentalService.Get<long>(_timeProvider.GetUtcNow(), _securityIdentifier, FundamentalProperty.CompanyProfile_MarketCap);
    // public static T Get<T> GetFundamentalData(string p_ticker, DateTime p_date, FundamentalProperty p_propertyName)
    // {

    // }
}