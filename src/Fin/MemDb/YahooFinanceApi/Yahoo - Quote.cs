using System;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Flurl;
using Flurl.Http;
using SqCommon;

namespace YahooFinanceApi; // based on https://github.com/karlwancl/YahooFinanceApi

// 2023-05-25: YF: "API-level access to Yahoo Finance quotes data has been disabled." We have to assume, YF RT will never work again. Reason. It is expensive. Pay the licenses. https://stackoverflow.com/questions/76059562/yahoo-finance-api-get-quotes-returns-invalid-cookie

public class QuoteResponse
{
    [JsonPropertyName("result")] // Do this, coz there is a performance cost using JsonSerializerOptions.PropertyNameCaseInsensitive = true
    public List<Dictionary<string, JsonElement>>? Result { get; set; } // field values can be String or Number, so dynamic is used. dynamic will be JsonElement (String or Number)

    [JsonPropertyName("error")] // Do this, coz there is a performance cost using JsonSerializerOptions.PropertyNameCaseInsensitive = true
    public string? Error { get; set; }
}

public class RtQuoteResponse // YF json field names comes as lowercase (camelCase), but C# classes uses PascalCase
{
    [JsonPropertyName("quoteResponse")] // Do this, coz there is a performance cost using JsonSerializerOptions.PropertyNameCaseInsensitive = true
    public QuoteResponse? QuoteResponse { get; set; }
}

public sealed partial class Yahoo
{
    public static bool g_doUseRoutedToUsaProxyForRealtime = true;
    private readonly List<string> fields = new();
    private string[] symbols = Array.Empty<string>();
    private Yahoo() { }

    // static!
    public static Yahoo Symbols(params string[] symbols)
    {
        if (symbols == null || symbols.Length == 0 || symbols.Any(x => x == null))
            throw new ArgumentException("Argument problem", nameof(symbols));

        return new Yahoo { symbols = symbols };
    }

    public Yahoo Fields(params string[] fields)
    {
        if (fields == null || fields.Length == 0 || fields.Any(x => x == null))
            throw new ArgumentException("Argument problem", nameof(fields));

        this.fields.AddRange(fields);

        return this;
    }

    public Yahoo Fields(params Field[] fields)
    {
        if (fields == null || fields.Length == 0)
            throw new ArgumentException("Argument problem", nameof(fields));

        this.fields.AddRange(fields.Select(f => f.ToString()));

        return this;
    }

