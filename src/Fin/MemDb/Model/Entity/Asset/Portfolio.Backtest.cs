using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using QuantConnect;
using QuantConnect.Data;
using QuantConnect.Data.Market;
using QuantConnect.Lean.Engine.Results;
using QuantConnect.Parameters;
using QuantConnect.Securities;
using QuantConnect.Statistics;
using SqCommon;

namespace Fin.MemDb;

public class PortfolioRunResultStatistics
{
    [JsonPropertyName("startPv")]
    public float StartPortfolioValue { get; set; } = 0.0f;
    [JsonPropertyName("endPv")]
    public float EndPortfolioValue { get; set; } = 0.0f;
    [JsonPropertyName("tr")]
    public float TotalReturn { get; set; } = 0.0f;
    public float CAGR { get; set; } = 0.0f;
    public float MaxDD { get; set; } = 0.0f;
    [JsonPropertyName("shrp")]
    [JsonConverter(typeof(FloatJsonConverterToNumber4D))]
    public float SharpeRatio { get; set; } = 0.0f;
    public float StDev { get; set; } = 0.0f;
    public float Ulcer { get; set; } = 0.0f;
    public int TradingDays { get; set; } = 0;
    public int NTrades { get; set; } = 0; // number of trades
    [JsonPropertyName("wr")]
    public float WinRate { get; set; } = 0.0f;
    [JsonPropertyName("lr")]
    public float LossRate { get; set; } = 0.0f;
    [JsonPropertyName("srtn")]
    [JsonConverter(typeof(FloatJsonConverterToNumber4D))]
    public float Sortino { get; set; } = 0;
    [JsonPropertyName("to")]
    public float Turnover { get; set; } = 0.0f;
    [JsonPropertyName("ls")]
    public float LongShortRatio { get; set; } = 0.0f;
    public float Fees { get; set; } = 0.0f;
    [JsonPropertyName("bCAGR")]
    public float BenchmarkCAGR { get; set; } = 0.0f;
    [JsonPropertyName("bMax")]
    public float BenchmarkMaxDD { get; set; } = 0.0f;
    [JsonPropertyName("cwb")]
    public float CorrelationWithBenchmark { get; set; } = 0.0f;
}

public enum ChartResolution
{
    Second, Minute, Minute5, Hour, Daily, Weekly, Monthly
}

public class PortfolioPosition
{
    public string SqTicker { get; set; } = string.Empty;
    public int Quantity { get; set; } = -1;
    public float AvgPrice { get; set; } = 0.0f;
    public float LastPrice { get; set; } = 0.0f;  // the last price of the asset at the end of the backtest (not real-time price)
}

public class PriceHistoryJs // To save bandwidth, we send Dates, and Prices just as a List, instead of a List of <Date,Price> objects that would add property names thousands of times into JSON
{
    public List<string> Dates { get; set; } = new();
    public List<float> Prices { get; set; } = new();
}

[DebuggerDisplay("{Id}, Name:{Name}, User:{User?.Username??\"-NoUser-\"}")]
public partial class Portfolio : Asset // this inheritance makes it possible that a Portfolio can be part of an Uber-portfolio
{
    // PortfolioValue chart data.
    // We have the option to return Date fields in different formats in JSON string:
    // '2021-01-27' is 10 chars, '20210127' is 8 chars. Resolution is only daily.
    // Or number of seconds from Unix epoch: '1641013200' is 10 chars. Resolution can be 1 second.
    // Although it is 2 chars more data, but we chose this, because QC uses it and also it will allow us to go intraday in the future.
    // Also it allows to show the user how up-to-date (real-time) the today value is.
    public string? GetPortfolioRunResult(SqResult p_sqResult, out PortfolioRunResultStatistics p_stat, out List<ChartPoint> p_pv, out List<PortfolioPosition> p_prtfPoss, out ChartResolution p_chartResolution)
    {
        #pragma warning disable IDE0066 // disable the switch suggestion warning only locally
        switch (Type)
        {
            case PortfolioType.Simulation:
                return GetBacktestResult(p_sqResult, out p_stat, out p_pv, out p_prtfPoss, out p_chartResolution);
            case PortfolioType.Trades:
            case PortfolioType.TradesSqClassic:
            default:
                return GetPortfolioRunResultDefault(out p_stat, out p_pv, out p_prtfPoss, out p_chartResolution);
        }
        #pragma warning restore IDE0066
    }

