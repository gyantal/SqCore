using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using CsvHelper;
using Flurl;
using Flurl.Http;
using SqCommon;

namespace YahooFinanceApi; // based on https://github.com/karlwancl/YahooFinanceApi

public sealed partial class Yahoo
{
    public static CultureInfo Culture = CultureInfo.InvariantCulture;
    public static bool IgnoreEmptyRows { set { RowExtension.IgnoreEmptyRows = value; } }

    public static async Task<IReadOnlyList<Candle?>> GetHistoricalAsync(string symbol, DateTime? startTime = null, DateTime? endTime = null, Period period = Period.Daily, CancellationToken token = default)
        => await GetTicksAsync<Candle?>(
            symbol,
            startTime,
            endTime,
            period,
            ShowOption.History,
            RowExtension.ToCandle,
            token).ConfigureAwait(false);

    public static async Task<IReadOnlyList<DividendTick?>> GetDividendsAsync(string symbol, DateTime? startTime = null, DateTime? endTime = null, CancellationToken token = default)
        => await GetTicksAsync<DividendTick?>(
            symbol,
            startTime,
            endTime,
            Period.Daily,
            ShowOption.Dividend,
            RowExtension.ToDividendTick,
            token).ConfigureAwait(false);

    public static async Task<IReadOnlyList<SplitTick?>> GetSplitsAsync(string symbol, DateTime? startTime = null, DateTime? endTime = null, CancellationToken token = default)
        => await GetTicksAsync<SplitTick?>(
            symbol,
            startTime,
            endTime,
            Period.Daily,
            ShowOption.Split,
            RowExtension.ToSplitTick,
            token).ConfigureAwait(false);

    private static async Task<List<TTick>> GetTicksAsync<TTick>(
        string symbol,
        DateTime? startTime,
        DateTime? endTime,
        Period period,
        ShowOption showOption,
        Func<string[], TTick> instanceFunction,
        CancellationToken token)
    {
        // It was in pre 2023 code, probably not needed, although the NewLine on Windows is suspicios, and YF servers are Linux compatible
        // var csvConfig = new CsvHelper.Configuration.CsvConfiguration(CultureInfo.InvariantCulture)
        // {
        //     NewLine = "\n",     // instead of the default Environment.NewLine which is double
        //     Delimiter = ",",
        //     Quote = '\'',
        //     MissingFieldFound = null // ignore CsvHelper.MissingFieldException, because SplitTick 2 computed fields will be missing.
        // };
        var ticks = new List<TTick>();

        const int nMaxTries = 3;    // YF server is unreliable, as all web queries. It is worth repeating the download at least 3 times.
        int nTries = 0;
        do
        {
            try
            {
                nTries++;

                using var stream = await GetResponseStreamAsync(symbol, startTime, endTime, period, showOption.Name(), token).ConfigureAwait(false);
                using var sr = new StreamReader(stream);
                using var csvReader = new CsvReader(sr, Culture);
                // using var csvReader = new CsvReader(sr, csvConfig);
                csvReader.Read(); // skip header

                while (csvReader.Read())
                {
                    // 2021-06-18T15:00 : exception thrown CsvHelper.TypeConversion.TypeConverterException
                    // because https://finance.yahoo.com/quote/SPY/history returns RawRecord: "2021-06-17,null,null,null,null,null,null"
                    // YF has intermittent data problems for yesterday.
                    // TODO: future work. We need a backup data source, like https://www.nasdaq.com/market-activity/funds-and-etfs/spy/historical
                    // or IbGateway in cases when YF doesn't give data.
                    // 2021-08-20: YF gives ^VIX history badly for this date for the last 2 weeks. "2021-08-09,null,null,null,null,null,null"
                    // They don't fix it. CBOE, WSJ has proper data for that day.
                    TTick? tick = default;
                    try
                    {
                        tick = instanceFunction(csvReader.Context.Parser.Record!);
                    }
                    catch (Exception e) // "The conversion cannot be performed."  RawRecord:\r\n2021-08-09,null,null,null,null,null,null\n\r\n"
                    {
                        Utils.Logger.Warn(e, $"Warning in Yahoo.GetTicksAsync(): Stock:'{symbol}', {e.Message}");
                        // There is a business decision what to do when 1 day in the middle is missing from the last 200 days of VIX data.
                        // Option 1. At the moment, we swallow it, and don't give that date-record back to the client (imitating that the source was totally bad, and missed even the date)
                        // Probably that is the best way to handle, so it is the caller's responsibility to check that all Date he expects is given.
                        // Because there is no point for us giving back the user the "2021-08-09,null,null,null,null,null,null" VIX records if it is possible that YF skips a whole day record, omitting that day.
                        // Checking that kind of data-error has to be done on the Caller side anyway. So we just pass the data-checking problem to the caller.
                        // Option 2: We can give back the Caller a "2021-08-09,null,null,null,null,null,null" record. But the caller has to check missing days anyway. So, we just increase its load.
                        // Option 3: We can terminate the price query, and raise an Exception. However, that we we wouldn't give anything to the Caller.
                        // If only 1 record is missing in the 10 years history, it is better to return the 'almost-complete' data to the Caller than returning nothing at all.
                    }
#pragma warning disable RECS0017 // Possible compare of value type with 'null'
                    if (tick != null)
#pragma warning restore RECS0017 // Possible compare of value type with 'null'
                        ticks.Add(tick);
                }

                return ticks;
            }
            catch (Exception e)
            {
                Utils.Logger.Info(e, $"Exception in Yahoo.GetTicksAsync(): Stock:'{symbol}', {e.Message}");
                Thread.Sleep(3000); // sleep for 3 seconds before trying again.
            }
        }
        while (nTries <= nMaxTries);

        throw new Exception($"ReloadHistoricalDataAndSetTimer() exception. Cannot download YF data (ticker:{symbol}) after {nMaxTries} tries.");
    }

