using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Flurl;
using Flurl.Http;

namespace YahooFinanceApi;

public static class ChartDataLoader
{
    public static async Task<dynamic> GetResponseStreamAsync(string symbol, DateTime startTime, DateTime endTime, Period period, string events, CancellationToken token)
    {
        var url = "https://query2.finance.yahoo.com/v8/finance/chart/"
            .AppendPathSegment(symbol)
            .SetQueryParam("period1", startTime.ToUnixTimestamp())
            .SetQueryParam("period2", endTime.ToUnixTimestamp())
            .SetQueryParam("interval", $"1{period.Name()}")
            .SetQueryParam("events", events)
            .SetQueryParam("crumb", YahooSession.Crumb);

        Debug.WriteLine(url);

        // Warning in the Python API here: https://github.com/ranaroussi/yfinance/blob/main/tests/test_ticker.py
        // "Make sure calling history to get price data has not introduced more calls to yahoo than absolutely necessary.
        // As doing other type of scraping calls than "query2.finance.yahoo.com/v8/finance/chart" to yahoo website
        // will quickly trigger spam-block when doing bulk download of history data."
        Console.WriteLine("YF API: Sleeping for 500ms to avoid throttling");
        Thread.Sleep(500); // TEMP: to avoid "The remote server returned an error: (429) Too Many Requests."

        var response = await url
            .WithCookie(YahooSession.Cookie!.Name, YahooSession.Cookie!.Value)
            .WithHeader(YahooSession.UserAgentKey, YahooSession.UserAgentValue)
            // .AllowHttpStatus("500")
            .GetAsync(token);

        // The 'dynamic' works nicely in Flurl, because flurlResponse._serializer = {Flurl.Http.Configuration.NewtonsoftJsonSerializer}
        // So, it uses Newtonsoft. And that is doing recursively all is dynamic = ExpandoObject or Dictionaries (when needed)
        // See example here: https://code-maze.com/csharp-deserialize-json-into-dynamic-object/
        // But in System.Text.Json JsonSerializer.Deserialize<ExpandoObject>(text) or <dynamic> is not SupportedJoinOperators recursively.
        // Under the hood, this returns a boxed JsonElement. So, we don’t have the convenience to use it in a truly dynamic way.
        var json = await response.GetJsonAsync();

        var error = json.chart?.error?.description;
        if (error != null)
        {
            throw new InvalidDataException($"An error was returned by Yahoo: {error}");
        }

        return json;
    }
}