using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Dynamic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Flurl.Http;

namespace YahooFinanceApi; // based on https://github.com/karlwancl/YahooFinanceApi

// There is a business decision what to do when 1 day in the middle is missing from the last 200 days of VIX data.
// Option 1. At the moment, we swallow it, and don't give that date-record back to the client (imitating that the source was totally bad, and missed even the date)
// Probably that is the best way to handle, so it is the caller's responsibility to check that all Date he expects is given.
// Because there is no point for us giving back the user the "2021-08-09,null,null,null,null,null,null" VIX records if it is possible that YF skips a whole day record, omitting that day.
// Checking that kind of data-error has to be done on the Caller side anyway. So we just pass the data-checking problem to the caller.
// Option 2: We can give back the Caller a "2021-08-09,null,null,null,null,null,null" record. But the caller has to check missing days anyway. So, we just increase its load.
// Option 3: We can terminate the price query, and raise an Exception. However, that we we wouldn't give anything to the Caller.
// If only 1 record is missing in the 10 years history, it is better to return the 'almost-complete' data to the Caller than returning nothing at all.

public sealed partial class Yahoo
{
    public static CultureInfo Culture = CultureInfo.InvariantCulture;
    public static bool IgnoreEmptyRows { set { DataConvertors.IgnoreEmptyRows = value; } }

    public static async Task<IReadOnlyList<Candle>> GetHistoricalAsync(string symbol, DateTime? startTime = null, DateTime? endTime = null, Period period = Period.Daily, CancellationToken token = default)
        => await GetTicksAsync(
            symbol,
            startTime,
            endTime,
            period,
            ShowOption.History,
            DataConvertors.ToCandle,
            token).ConfigureAwait(false);

    public static async Task<IReadOnlyList<DividendTick>> GetDividendsAsync(string symbol, DateTime? startTime = null, DateTime? endTime = null, CancellationToken token = default)
        => await GetTicksAsync(
            symbol,
            startTime,
            endTime,
            Period.Daily,
            ShowOption.Dividend,
            DataConvertors.ToDividendTick,
            token).ConfigureAwait(false);

    public static async Task<IReadOnlyList<SplitTick>> GetSplitsAsync(string symbol, DateTime? startTime = null, DateTime? endTime = null, CancellationToken token = default)
        => await GetTicksAsync(
            symbol,
            startTime,
            endTime,
            Period.Daily,
            ShowOption.Split,
            DataConvertors.ToSplitTick,
            token).ConfigureAwait(false);

    private static async Task<List<T>> GetTicksAsync<T>(
        string symbol,
        DateTime? startTime,
        DateTime? endTime,
        Period period,
        ShowOption showOption,
        Func<ExpandoObject, TimeZoneInfo, List<T>> converter,
        CancellationToken token)
        where T : ITick
    {
        await YahooSession.InitAsync(token);
        TimeZoneInfo symbolTimeZone = await Cache.GetTimeZone(symbol);

        startTime ??= Helper.Epoch;
        endTime ??= DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc);
        DateTime start = startTime.Value.ToUtcFrom(symbolTimeZone);
        DateTime end = endTime.Value.AddDays(2).ToUtcFrom(symbolTimeZone);

        dynamic json = await GetResponseStreamAsync(symbol, start, end, period, showOption.Name(), token).ConfigureAwait(false);
        dynamic data = json.chart.result[0];

        List<T> allData = converter(data, symbolTimeZone);
        return allData.Where(x => x != null).Where(x => x.DateTime <= endTime.Value).ToList();
    }

    private static async Task<dynamic> GetResponseStreamAsync(string symbol, DateTime startTime, DateTime endTime, Period period, string events, CancellationToken token)
    {
        bool reset = false;
        while (true)
        {
            try
            {
                return await ChartDataLoader.GetResponseStreamAsync(symbol, startTime, endTime, period, events, token).ConfigureAwait(false);
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
    }
}