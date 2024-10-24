using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace SqCommon;
enum ProcessedToken { Unknown, Date, AdjClose, Split, SplitDate, SplitNumerator, SplitDenominator, Dividend, DividendAmount, DividendDate, Open, Close, High, Low, Volume }
public class HistPriceYf : IHistPrice
{
    static readonly int c_throttleDelayMs = 50; // Throttle to ensure at least X ms between downloads; to avoid YF spam filter and the error "The remote server returned an error: (429) Too Many Requests."
    static readonly SemaphoreSlim _throttleSemaphore = new SemaphoreSlim(1, 1); // To allow only max 1-2 threads. "lock" would be a tiny faster, but cannot be used in async-await code, so it would block the thread. We let the thread to do other things while waiting.
    static DateTime m_lastDownloadTime = DateTime.MinValue;
    static TimeZoneInfo g_timeZoneET = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");

    public async Task<(string? ErrorStr, SqDateOnly[]? Dates, float[]? AdjCloses)> GetHistAdjCloseAsync(string p_ticker, DateTime? p_startTime = null, DateTime? p_endTime = null)
    {
        var histResult = await GetHistAsync(p_ticker, HpDataNeed.AdjClose, p_startTime, p_endTime);
        return (histResult.ErrorStr, histResult.Dates, histResult.AdjCloses);
    }

    public async Task<(string? ErrorStr, SqDateOnly[]? Dates, float[]? AdjCloses, HpSplit[]? Splits, HpDividend[]? Dividends,
        float[]? Opens, float[]? Closes, float[]? Highs, float[]? Lows, long[]? Volumes)> GetHistAsync(
        string p_ticker,
        HpDataNeed p_dataNeed = HpDataNeed.AdjClose,
        DateTime? p_startTime = null,
        DateTime? p_endTime = null,
        string p_interval = "1d", // Valid intervals: [1m, 2m, 5m, 15m, 30m, 60m, 90m, 1h, 1d, 5d, 1wk, 1mo, 3mo]
        CancellationToken token = default)
    {
        // 1. Compose the URL
        // https://query2.finance.yahoo.com/v8/finance/chart/QQQ?period1=183284531&period2=1729671731&interval=1d&events=history,split,dividend  // QQQ, 1975 to 2024-10, with splits, divs. 700KB
        // 2024-10-09: historical data for Chart API doesn't need Crumb. It even works in Incognito browser window.
        string startTimeStampStr = p_startTime?.DateTimeUtcToUnixTimeStampStr() ?? "0"; // "0" means UnixEpoch = 1970, if period1 is omitted or -1, YF returns only last 1 year
        string endTimeStampStr = p_endTime?.DateTimeUtcToUnixTimeStampStr() ?? DateTime.UtcNow.DateTimeUtcToUnixTimeStampStr();
        string events = string.Empty; // "history,split,dividend";
        if ((p_dataNeed & (HpDataNeed.AdjClose | HpDataNeed.OHLCV)) != 0)
            events += "history";
        if ((p_dataNeed & HpDataNeed.Split) == HpDataNeed.Split)
        {
            if (events == string.Empty)
                events += "split";
            else
                events += ",split";
        }
        if ((p_dataNeed & HpDataNeed.Dividend) == HpDataNeed.Dividend)
            {
            if (events == string.Empty)
                events += "dividend";
            else
                events += ",dividend";
        }
        string url = $"https://query2.finance.yahoo.com/v8/finance/chart/{p_ticker}?period1={startTimeStampStr}&period2={endTimeStampStr}&interval={p_interval}&events={events}";

        // 1. Throttle to ensure at least X ms between downloads
        await _throttleSemaphore.WaitAsync(); // Wait as only 1 thread can enter the semaphore (asynchronously). Async: so if another thread uses the semaphore, we let this Thread return and do some other work.

        TimeSpan timeSinceLastDownload = DateTime.UtcNow - m_lastDownloadTime;
        if (timeSinceLastDownload.TotalMilliseconds < c_throttleDelayMs)
        {
            int delayTime = c_throttleDelayMs - (int)timeSinceLastDownload.TotalMilliseconds;
            await Task.Delay(delayTime);  // Better than Thread.Sleep(), or Task.Delay().Wait(), that would block the thread. 'await async' allows the thread to do other tasks while waiting
        }
        m_lastDownloadTime = DateTime.UtcNow;  // Update last download time. Only AFTER waiting for the Delay

        _throttleSemaphore.Release(); // Release the semaphore lock, so other threads can enter the semaphore.

        // 2. After waiting for the necessary throttle delay, run the Download() outside of the _semaphore. This allows some parallel queries. If Download() takes 200ms, and throttle Delay is 100ms, then we will have 2 parallel threads downloading. Which is good.
        Stopwatch stopwatch = Stopwatch.StartNew();
        var histResult = ParseHistoricalData(url, p_dataNeed); // not async
        stopwatch.Stop();
        Console.WriteLine($"HistPriceYf.ParseHistData({p_ticker}): {(long)((stopwatch.ElapsedTicks * 1_000_000) / Stopwatch.Frequency):N0} microsec");
        return histResult;
    }

