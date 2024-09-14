namespace YahooFinanceApi;

// In theory, we could write a more efficient, faster implementation than this YF API (https://github.com/karlwancl/YahooFinanceApi) from GitHub
// For the JSON object that is coming from YF, this API uses 'dynamic' (using internal IDictionary<string,object> deferred on-demand evaluation only)
// So, that deferred evaluation is quite efficient already, as it avoids memcpy() by not duplicating interior part of the JSON object.
// Anyhow, we keep that dynamic based implementation and build on it.
// The official GitHub API uses 3 separate functions for historical prices, splits, dividends. That is 3 URL query. That is OK if somebody only need the adjClose data.
// But when we require Raw prices, we need all 3: price + split + dividend. We need a function that can do all 3 with 1 URL query.

// 2021-06-18T15:00 : exception thrown CsvHelper.TypeConversion.TypeConverterException
// because https://finance.yahoo.com/quote/SPY/history returns RawRecord: "2021-06-17,null,null,null,null,null,null"
// YF has intermittent data problems for yesterday.
// TODO: future work. We need a backup data source, like https://www.nasdaq.com/market-activity/funds-and-etfs/spy/historical
// or IbGateway in cases when YF doesn't give data.
// 2021-08-20: YF gives ^VIX history badly for this date for the last 2 weeks. "2021-08-09,null,null,null,null,null,null"
// They don't fix it. CBOE, WSJ has proper data for that day.

public static class RowExtension
{
    // 2020-04-08: ET: 10-12, YF-website gives empty row ("Apr 07, 2020") for yesterday, although today is good. And yesterday was good at 9:30. They do maintenance...
    public static bool IsEmptyRow(Candle candle)
    {
        return candle.Open == 0 && candle.High == 0 && candle.Low == 0 && candle.Close == 0 &&
            candle.AdjustedClose == 0 && candle.Volume == 0;
    }
}