    public async Task<IReadOnlyDictionary<string, Security>> QueryAsync(CancellationToken token = default)
    {
        if (!symbols.Any())
            throw new ArgumentException("No symbols indicated.");

        var duplicateSymbol = symbols.Duplicates().FirstOrDefault();
        if (duplicateSymbol != null)
            throw new ArgumentException($"Duplicate symbol: {duplicateSymbol}.");

        // 2023-05-10: YF realtime stopped working. see https://github.com/joshuaulrich/quantmod/issues/382
        // Pre 2023-05-10:
        // - realtime price DID NOT require crumb. https://query1.finance.yahoo.com/v7/finance/quote?symbols=QQQ
        // - historical price DID require crumb: https://query1.finance.yahoo.com/v7/finance/download/SPY&crumb=utzqhapoGQ9
        // After 2023-05-10:
        // - realtime price requires crumb. https://query1.finance.yahoo.com/v7/finance/quote?symbols=QQQ&crumb=utzqhapoGQ9
        // - historical price does NOT require crumb: https://query1.finance.yahoo.com/v7/finance/download/SPY
        // Crumb can be obtained
        // - get a YF cookie with any YF query. In some countries GDPR consent needs to be given in a popup.
        // - get crumb with https://query2.finance.yahoo.com/v1/test/getcrumb and that crumb can be used in real-time query.
        // if cookie is not sent, the https://query2.finance.yahoo.com/v1/test/getcrumb returns an empty string.
        // It is quite a hassle.
        // As a temporary workaround, the v6 API still works as it was (without crumb). So, use v6 API with realtime quote, and the v7 API for historical.

        // YF v10 API can be another PlanB in the future:
        // https://query2.finance.yahoo.com/v10/finance/quoteSummary/SPY?modules=price
        // But it is only single ticker query, not Symbols=list of 500 symbols in 1 query, so not good.
        // "it's not quite as swift as the comma-separated shares list option that's available via the current v7 API process."
        // But at least this doesn't use Crumbs. And in general it works.

        // After 2023-05-24: realtime price v6 stopped working. Yahoo has removed the v6 endpoint (that worked without crumbs) (not surprising). for people in GDPR countries.
        // If you have a VPN, an alternative is to configure it to route network traffic through it to a VPN exit server in the USA (or any country not within the GDPR).
        // https://query1.finance.yahoo.com/v6/finance/quote?symbols=QQQ In UK: "HTTP 404 Not Found". In USA: works well.
        // Our HealthMonitor server is in the USA. I will route the query to that.
        // We wrote the proxy server, but 1 day later YF stopped the v6 API not only in EU,India, but in the USA too.

        // 2023-05-25: YF: "API-level access to Yahoo Finance quotes data has been disabled." We have to assume, YF RT will never work again.
        // Reason. It is expensive. Pay the licenses. https://stackoverflow.com/questions/76059562/yahoo-finance-api-get-quotes-returns-invalid-cookie
        // https://bit.ly/yahoo-finance-api-feedback "Yahoo Finance | API Feedback: We’re sorry for the inconvenience, but API-level access to Yahoo Finance quotes data has been disabled.
        // Yahoo Finance licenses data from 3rd-party providers that do not currently authorize us to redistribute these data in API form.
        // Licenses that authorize redistribution come with a greater cost that varies depending on a number of factors, including whether the data
        // is for personal or commercial use, the type of data, the volume of queries, and additional features which may be available."
        // >For RT prices, we have to use other sources, not the YF API.
        // Maybe YF browser client user emulation (with user logged in, cookies, consent dialog, crumbs implemted if others figure it out), or IEX, or IB.

        // var url = "https://query1.finance.yahoo.com/v7/finance/quote"
        Url? url = "https://query1.finance.yahoo.com/v6/finance/quote"
            .SetQueryParam("symbols", string.Join(",", symbols));

        if (fields.Any())
        {
            var duplicateField = fields.Duplicates().FirstOrDefault();
            if (duplicateField != null)
                throw new ArgumentException($"Duplicate field: {duplicateField}.");

            url = url.SetQueryParam("fields", string.Join(",", fields.Select(s => s.ToLowerCamel())));
        }

        // Invalid symbols as part of a request are ignored by Yahoo.
        // So the number of symbols returned may be less than requested.
        // If there are no valid symbols, an exception is thrown by Flurl.
        // This exception is caught (below) and an empty dictionary is returned.
        // There seems to be no easy way to reliably identify changed symbols.

        dynamic expando = new object();

        var assets = new Dictionary<string, Security>();
        if (g_doUseRoutedToUsaProxyForRealtime)
        {
            string urlStr = url.ToString();
            string? webPage = Utils.DownloadStringRoutedToUsaProxy(urlStr /*, 5, TimeSpan.FromSeconds(2), false */).TurnAsyncToSyncTask();
            if (webPage != null)
            {
                RtQuoteResponse? jsonResponse = JsonSerializer.Deserialize<RtQuoteResponse>(webPage);
                Console.WriteLine(jsonResponse?.QuoteResponse?.Error ?? "<Error is null, which is fine.>");

                // the former official Furl version ReceiveJson() returned dynamic fields (which could be string/double/long), so we convert to match them
                if (jsonResponse?.QuoteResponse?.Result != null)
                {
                    foreach (IDictionary<string, JsonElement> dictionary in jsonResponse.QuoteResponse.Result)
                    {
                        // Change the Yahoo field names to start with upper case. YF json field names comes as lowercase (camelCase), but we use PascalCase in Dictionary
                        Dictionary<string, dynamic>? pascalDictionary = dictionary.ToDictionary(
                            x => x.Key.ToPascal(),
                            x =>
                            {
                                if (x.Value.ValueKind == JsonValueKind.Number)
                                {
                                    if (x.Value.TryGetInt64(out long val)) // regularMarketVolume is integer
                                        return val;
                                    else
                                        return x.Value.GetDouble(); // price is float
                                }
                                else
                                    return (dynamic)x.Value.ToString();
                            });
                        assets.Add(pascalDictionary["Symbol"], new Security(pascalDictionary));
                    }
                }

                // Test Results:
                Console.WriteLine($"regMktPrevClose: {(float)assets["QQQ"].RegularMarketPrice}");
                if (assets["QQQ"].Fields.TryGetValue("RegularMarketPreviousClose", out dynamic? regMktPrevClose))
                    Console.WriteLine($"regMktPrevClose: {(float)regMktPrevClose}");
            }
        }
        else
        {
            try
            {
                expando = await url
                    .GetAsync(token)
                    .ReceiveJson() // ExpandoObject
                    .ConfigureAwait(false);
            }
            catch (FlurlHttpException ex)
            {
                if (ex.Call.Response.StatusCode == (int)System.Net.HttpStatusCode.NotFound)
                    return new Dictionary<string, Security>(); // When there are no valid symbols
                else
                    throw;
            }

            var quoteExpando = expando.quoteResponse;

            var error = quoteExpando.error;
            if (error != null)
                throw new InvalidDataException($"QueryAsync error: {error}");

            foreach (IDictionary<string, dynamic> dictionary in quoteExpando.result)
            {
                // Change the Yahoo field names to start with upper case.
                var pascalDictionary = dictionary.ToDictionary(x => x.Key.ToPascal(), x => x.Value);
                assets.Add(pascalDictionary["Symbol"], new Security(pascalDictionary));
            }
        }

        return assets;
    }
}