    private static async Task<Stream> GetResponseStreamAsync(string symbol, DateTime? startTime, DateTime? endTime, Period period, string events, CancellationToken token)
    {
        bool reset = false;
        while (true)
        {
            try
            {
                await YahooSession.InitAsync(reset, token);
                return await _GetResponseStreamAsync(token).ConfigureAwait(false);
            }
            catch (FlurlHttpException ex) when (ex.Call.Response?.StatusCode == (int)HttpStatusCode.NotFound)
            {
                throw new Exception($"Invalid ticker or endpoint for symbol '{symbol}'.", ex);
            }
            catch (FlurlHttpException ex) when (ex.Call.Response?.StatusCode == (int)HttpStatusCode.Unauthorized)
            {
                Debug.WriteLine("GetResponseStreamAsync: Unauthorized.");

                if (reset)
                    throw;
                reset = true; // try again with a new client
            }
        }

        Task<Stream> _GetResponseStreamAsync(CancellationToken token)
        {
            // Yahoo expects dates to be "Eastern Standard Time"
            startTime = startTime?.FromEstToUtc() ?? new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            endTime = endTime?.FromEstToUtc() ?? DateTime.UtcNow;

            var url = "https://query1.finance.yahoo.com/v7/finance/download"
                .AppendPathSegment(symbol)
                .SetQueryParam("period1", startTime.Value.ToUnixTimestamp())
                .SetQueryParam("period2", endTime.Value.ToUnixTimestamp())
                .SetQueryParam("interval", $"1{period.Name()}")
                .SetQueryParam("events", events)
                .SetQueryParam("crumb", YahooSession.Crumb);

            Debug.WriteLine(url);

            return url
                .WithCookie(YahooSession.Cookie!.Name, YahooSession.Cookie.Value)
                .GetAsync(token)
                .ReceiveStream();
        }
    }
}