    public static string? GetPortfolioRunResultDefault(out PortfolioRunResultStatistics p_stat, out List<ChartPoint> p_pv, out List<PortfolioPosition> p_prtfPoss, out ChartResolution p_chartResolution)
    {
        List<ChartPoint> pvs = new()
        {
            new ChartPoint(1641013200, 101665),
            new ChartPoint(1641099600, 101487),
            new ChartPoint(1641186000, 101380),
            new ChartPoint(1641272400, 101451),
            new ChartPoint(1641358800, 101469),
            new ChartPoint(1641445200, 101481),
            new ChartPoint(1641531600, 101535),
            new ChartPoint(1641618000, 101416),
            new ChartPoint(1641704400, 101392),
            new ChartPoint(1641790800, 101386)
        }; // 5 or 10 real values.

        p_pv = pvs; // output
        p_stat = new PortfolioRunResultStatistics
        {
            StartPortfolioValue = 1000.0f,
            EndPortfolioValue = 1400.0f,
            SharpeRatio = 0.8f
        }; // output
        List<PortfolioPosition> prtfPoss = new ()
        {
            new PortfolioPosition { SqTicker = "S/Spy", Quantity = 1, AvgPrice = 1.0f, LastPrice = 1.0f },
            new PortfolioPosition { SqTicker = "S/TQQQ", Quantity = 1, AvgPrice = 1.0f, LastPrice = 1.0f }
        }; // output
        p_prtfPoss = prtfPoss;
        p_chartResolution = ChartResolution.Daily;
        return null; // No Error
    }

