using System;
using System.Collections.Generic;
using System.Linq;

namespace YahooFinanceApi;

internal static class DataConvertors
{
    internal static bool IgnoreEmptyRows;

    internal static List<Candle> ToCandle(dynamic data, TimeZoneInfo timeZone)
    {
        List<object> timestamps = data.timestamp;
        DateTime[] dates = timestamps.Select(x => x.ToDateTime(timeZone).Date).ToArray();
        IDictionary<string, object> indicators = data.indicators;
        IDictionary<string, object> values = data.indicators.quote[0];

        if (indicators.ContainsKey("adjclose"))
            values["adjclose"] = data.indicators.adjclose[0].adjclose;

        var ticks = new List<Candle>();

        for (int i = 0; i < dates.Length; i++)
        {
            var slice = new Dictionary<string, object>();
            foreach (KeyValuePair<string, object> pair in values)
            {
                List<object> ts = (List<object>)pair.Value;
                slice.Add(pair.Key, ts[i]);
            }
            Candle? candle = CreateCandle(dates[i], slice);
            if (candle != null)
                ticks.Add(candle);
        }

        return ticks;

        Candle? CreateCandle(DateTime date, IDictionary<string, object> row)
        {
            var candle = new Candle
            {
                DateTime = date,
                Open = row.GetValueOrDefault("open").ToDecimal(),
                High = row.GetValueOrDefault("high").ToDecimal(),
                Low = row.GetValueOrDefault("low").ToDecimal(),
                Close = row.GetValueOrDefault("close").ToDecimal(),
                AdjustedClose = row.GetValueOrDefault("adjclose").ToDecimal(),
                Volume = row.GetValueOrDefault("volume").ToInt64()
            };

            if (IgnoreEmptyRows &&
                candle.Open == 0 && candle.High == 0 && candle.Low == 0 && candle.Close == 0 &&
                candle.AdjustedClose == 0 && candle.Volume == 0)
                return null;

            return candle;
        }
    }

    internal static List<DividendTick> ToDividendTick(dynamic data, TimeZoneInfo timeZone)
    {
        IDictionary<string, object> expandoObject = data;

        if (!expandoObject.ContainsKey("events"))
            return new List<DividendTick>();

        IDictionary<string, dynamic> dvdObj = data.events.dividends;
        var dividends = dvdObj.Values.Select(x => new DividendTick(ToDateTime(x.date, timeZone), ToDecimal(x.amount))).ToList();

        if (IgnoreEmptyRows)
            dividends = dividends.Where(x => x.Dividend > 0).ToList();

        return dividends;
    }

    internal static List<SplitTick> ToSplitTick(dynamic data, TimeZoneInfo timeZone)
    {
        // If there are no splits or dividends sent, then the result's 'events' field is missing in the returned json file. Trying to access it (by data.events) results a runtime RuntimeBinderException exception.
        if (!((IDictionary<string, object>)data).TryGetValue("events", out dynamic? eventsObj)) // casting dynamic to IDictionary<string, object> is CPU easy, because dynamic uses IDictionary<string, object> internally
            return new List<SplitTick>(); // return empty list
        IDictionary<string, dynamic> splitsObj = eventsObj.splits;

        // ! 100% sure that the YF API is wrong, because everybody uses the YF adjusted prices, so nobody tests this
        // row[1] is: EEM: "3:1", QQQ: "2:1". Every 1 stock before becomes 2 stocks after. (to decrease the price)
        // VXX: "1:4". Every 4 stocks before, becomes 1 stock after (to increase the price)
        // The Before (stock#) is the second one, the After is the first one.
        var splits = splitsObj.Values.Select(x => new SplitTick(ToDateTime(x.date, timeZone), ToDecimal(x.numerator), ToDecimal(x.denominator))).ToList();

        if (IgnoreEmptyRows)
            splits = splits.Where(x => x.BeforeSplit > 0 && x.AfterSplit > 0).ToList();

        return splits;
    }

    private static DateTime ToDateTime(this object obj, TimeZoneInfo timeZone)
    {
        if (obj is long lng)
        {
            return TimeZoneInfo.ConvertTimeFromUtc(DateTimeOffset.FromUnixTimeSeconds(lng).DateTime, timeZone);
        }

        throw new Exception($"Could not convert '{obj}' to DateTime.");
    }

    private static Decimal ToDecimal(this object obj)
    {
        return Convert.ToDecimal(obj);
    }

    private static Int64 ToInt64(this object obj)
    {
        return Convert.ToInt64(obj);
    }
}