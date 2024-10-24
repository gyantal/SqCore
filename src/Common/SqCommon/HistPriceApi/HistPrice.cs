using System;
using System.Threading;
using System.Threading.Tasks;

namespace SqCommon;

public class HpSplit // Hp prefix is HistoricalPrice
{
    public DateTime DateTime { get; set; } // Time part is needed. It can be 9:30 ET
    public int BeforeSplit { get; set; }
    public int AfterSplit { get; set; }
}

public sealed class HpDividend
{
    public DateTime DateTime { get; set; } // Time part is needed. It can be 9:30 ET
    public float Amount { get; set; }
}

[Flags] // allows combining multiple enum values using bitwise OR (|).
public enum HpDataNeed // each option a unique power of 2.
{
    AdjClose = 1, // only AdjClose is needed
    OHLCV = 2, // Open, High, Low, Close, Volume
    Dividend = 4,
    Split = 8
}

public interface IHistPrice // hiding the implementation behind an interface. Concrete implementations can be: YahooFinance, InteractiveBroker
{
    // A simpler version that returns only the required 2 output arrays: Dates, AdjCloses
    Task<(string? ErrorStr, SqDateOnly[]? Dates, float[]? AdjCloses)> GetHistAdjCloseAsync(string p_ticker, DateTime? p_startTime = null, DateTime? p_endTime = null);

    // A complex version that returns all 9 output arrays
    Task<(string? ErrorStr, SqDateOnly[]? Dates, float[]? AdjCloses, HpSplit[]? Splits, HpDividend[]? Dividends, float[]? Opens, float[]? Closes, float[]? Highs, float[]? Lows, long[]? Volumes)> GetHistAsync(
        string p_ticker,
        HpDataNeed p_dataNeed = HpDataNeed.AdjClose,
        DateTime? p_startTime = null,
        DateTime? p_endTime = null,
        string p_interval = "1d", // Valid intervals: [1m, 2m, 5m, 15m, 30m, 60m, 90m, 1h, 1d, 5d, 1wk, 1mo, 3mo]
        CancellationToken token = default);
}

public class HistPrice
{
    public static IHistPrice g_HistPrice = new HistPriceYf(); // or = new HistPriceIb(); or a dynamic switch between them as a fallback

    public static async void TestGetHist()
    {
        var histResult = await HistPrice.g_HistPrice.GetHistAsync("QQQ", HpDataNeed.AdjClose | HpDataNeed.Split | HpDataNeed.Dividend | HpDataNeed.OHLCV);
        // var histResult = await HistPrice.g_HistPrice.GetHistAsync("QQQ", DataNeed.AdjClose); // returns all 9 output arrays
        // var histResult = await HistPrice.g_HistPrice.GetHistAdjCloseAsync("QQQ"); // returns only the required 2 output arrays: Dates, AdjCloses
        if (histResult.ErrorStr != null)
        {
            Utils.Logger.Error($"HistPrice Error: {histResult.ErrorStr}");
            return;
        }
        SqDateOnly[]? dates = histResult.Dates;
        float[]? adjCloses = histResult.AdjCloses;
    }
}