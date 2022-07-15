using System;
using System.Globalization;

namespace YahooFinanceApi;

public static class RowExtension
{
    internal static bool IgnoreEmptyRows;

    // 2020-04-08: ET: 10-12, YF-website gives empty row ("Apr 07, 2020	-	-	-	-	-	") for yesterday, although today is good. And yesterday was good at 9:30. They do maintenance...
    public static bool IsEmptyRow(Candle candle)
    {
        return candle.Open == 0 && candle.High == 0 && candle.Low == 0 && candle.Close == 0 &&
            candle.AdjustedClose == 0 &&  candle.Volume == 0; 
    }

    internal static Candle? ToCandle(string[] row)
    {
        var candle = new Candle
        {
            DateTime      = row[0].ToDateTime(),
            Open          = row[1].ToDecimal(),
            High          = row[2].ToDecimal(),
            Low           = row[3].ToDecimal(),
            Close         = row[4].ToDecimal(),
            AdjustedClose = row[5].ToDecimal(),
            Volume        = row[6].ToInt64()
        };

        if (IgnoreEmptyRows &&
            candle.Open == 0 && candle.High == 0 && candle.Low == 0 && candle.Close == 0 &&
            candle.AdjustedClose == 0 &&  candle.Volume == 0)
            return null;

        return candle;
    }

    internal static Candle? PostprocessCandle(Candle? candle)
    {
        if (IgnoreEmptyRows &&
            candle!.Open == 0 && candle!.High == 0 && candle.Low == 0 && candle.Close == 0 &&
            candle.AdjustedClose == 0 &&  candle.Volume == 0)
            return null;

        return candle;
    }

    internal static DividendTick? ToDividendTick(string[] row)
    {
        var tick = new DividendTick
        {
            DateTime = row[0].ToDateTime(),
            Dividend = row[1].ToDecimal()
        };

        if (IgnoreEmptyRows && tick.Dividend == 0)
            return null;

        return tick;
    }

    internal static DividendTick? PostprocessDividendTick(DividendTick? tick)
    {
        if (IgnoreEmptyRows && tick!.Dividend == 0)
            return null;

        return tick;
    }

    internal static SplitTick? ToSplitTick(string[] row)
    {
        var tick = new SplitTick { DateTime = row[0].ToDateTime() };

        // var split = row[1].Split('/');   // original source code fails
        var split = row[1].Split(':');  // 2020-06-09 fix. It looks like "1:8" instead of "1/8"
        if (split.Length == 2)
        {
            tick.AfterSplit  = split[0].ToDecimal();
            tick.BeforeSplit = split[1].ToDecimal();
        }

        if (IgnoreEmptyRows && tick.AfterSplit == 0 && tick.BeforeSplit == 0)
            return null;

        return tick;
    }

    internal static SplitTick? PostprocessSplitTick(SplitTick? tick)
    {

        // var split = row[1].Split('/');   // original source code fails
        var split = tick!.StockSplits.Split(':');  // 2020-06-09 fix. It looks like "1:8" instead of "1/8"
        if (split.Length == 2)
        {
            tick.AfterSplit  = split[0].ToDecimal();
            tick.BeforeSplit = split[1].ToDecimal();
        }

        if (IgnoreEmptyRows && tick.AfterSplit == 0 && tick.BeforeSplit == 0)
            return null;

        return tick;
    }

    private static DateTime ToDateTime(this string str)
    {
        if (!DateTime.TryParse(str, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime dt))
            throw new Exception($"Could not convert '{str}' to DateTime.");
        return dt;
    }

    private static Decimal ToDecimal(this string str)
    {
        Decimal.TryParse(str, NumberStyles.Any, CultureInfo.InvariantCulture, out Decimal result);
        return result;
    }

    private static Int64 ToInt64(this string str)
    {
        Int64.TryParse(str, NumberStyles.Any, CultureInfo.InvariantCulture, out Int64 result);
        return result;
    }
}
