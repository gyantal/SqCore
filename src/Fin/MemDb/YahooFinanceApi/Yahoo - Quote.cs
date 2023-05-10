using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Flurl;
using Flurl.Http;

namespace YahooFinanceApi; // based on https://github.com/karlwancl/YahooFinanceApi

public sealed partial class Yahoo
{
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

        // var url = "https://query1.finance.yahoo.com/v7/finance/quote"
        var url = "https://query1.finance.yahoo.com/v6/finance/quote"
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

        var assets = new Dictionary<string, Security>();

        foreach (IDictionary<string, dynamic> dictionary in quoteExpando.result)
        {
            // Change the Yahoo field names to start with upper case.
            var pascalDictionary = dictionary.ToDictionary(x => x.Key.ToPascal(), x => x.Value);
            assets.Add(pascalDictionary["Symbol"], new Security(pascalDictionary));
        }

        return assets;
    }
}