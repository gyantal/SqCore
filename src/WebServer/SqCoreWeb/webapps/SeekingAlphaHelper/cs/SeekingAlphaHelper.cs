using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using SqCommon;

[ApiController]
[Route("[controller]")]
[ResponseCache(CacheProfileName = "NoCache")]
public class SeekingAlphaHelperController : ControllerBase
{
    public class StockData
    {
        public string Ticker { get; set; } = string.Empty;
        public float SaQuantRating { get; set; }
    }

    #pragma warning disable CA1822 // "Mark members as static". Kestrel Controller methods that is called as an URL has to be instance methods, not static.
    [HttpGet] // only 1 HttpGet attribute should be in the Controller (or you have to specify in it how to resolve)
    public IActionResult Get([FromQuery] string dataSelector)
    {
        if (string.IsNullOrEmpty(dataSelector))
            return BadRequest(new { errorMsg = "dataSelector parameter is required." });
        string result = ExtractDataBySelector(dataSelector); // Process the request based on the dataSelector value
        return Ok(result);
    }

    private string ExtractDataBySelector(string p_dataSelector) // p_dataSelector : topStocks or topAnalysts
    {
        if (p_dataSelector == "topStocks")
        {
            string stockHistDict = ExtractStocksRawHistData2Dict();
            return stockHistDict;
        }
        else if (p_dataSelector == "topAnalysts")
            return "Top Analysts Data"; // return data for top analysts
        else
            return "Unknown dataSelector value.";
    }

    private string ExtractStocksRawHistData2Dict()
    {
        // string url = "https://drive.google.com/uc?export=download&id=1-1HZBrjO4HihpJvk47vxtgBylGeZ0At-";
        // string? rawTopStocksData = Utils.DownloadStringWithRetryAsync(url).TurnAsyncToSyncTask();
        string url = string.Empty;
        Utils.BenchmarkElapsedTime("ExtractStocksRawHistData2Dict(): for initializing the URL", () =>
        {
            url = "https://drive.google.com/uc?export=download&id=1-1HZBrjO4HihpJvk47vxtgBylGeZ0At-";
        });
        string? rawTopStocksData = null;
        Utils.BenchmarkElapsedTime("ExtractStocksRawHistData2Dict(): for downloading the raw stocks data", () =>
        {
            rawTopStocksData = Utils.DownloadStringWithRetryAsync(url).TurnAsyncToSyncTask();
        });
        if (string.IsNullOrEmpty(rawTopStocksData) || rawTopStocksData.Contains("Error"))
            return "Error in DownloadStringWithRetry()";
        string topStocksStr = string.Empty;
        Utils.BenchmarkElapsedTime("ExtractStocksRawHistData2Dict(): converting raw srocks to required string format", () =>
        {
            Dictionary<DateTime, List<StockData>> topStocksDict = TopStocksRawHistData2Dict(rawTopStocksData);
            topStocksStr = TopStocksDict2Str(topStocksDict);
        });
        return topStocksStr;
    }