    public string? GetBacktestResult(SqResult p_sqResult, out PortfolioRunResultStatistics p_stat, out List<ChartPoint> p_pv, out List<PortfolioPosition> p_prtfPoss, out ChartResolution p_chartResolution)
    {
        p_stat = new PortfolioRunResultStatistics();
        p_pv = new List<ChartPoint>();
        p_prtfPoss = new List<PortfolioPosition>();
        p_chartResolution = ChartResolution.Daily;

        string algorithmName = String.IsNullOrEmpty(Algorithm) ? "BasicTemplateFrameworkAlgorithm" : Algorithm;
        BacktestingResultHandler backtestResults = Backtester.BacktestInSeparateThreadWithTimeout(algorithmName, AlgorithmParam, @"{""ema-fast"":10,""ema-slow"":20}", p_sqResult);
        if (backtestResults == null)
            return "Error in Backtest";

        Console.WriteLine("BacktestResults.LogStore (from Algorithm)"); // we can force the Trade Logs into a text file. ("SaveListOfTrades(AlgorithmHandlers.Transactions, csvTransactionsFileName);"). But our Algo also can put it into the LogStore
        backtestResults.LogStore.ForEach(r => Console.WriteLine(r.Message)); // Trade Logs. "Time: 10/07/2013 13:31:00 OrderID: 1 EventID: 2 Symbol: SPY Status: Filled Quantity: 688 FillQuantity: 688 FillPrice: 144.7817 USD OrderFee: 3.44 USD"

        Console.WriteLine($"BacktestResults.PV. startPV:{backtestResults.StartingPortfolioValue:N0}, endPV:{backtestResults.DailyPortfolioValue:N0} ({(backtestResults.DailyPortfolioValue / backtestResults.StartingPortfolioValue - 1) * 100:N2}%)");

        List<ChartPoint> equityChart = backtestResults.Charts["Strategy Equity"].Series["Equity"].Values;
        Console.WriteLine($"#Charts:{backtestResults.Charts.Count}. The Equity (PV) chart: {equityChart[0].y:N0}, {equityChart[1].y:N0} ... {equityChart[^2].y:N0}, {equityChart[^1].y:N0}");

        // With Minute resolution simulation, the PV chart is generated at every 5 minutes. But the first point of the day is UTC 4:00, then 13:31, 13:36, 13:41,...
        if (equityChart.Count >= 3) // because the first is a dummy point, we need at least 3 data points to decide.
        {
            int diffBetween2points = (int)(equityChart[2].x - equityChart[1].x);
            if (diffBetween2points <= 60)
                p_chartResolution = ChartResolution.Minute;
            else if (diffBetween2points <= 300)
                p_chartResolution = ChartResolution.Minute5;
            else
                p_chartResolution = ChartResolution.Daily;
        }

        if (p_chartResolution == ChartResolution.Daily)
        {
            // Eliminate daily chart duplicates. There is 1 point for weekends, but 2 points (morning, marketclose) for the weekdays. We keep only the last Y value for the day.
            DateTime currentDate = DateTime.MinValue; // initialize currentDate to the smallest possible value
            for (int i = 0; i < equityChart.Count; i++)
            {
                ChartPoint item = equityChart[i];
                // convert the Unix timestamp (item.x) to a DateTime object and take only the date part
                DateTime itemDate = DateTimeOffset.FromUnixTimeSeconds(item.x).DateTime.Date;
                if (itemDate != currentDate) // if this is a new date, add a new point to p_pv
                {
                    p_pv.Add(new ChartPoint { x = item.x, y = item.y });
                    currentDate = itemDate; // set currentDate to the new date
                }
                else // if this is the same date as the previous point, update the existing point
                {
                    ChartPoint lastVal = p_pv[^1]; // get the last point in p_pv
                    lastVal.y = item.y;
                    lastVal.x = item.x;
                }
            }
        }
        else // PerMinute Data
        {
            for (int i = 0; i < equityChart.Count; i++)
            {
                p_pv.Add(new ChartPoint { x = equityChart[i].x, y = equityChart[i].y });
            }
        }

        if (p_sqResult != SqResult.SqPvOnly)
        {
            Dictionary<string, string> finalStat = backtestResults.FinalStatistics;
            var statisticsStr = $"{Environment.NewLine}" + $"{string.Join(Environment.NewLine, finalStat.Select(x => $"STATISTICS:: {x.Key} {x.Value}"))}";
            Console.WriteLine(statisticsStr);

            p_stat.StartPortfolioValue = (float)backtestResults.StartingPortfolioValue;
            p_stat.EndPortfolioValue = (float)backtestResults.DailyPortfolioValue;
            p_stat.TotalReturn = float.Parse(finalStat[PerformanceMetrics.NetProfit].Replace("%", string.Empty));
            p_stat.CAGR = float.Parse(finalStat[PerformanceMetrics.CompoundingAnnualReturn].Replace("%", string.Empty));
            p_stat.StDev = float.Parse(finalStat[PerformanceMetrics.AnnualStandardDeviation]);
            if (p_stat.SharpeRatio > 100f)
                p_stat.SharpeRatio = float.NaN; // if value is obviously wrong, indicate that with NaN
            p_stat.SharpeRatio = float.Parse(finalStat[PerformanceMetrics.SharpeRatio]);
            p_stat.MaxDD = float.Parse(finalStat[PerformanceMetrics.Drawdown].Replace("%", string.Empty));

            p_stat.NTrades = int.Parse(finalStat[PerformanceMetrics.TotalTrades]);

            // Ulcer - To be added, but these are not cardinal at the moment.
            // p_stat.Sortino = float.Parse(finalStat[PerformanceMetrics.SharpeRatio].Replace("%", string.Empty));
            // if (p_stat.Sortino > 100f)
            //     p_stat.Sortino = float.NaN; // if value is obviously wrong, indicate that with NaN

            // p_stat.WinRate = float.Parse(finalStat[PerformanceMetrics.WinRate].Replace("%", string.Empty));
            // p_stat.LossRate = float.Parse(finalStat[PerformanceMetrics.LossRate].Replace("%", string.Empty));
            // p_stat.Turnover = float.Parse(finalStat["Portfolio Turnover"]);
            // p_stat.LongShortRatio = float.Parse(finalStat["Long/Short Ratio"].Replace("%", string.Empty));
            p_stat.Fees = float.Parse(finalStat[PerformanceMetrics.TotalFees].Replace("$", string.Empty));
            // BenchmarkCAGR - To be added
            // BenchmarkMaxDrawDown - To be added
            // CorrelationWithBenchmark - To be added
        }

        // We need these in the Statistic: "Net Profit" => TotalReturn, "Compounding Annual Return" =>CAGR, "Drawdown" => MaxDD,  "Sharpe Ratio" =>Sharpe, "Win Rate" =>WinRate, "Annual Standard Deviation" =>StDev, "Sortino Ratio" => Sortino, "Portfolio Turnover" => Turnover, "Long/Short Ratio" =>LongShortRatio, "Total Fees" => Fees,

        var prtfPositions = backtestResults.Algorithm;
        foreach (var item in prtfPositions.UniverseManager.ActiveSecurities.Values)
        {
            if ((int)item.Holdings.Quantity == 0) // eliminating the positions with holding quantity equals to zero
                continue;
            PortfolioPosition posStckItem = new()
            {
                SqTicker = "S/" + item.Holdings.Symbol.ToString(),
                Quantity = (int)item.Holdings.Quantity,
                AvgPrice = (float)item.Holdings.AveragePrice,
                LastPrice = (float)item.Holdings.Price
            };
            p_prtfPoss.Add(posStckItem); // Stock Tickers
        }

        PortfolioPosition posCashItem = new(); // Cash Tickers
        foreach (var item in prtfPositions.Portfolio.CashBook.Values)
        {
            posCashItem.SqTicker = "C/" + item.Symbol.ToString();
            posCashItem.LastPrice = (float)item.Amount;
            p_prtfPoss.Add(posCashItem);
        }
        return null; // No Error
    }

