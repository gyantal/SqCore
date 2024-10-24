using System;
using System.Threading;
using System.Threading.Tasks;

namespace SqCommon;
public class HistPriceIb : IHistPrice
{
    public async Task<(string? ErrorStr, SqDateOnly[]? Dates, float[]? AdjCloses)> GetHistAdjCloseAsync(string p_ticker, DateTime? p_startTime = null, DateTime? p_endTime = null)
    {
        await Task.Delay(1); // dummy for eliminating the compiler warning 'This async method lacks await operators'
        throw new NotImplementedException();
    }

    public async Task<(string? ErrorStr, SqDateOnly[]? Dates, float[]? AdjCloses, HpSplit[]? Splits, HpDividend[]? Dividends,
        float[]? Opens, float[]? Closes, float[]? Highs, float[]? Lows, long[]? Volumes)> GetHistAsync(
        string p_ticker,
        HpDataNeed p_dataNeed = HpDataNeed.AdjClose,
        DateTime? p_startTime = null,
        DateTime? p_endTime = null,
        string p_interval = "1d",
        CancellationToken token = default)
    {
        await Task.Delay(1); // dummy for eliminating the compiler warning 'This async method lacks await operators'
        throw new NotImplementedException();
    }
}