    public Dictionary<DateTime, List<StockData>> TopStocksRawHistData2Dict(string p_topStocksHistData)
    {
        Dictionary<DateTime, List<StockData>> topStocksDict = new();
        ReadOnlySpan<char> span = p_topStocksHistData.AsSpan();
        int i = 0;
        while (i < span.Length)
        {
            int dateStartInd = span.Slice(i).IndexOf("***"); // Find the start of a new date block
            if (dateStartInd == -1)
                break;

            i += dateStartInd + "***".Length; // Adjust `i` to the actual start of the date marker
            int dateEndInd = span.Slice(i).IndexOf('\n');            // Find the end of the date line
            if (dateEndInd == -1)
                break;

            ReadOnlySpan<char> dateSpan = span.Slice(i, dateEndInd - " UTC".Length).Trim(); // Parse the date. dateStr = "2024-10-14T12:00 UTC" => dateStr = "2024-10-14T12:00";
            DateTime dateTime = Utils.Str2DateTimeUtc(dateSpan.ToString());
            i += dateEndInd + 1;

            // Prepare a new list of StockData
            List<StockData> stockList = new();
            int recordNumber = 1;
            while (i < span.Length)
            {
                string recordMarker = $"\r\n{recordNumber}\r\n"; // Find the start of the record marker
                int recordStart = span.Slice(i).IndexOf(recordMarker);
                if (recordStart == -1)
                    break; // No more records found

                i += recordStart + recordMarker.Length;
                int tickerEnd = span.Slice(i).IndexOf("\r\n");
                if (tickerEnd == -1)
                    break; // No ticker found

                ReadOnlySpan<char> tickerSpan = span.Slice(i, tickerEnd); // Extract the ticker symbol
                i += tickerEnd + "\r\n".Length;

                string strongBuyMarker = "Rating: Strong Buy"; // Move to find the first occurrence of "Rating: Strong Buy"
                int ratingStart = span.Slice(i).IndexOf(strongBuyMarker);

                if (ratingStart != -1)
                {
                    i += ratingStart + strongBuyMarker.Length;
                    int ratingEnd = span.Slice(i).IndexOf('\t'); // Find the end of the rating value which is followed by a tab character
                    if (ratingEnd != -1)
                    {
                        ReadOnlySpan<char> ratingSpan = span.Slice(i, ratingEnd);
                        i += ratingEnd + 1; // Move past the tab character
                        if (float.TryParse(ratingSpan.ToString(), out float rating))
                            stockList.Add(new StockData { Ticker = tickerSpan.ToString(), SaQuantRating = rating });
                    }
                }
                recordNumber++;
            }

            topStocksDict[dateTime] = stockList;

            int nextDateMarkerInd = span.Slice(i).IndexOf("***"); // Move `i` to the next block after the current records
            if (nextDateMarkerInd == -1)
                break; // No more date markers found, exit the loop
            i += nextDateMarkerInd; // Set `i` to the start of the next date marker
        }
        return topStocksDict;
    }

    public string TopStocksDict2Str(Dictionary<DateTime, List<StockData>> p_topStocksDict)
    {
        StringBuilder sbTopStocksDict = new();

        // creating the dictionary string with proper formatting for readability.
        // Using tabs (\t) and newlines (\n) for better formatting when copying into QuantConnect's QCStrategy.
        sbTopStocksDict.Append("Dictionary<DateTime, List<StockData>> recommendations = new() \n\t\t{\n");

        // Initialize counters to keep track of the current entry position
        int topStocksDictCount = p_topStocksDict.Count;
        int currentStockCount = 0;

        foreach (KeyValuePair<DateTime, List<StockData>> topStocks in p_topStocksDict.OrderByDescending(r => r.Key)) // Iterate through the dictionary, ordered by the date key in descending order (latest date first)
        {
            DateTime date = topStocks.Key; // Get the current date (key)
            List<StockData> stocks = topStocks.Value; // Get the list of stocks (value)

            // Append the dictionary entry for DateTime with the exact year, month, day, and time.
            sbTopStocksDict.Append("\t\t\t{ ");
            sbTopStocksDict.Append($"new DateTime({date.Year}, {date.Month}, {date.Day}, {date.Hour}, {date.Minute}, {date.Second}), ");
            sbTopStocksDict.Append("new List<StockData> {\n\t\t\t\t"); // Start the list of StockData entries.

            // Iterate through the stocks for the current date.
            for (int i = 0; i < stocks.Count; i++)
            {
                StockData stock = stocks[i]; // Current stock data
                sbTopStocksDict.Append($"new StockData(\"{stock.Ticker}\", {stock.SaQuantRating})"); // Append StockData entry with ticker and rating.

                if (i < stocks.Count - 1) // Append a comma and space after the stock, but not for the last stock in the list.
                    sbTopStocksDict.Append(", ");

                // Adjust the index to a 1-based, since the loop index starts from 0
                if ((i + 1) % 3 == 0) // Add a newline and tab after every 3 stocks for better visual formatting.
                    sbTopStocksDict.Append("\n\t\t\t\t");
            }

            currentStockCount++; // Increment the current stock count after processing all stocks for the current date.
            // Close the current dictionary entry, adding a comma if there are more entries left.
            if (currentStockCount < topStocksDictCount)
                sbTopStocksDict.Append("}\n\t\t\t},\n"); // Closing the List<StockData> and DateTime entry, add comma
            else
                sbTopStocksDict.Append("}\n\t\t\t}\n"); // For the last entry, close without a comma
        }

        sbTopStocksDict.Append("\t\t};"); // Append the closing bracket for the dictionary
        return sbTopStocksDict.ToString();
    }
}