    public static string? GetBmrksHistoricalResults(string p_bmrksStr, DateTime p_minDate, out PriceHistoryJs p_histPrices)
    {
        PriceHistoryJs historicalPrices = new();
        string tickerAsTradedToday2 = p_bmrksStr; // if symbol.zip doesn't exist in Data folder, it will not download it (cost money, you have to download in their shop). It raises an exception.
        Symbol symbol = new(SecurityIdentifier.GenerateEquity(tickerAsTradedToday2, Market.USA, true, FinDb.gFinDb.MapFileProvider), tickerAsTradedToday2);

        DateTime startTimeUtc = new(p_minDate.Year, p_minDate.Month, p_minDate.Day);
        // DateTime startTimeUtc = new(2008, 01, 01);
        // If you want to get 20080104 day data too, it has to be specified like this:
        // class TimeBasedFilter assures that (data.EndTime <= EndTimeLocal)
        // It is assumed that any TradeBar final values are only released at TradeBar.EndTime (OK for minute, hourly data, but not perfect for daily data which is known at 16:00)
        // Any TradeBar's EndTime is Time+1day (assuming that ClosePrice is released not at 16:00, but later, at midnight)
        // So the 20080104's row in CVS is: Time: 20080104:00:00, EndTime:20080105:00:00
        DateTime today = DateTime.UtcNow.Date;
        DateTime endTimeUtc = new(today.Year, today.Month, today.Day, 5, 0, 0); // this will be => 2008-01-05:00:00 endTimeLocal

        // Use TickType.TradeBar. That is in the daily CSV file. TickType.Quote file would contains Ask(Open/High/Low/Close) + Bid(Open/High/Low/Close), like a Quote from a Broker at trading realtime.
        var historyRequests = new[]
        {
            new HistoryRequest(startTimeUtc, endTimeUtc, typeof(TradeBar), symbol, Resolution.Daily, SecurityExchangeHours.AlwaysOpen(TimeZones.NewYork),
                TimeZones.NewYork, null, false, false, DataNormalizationMode.Adjusted, QuantConnect.TickType.Trade)
        };

        NodaTime.DateTimeZone sliceTimeZone = TimeZones.NewYork; // "algorithm.TimeZone"

        var result = FinDb.gFinDb.HistoryProvider.GetHistory(historyRequests, sliceTimeZone).ToList();
        Utils.Logger.Info("length of result bar values:" + result[0].Bars.Values.ToArray().Length);
        Console.WriteLine($" Test Historical price data. Number of TradeBars: {result.Count}. SPY RAW ClosePrice on {result[0].Bars.Values.ToArray()[0].Time}: {result[0].Bars.Values.ToArray()[0].Close}");

        for (int i = 0; i < result.Count; i++)
        {
            var resBarVals = result[i].Bars.Values.ToArray();
            string dateStr = resBarVals[0].Time.TohYYYYMMDD();
            float price = (float)resBarVals[0].Price;

            historicalPrices.Dates.Add(dateStr); // Add the date to the Date list
            historicalPrices.Prices.Add(price); // Add the price to the Price list
        }
        p_histPrices = historicalPrices;
        return null; // No Error
    }
}