    // Raw json text data in utf8: QQQ: 700K, VXX: 170K.
    // 2024-10-15: In .Net8 (C# 12) Utf8JsonReader cannot be directly used in async methods because it is a ref struct. It will be fixed in .Net9 (C# 13)
    // We have to buffer and then parse the entire JSON synchronously if using Utf8JsonReader. Fine.
    // It only means we have to hold the Thread. Not releasing back to Threadpool. However, execution will be faster. So, actully it is good for speed.
    public static (string? ErrorStr, SqDateOnly[]? Dates, float[]? AdjCloses, HpSplit[]? Splits, HpDividend[]? Dividends,
        float[]? Opens, float[]? Closes, float[]? Highs, float[]? Lows, long[]? Volumes) ParseHistoricalData(string p_url, HpDataNeed p_dataNeed) // not async, because of Utf8JsonReader
    {
        HttpRequestMessage request = new() // with empty headers, it returns "429 Too Many Requests". So, if the User-Agent is empty, then it fails. Otherwise, it is OK.
        {
            RequestUri = new Uri(p_url), // e.g. https://query2.finance.yahoo.com/v8/finance/chart/QQQ?period1=0&period2=1729707659&interval=1d&events=history,split,dividend
            Method = HttpMethod.Get,
            Version = HttpVersion.Version20,
            Headers = { { "User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/125.0.0.0 Safari/537.36" } }
        };
        HttpResponseMessage response = Utils.GetHttpClientDirect().SendAsync(request).Result; // Make the synchronous HTTP request with ".Result"
        response.EnsureSuccessStatusCode();

        using Stream stream = response.Content.ReadAsStream();

        // https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/use-utf8jsonreader
        // "when there are bytes left over in the buffer, you have to pass them to the reader again. You can use BytesConsumed to determine how many bytes are left over."
        byte[] buffer = new byte[8 * 8192]; // a reasonable size: 8 * 8192 = 65536 = 65K. If buffer is smaller, we can start processing data sooner. QQQ (25years) OLHCV: 700KB, VXX (6years): 170KB, mostStocks (2 years fix, only AdjClose): 55KB
        int bytesRead;
        int previousBytesRemaining = 0;
        byte[]? leftoverBuffer = null;
        JsonReaderState readerState = new JsonReaderState(new JsonReaderOptions { CommentHandling = JsonCommentHandling.Skip });

        ProcessedToken processedToken = ProcessedToken.Unknown;
        List<SqDateOnly>? dates = null;
        if ((p_dataNeed & (HpDataNeed.AdjClose | HpDataNeed.OHLCV)) != 0)
            dates = new(260 * 10); // assume 260 trading days for 10 years data. To reduce memcpy reallocation overhead.
        List<float>? adjcloses = null;
        if ((p_dataNeed & HpDataNeed.AdjClose) == HpDataNeed.AdjClose)
            adjcloses = new(260 * 10); // assume 260 trading days for 10 years data. To reduce memcpy reallocation overhead.

        List<HpSplit>? splits = null;
        if ((p_dataNeed & HpDataNeed.Split) == HpDataNeed.Split)
            splits = new();
        HpSplit? currentSplit = null;
        int splitsProcessDepth = 0;

        List<HpDividend>? dividends = null;
        if ((p_dataNeed & HpDataNeed.Dividend) == HpDataNeed.Dividend)
            dividends = new();
        HpDividend? currentDividend = null;
        int dividendsProcessDepth = 0;

        List<float>? opens = null;
        List<float>? closes = null;
        List<float>? highs = null;
        List<float>? lows = null;
        List<long>? volumes = null;
        if ((p_dataNeed & HpDataNeed.OHLCV) == HpDataNeed.OHLCV) // only allocate RAM, if it is needed
        {
            opens = new(260 * 10); // assume 260 trading days for 10 years data. To reduce memcpy reallocation overhead.
            closes = new(260 * 10);
            highs = new(260 * 10);
            lows = new(260 * 10);
            volumes = new(260 * 10);
        }

        while ((bytesRead = stream.Read(buffer, previousBytesRemaining, buffer.Length - previousBytesRemaining)) > 0) // read new chunks, but if there is leftover from previous buffers, then keep a gap at the front of the buffer
        {
            // If there's leftover data, combine it with the newly read bytes. Put the leftovers to the front of the buffer.
            if (leftoverBuffer != null && previousBytesRemaining > 0)
            {
                leftoverBuffer.AsSpan(0, previousBytesRemaining).CopyTo(buffer);
                bytesRead += previousBytesRemaining;
                leftoverBuffer = null; // No longer needed since it’s now merged
            }

            Span<byte> jsonSpan = buffer.AsSpan(0, bytesRead);
            Utf8JsonReader reader = new(jsonSpan, isFinalBlock: false, readerState); // create a new reader for the buffer, use previous readerState

            while (reader.Read())
            {
                switch (reader.TokenType)
                {
                    case JsonTokenType.PropertyName:
                        string? propertyName = reader.GetString();

                        if (propertyName == "timestamp" && (p_dataNeed & (HpDataNeed.AdjClose | HpDataNeed.OHLCV)) != 0)
                            processedToken = ProcessedToken.Date;
                        else if (propertyName == "adjclose" && (p_dataNeed & HpDataNeed.AdjClose) == HpDataNeed.AdjClose)
                            processedToken = ProcessedToken.AdjClose;
                        else if (propertyName == "splits" && (p_dataNeed & HpDataNeed.Split) == HpDataNeed.Split)
                        {
                            processedToken = ProcessedToken.Split;
                            splitsProcessDepth = 0;
                        }
                        else if (propertyName == "dividends" && (p_dataNeed & HpDataNeed.Dividend) == HpDataNeed.Dividend)
                        {
                            processedToken = ProcessedToken.Dividend;
                            dividendsProcessDepth = 0;
                        }
                        else if (propertyName == "open" && (p_dataNeed & HpDataNeed.OHLCV) == HpDataNeed.OHLCV)
                            processedToken = ProcessedToken.Open;
                        else if (propertyName == "close" && (p_dataNeed & HpDataNeed.OHLCV) == HpDataNeed.OHLCV)
                            processedToken = ProcessedToken.Close;
                        else if (propertyName == "high" && (p_dataNeed & HpDataNeed.OHLCV) == HpDataNeed.OHLCV)
                            processedToken = ProcessedToken.High;
                        else if (propertyName == "low" && (p_dataNeed & HpDataNeed.OHLCV) == HpDataNeed.OHLCV)
                            processedToken = ProcessedToken.Low;
                        else if (propertyName == "volume" && (p_dataNeed & HpDataNeed.OHLCV) == HpDataNeed.OHLCV)
                            processedToken = ProcessedToken.Volume;

                        if (processedToken == ProcessedToken.Split || processedToken == ProcessedToken.SplitDate || processedToken == ProcessedToken.SplitNumerator)
                        {
                            if (propertyName == "date")
                                processedToken = ProcessedToken.SplitDate;
                            else if (propertyName == "numerator")
                                processedToken = ProcessedToken.SplitNumerator;
                            else if (propertyName == "denominator")
                                processedToken = ProcessedToken.SplitDenominator;
                        }
                        if (processedToken == ProcessedToken.Dividend || processedToken == ProcessedToken.DividendAmount)
                        {
                            if (propertyName == "amount")
                                processedToken = ProcessedToken.DividendAmount;
                            else if (propertyName == "date")
                                processedToken = ProcessedToken.DividendDate;
                        }
                        break;
                    case JsonTokenType.Number:
                        if (processedToken == ProcessedToken.Date)
                        {
                            long timestamp = reader.GetInt64();
                            // YF API correctly use a TimeZone Cache and do 1 extra query per symbol for getting data.chart.result[0].meta.exchangeTimezoneName;
                            // TimeZoneInfo symbolTimeZone = await Cache.GetTimeZone(symbol); // we will need this If we use nonUSA stocks, but not necessary now
                            // TODO: we can also get the timezone from the currently processed JSON.
                            DateTime dtUtc = DateTimeOffset.FromUnixTimeSeconds(timestamp).DateTime; // UTC 14:30
                            DateTime dtEt = TimeZoneInfo.ConvertTimeFromUtc(dtUtc, g_timeZoneET); // ET: 9:30 (TBH, this time zone conversion is not necessary, because we only use it as date, not time)
                            dates!.Add(new SqDateOnly(dtEt.Date));
                        }
                        else if (processedToken == ProcessedToken.AdjClose)
                            adjcloses!.Add(reader.GetSingle());
                        else if (processedToken == ProcessedToken.SplitDate)
                            currentSplit!.DateTime = TimeZoneInfo.ConvertTimeFromUtc(DateTimeOffset.FromUnixTimeSeconds(reader.GetInt64()).DateTime, g_timeZoneET);
                        else if (processedToken == ProcessedToken.SplitNumerator)
                            currentSplit!.AfterSplit = (int)reader.GetSingle(); // I see, '"numerator": 1' in browser text, but byte data arrived has '"numerator": 1.0' instead (somehow). So process as float.
                        else if (processedToken == ProcessedToken.SplitDenominator)
                            currentSplit!.BeforeSplit = (int)reader.GetSingle();
                        else if (processedToken == ProcessedToken.DividendAmount)
                            currentDividend!.Amount = reader.GetSingle();
                        else if (processedToken == ProcessedToken.DividendDate)
                            currentDividend!.DateTime = TimeZoneInfo.ConvertTimeFromUtc(DateTimeOffset.FromUnixTimeSeconds(reader.GetInt64()).DateTime, g_timeZoneET);
                        else if (processedToken == ProcessedToken.Open)
                            opens!.Add(reader.GetSingle());
                        else if (processedToken == ProcessedToken.Close)
                            closes!.Add(reader.GetSingle());
                        else if (processedToken == ProcessedToken.High)
                            highs!.Add(reader.GetSingle());
                        else if (processedToken == ProcessedToken.Low)
                            lows!.Add(reader.GetSingle());
                        else if (processedToken == ProcessedToken.Volume)
                            volumes!.Add(reader.GetInt64());
                        break;
                    case JsonTokenType.Null: // e.g. ^VIX data contains nulls in adjCloses: "...19.4699993133545, null, 18.3500003814697..."
                        if (processedToken == ProcessedToken.AdjClose)
                            adjcloses!.Add(float.NaN);
                        break;
                    case JsonTokenType.EndArray:
                        if (processedToken == ProcessedToken.Date || processedToken == ProcessedToken.AdjClose || processedToken == ProcessedToken.Open || processedToken == ProcessedToken.Close
                            || processedToken == ProcessedToken.High || processedToken == ProcessedToken.Low || processedToken == ProcessedToken.Volume)
                            processedToken = ProcessedToken.Unknown;
                        break;
                    case JsonTokenType.StartObject:
                        if (processedToken == ProcessedToken.Split)
                        {
                            splitsProcessDepth++;
                            if (splitsProcessDepth == 2)
                                currentSplit = new HpSplit();
                        }
                        else if (processedToken == ProcessedToken.Dividend)
                        {
                            dividendsProcessDepth++;
                            if (dividendsProcessDepth == 2)
                                currentDividend = new HpDividend();
                        }
                        break;
                    case JsonTokenType.EndObject:
                        if (processedToken == ProcessedToken.SplitDenominator) // the last token in a Split record
                        {
                            splitsProcessDepth--;
                            splits!.Add(currentSplit!);
                            currentSplit = null;
                            processedToken = ProcessedToken.Split; // go back to parent processing
                        }
                        else if (processedToken == ProcessedToken.Split)
                        {
                            splitsProcessDepth--; // it is actually ProcessDepth = 0; (from 1). We are leaving the main parent node.
                            processedToken = ProcessedToken.Unknown;
                        }
                        else if (processedToken == ProcessedToken.DividendDate) // the last token in a Dividend record
                        {
                            dividendsProcessDepth--;
                            dividends!.Add(currentDividend!);
                            currentDividend = null;
                            processedToken = ProcessedToken.Dividend; // go back to parent processing
                        }
                        else if (processedToken == ProcessedToken.Dividend)
                        {
                            dividendsProcessDepth--; // it is actually ProcessDepth = 0; (from 1). We are leaving the main parent node.
                            processedToken = ProcessedToken.Unknown;
                        }
                        break;
                    case JsonTokenType.String:
                        // Console.WriteLine($"String value: {reader.GetString()}");
                        break;
                }
            }

            // Update reader state for the next chunk of data
            readerState = reader.CurrentState;

            // Determine how many bytes were consumed and how many are leftover
            int bytesConsumed = (int)reader.BytesConsumed;
            previousBytesRemaining = bytesRead - bytesConsumed;

            if (previousBytesRemaining > 0)
            {
                // Copy leftover bytes to a temporary buffer for use in the next loop iteration
                leftoverBuffer = new byte[previousBytesRemaining];
                buffer.AsSpan(bytesConsumed, previousBytesRemaining).CopyTo(leftoverBuffer);
            }
        }

        if (previousBytesRemaining > 0) // If there's any remaining data in the leftover buffer, we should handle it. But it should never happen if input is a proper JSON
            Utils.Logger.Error($"HistPriceParser(): Potential Error. Remaining data should be 0: previousBytesRemaining: {previousBytesRemaining}");

        // Perform some checks here to ensure the data is valid
        if (dates != null && adjcloses != null && dates.Count != adjcloses.Count)
        {
            Utils.Logger.Error($"HistPriceParser(): Potential Error. dates.Count != adjcloses.Count");
            return (" Potential Error. dates.Count != adjcloses.Count", null, null, null, null, null, null, null, null, null);
        }

        return (null, dates?.ToArray(), adjcloses?.ToArray(), splits?.ToArray(), dividends?.ToArray(), opens?.ToArray(), closes?.ToArray(), highs?.ToArray(), lows?.ToArray(), volumes?.ToArray());
    }
}