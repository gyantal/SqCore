using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using QuantConnect;
using QuantConnect.Lean.Engine.Results;
using SqCommon;

namespace Fin.MemDb;

// Temporary here. Will be refactored to another file.
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

public class PortfolioPosition
{
    public string SqTicker { get; set; } = string.Empty;
    public int Quantity { get; set; } = -1;
    public float AvgPrice { get; set; } = 0.0f;
    public float LastPrice { get; set; } = 0.0f;  // the last price of the asset at the end of the backtest (not real-time price)
}

public class PortfolioInDb // Portfolio.Id is not in the JSON, which is the HashEntry.Value. It comes separately from the HashEntry.Key
{
    [JsonPropertyName("User")]
    public int UserId { get; set; } = -1;   // Some folders: SqExperiments, Backtest has UserId = -1, indicating there is no proper user
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("ParentFolder")]
    public int ParentFolderId { get; set; } = -1;
    public string SharedAccess { get; set; } = string.Empty;
    public string SharedUsersWith { get; set; } = string.Empty;
    [JsonPropertyName("CTime")]
    public string CreationTime { get; set; } = string.Empty;
    public string Note { get; set; } = string.Empty;
    public string BaseCurrency { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Algorithm { get; set; } = string.Empty;

    public PortfolioInDb()
    {
    }

    public PortfolioInDb(Portfolio p_prtf)
    {
        UserId = p_prtf.User?.Id ?? -1;
        Name = p_prtf.Name;
        ParentFolderId = p_prtf.ParentFolderId;
        SharedAccess = p_prtf.SharedAccess.ToString();
        SharedUsersWith = string.Join(",", p_prtf.SharedUsersWith);
        CreationTime = p_prtf.CreationTime;
        Note = p_prtf.Note;
        BaseCurrency = p_prtf.BaseCurrency.ToString();
        Type = p_prtf.Type.ToString();
        Algorithm = p_prtf.Algorithm.ToString();
    }
}

[DebuggerDisplay("{Id}, Name:{Name}, User:{User?.Username??\"-NoUser-\"}")]
public class Portfolio : Asset // this inheritance makes it possible that a Portfolio can be part of an Uber-portfolio
{
    public int Id { get; set; } = -1;
    public User? User { get; set; } = null; // Some portfolios in SqExperiments, Backtest UserId = -1, so no user.

    public int ParentFolderId { get; set; } = -1;

    public SharedAccess SharedAccess { get; set; } = SharedAccess.Unknown;
    public List<User> SharedUsersWith { get; set; } = new();    // List is better than Array, because the user can add new users into it realtime
    public string CreationTime { get; set; } = string.Empty;
    public string Note { get; set; } = string.Empty;
    public CurrencyId BaseCurrency { get; set; } = CurrencyId.USD;
    public PortfolioType Type { get; set; } = PortfolioType.Unknown;
    public string Algorithm { get; set; } = string.Empty;

    // public List<Asset> Assets { get; set; } = new List<Asset>();    // TEMP. Delete this later when Portfolios are finalized.

    public Portfolio(int id, PortfolioInDb portfolioInDb, User[] users)
    {
        Id = id;
        User = users.FirstOrDefault(r => r.Id == portfolioInDb.UserId);
        Name = portfolioInDb.Name;
        ParentFolderId = portfolioInDb.ParentFolderId;

        SharedAccess = AssetHelper.gStrToSharedAccess[portfolioInDb.SharedAccess];
        if (!String.IsNullOrEmpty(portfolioInDb.SharedUsersWith))
        {
            string[] userIds = portfolioInDb.SharedUsersWith.Split(',', StringSplitOptions.RemoveEmptyEntries);
            foreach (var userIdStr in userIds)
            {
                if (!Int32.TryParse(userIdStr, out int userId))
                    continue;

                User? user = Array.Find(users, r => r.Id == userId);
                if (user == null)
                    continue;
                SharedUsersWith.Add(user);
            }
        }

        CreationTime = portfolioInDb.CreationTime;
        Note = portfolioInDb.Note;

        string baseCurrencyStr = portfolioInDb.BaseCurrency;
        if (String.IsNullOrEmpty(baseCurrencyStr))
            baseCurrencyStr = "USD";
        BaseCurrency = AssetHelper.gStrToCurrency[baseCurrencyStr]; // BaseCurrency is a Portfolio property, the original intention of the user at Portfolio Creation.
        Currency = BaseCurrency;                                    // Currency is the base class Asset property. The runtime property. At runtime a user might decide to accumulate portfolio in USD terms, although BaseCurrency was GBP.

        Type = AssetHelper.gStrToPortfolioType[portfolioInDb.Type];
        Algorithm = portfolioInDb.Algorithm;
    }

    public Portfolio(int p_id, User? p_user, string p_name, int p_parentFldId, string p_creationTime, CurrencyId p_currency, PortfolioType p_type, string p_algorithm, SharedAccess p_sharedAccess, string p_note, List<User> p_sharedUsersWith)
    {
        Id = p_id;
        User = p_user;
        Name = p_name;
        ParentFolderId = p_parentFldId;
        CreationTime = p_creationTime;
        Note = p_note;
        BaseCurrency = p_currency;
        Type = p_type;
        Algorithm = p_algorithm;
        SharedAccess = p_sharedAccess;
        SharedUsersWith = p_sharedUsersWith;
    }

    public Portfolio()
    {
    }

    // PortfolioValue chart data.
    // We have the option to return Date fields in different formats in JSON string:
    // '2021-01-27' is 10 chars, '20210127' is 8 chars. Resolution is only daily.
    // Or number of seconds from Unix epoch: '1641013200' is 10 chars. Resolution can be 1 second.
    // Although it is 2 chars more data, but we chose this, because QC uses it and also it will allow us to go intraday in the future.
    // Also it allows to show the user how up-to-date (real-time) the today value is.
    public string? GetPortfolioRunResult(out PortfolioRunResultStatistics p_stat, out List<ChartPoint> p_pv, out List<PortfolioPosition> p_prtfPoss)
    {
        #pragma warning disable IDE0066 // disable the switch suggestion warning only locally
        switch (Type)
        {
            case PortfolioType.Simulation:
                return GetBacktestResult(out p_stat, out p_pv, out p_prtfPoss);
            case PortfolioType.Trades:
            case PortfolioType.TradesSqClassic:
            default:
                return GetPortfolioRunResultDefault(out p_stat, out p_pv, out p_prtfPoss);
        }
        #pragma warning restore IDE0066
    }

    public string? GetPortfolioRunResultDefault(out PortfolioRunResultStatistics p_stat, out List<ChartPoint> p_pv, out List<PortfolioPosition> p_prtfPoss)
    {
        Thread.Sleep(500 + Id);
        // we will run the backtest.
        // List<ChartPoint> pvs = new List<ChartPoint>(); // Date + value pairs.
        // create a fake PVs.
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
        return null; // No Error
    }

    public string? GetBacktestResult(out PortfolioRunResultStatistics p_stat, out List<ChartPoint> p_pv, out List<PortfolioPosition> p_prtfPoss)
    {
        const int gDateTimeOffset = 300; // used for calculating the isDailChart Data or perminute data
        p_stat = new PortfolioRunResultStatistics();
        p_pv = new List<ChartPoint>();
        p_prtfPoss = new List<PortfolioPosition>();

        Thread.Sleep(1 + Id);   // temporary here for simulation.

        string algorithmName = String.IsNullOrEmpty(Algorithm) ? "BasicTemplateFrameworkAlgorithm" : Algorithm;
        BacktestingResultHandler backtestResults = Backtester.BacktestInSeparateThreadWithTimeout(algorithmName, @"{""ema-fast"":10,""ema-slow"":20}");
        if (backtestResults == null)
            return "Error in Backtest";

        Console.WriteLine("BacktestResults.LogStore (from Algorithm)"); // we can force the Trade Logs into a text file. ("SaveListOfTrades(AlgorithmHandlers.Transactions, csvTransactionsFileName);"). But our Algo also can put it into the LogStore
        backtestResults.LogStore.ForEach(r => Console.WriteLine(r.Message)); // Trade Logs. "Time: 10/07/2013 13:31:00 OrderID: 1 EventID: 2 Symbol: SPY Status: Filled Quantity: 688 FillQuantity: 688 FillPrice: 144.7817 USD OrderFee: 3.44 USD"

        Console.WriteLine($"BacktestResults.PV. startPV:{backtestResults.StartingPortfolioValue:N0}, endPV:{backtestResults.DailyPortfolioValue:N0} ({(backtestResults.DailyPortfolioValue / backtestResults.StartingPortfolioValue - 1) * 100:N2}%)");

        List<ChartPoint> equityChart = backtestResults.Charts["Strategy Equity"].Series["Equity"].Values;
        Console.WriteLine($"#Charts:{backtestResults.Charts.Count}. The Equity (PV) chart: {equityChart[0].y:N0}, {equityChart[1].y:N0} ... {equityChart[^2].y:N0}, {equityChart[^1].y:N0}");

        bool isDailyChartData = true;
        if (equityChart.Count > 2)
        {
            isDailyChartData = (equityChart[2].x - equityChart[1].x) > gDateTimeOffset;
        }
        if (isDailyChartData)
        {
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

        Dictionary<string, string> finalStat = backtestResults.FinalStatistics;
        var statisticsStr = $"{Environment.NewLine}" + $"{string.Join(Environment.NewLine, finalStat.Select(x => $"STATISTICS:: {x.Key} {x.Value}"))}";
        Console.WriteLine(statisticsStr);

        p_stat.StartPortfolioValue = (float)backtestResults.StartingPortfolioValue;
        p_stat.EndPortfolioValue = (float)backtestResults.DailyPortfolioValue;
        p_stat.TotalReturn = float.Parse(finalStat["Net Profit"].Replace("%", string.Empty));
        p_stat.CAGR = float.Parse(finalStat["Compounding Annual Return"].Replace("%", string.Empty));
        p_stat.MaxDD = float.Parse(finalStat["Drawdown"].Replace("%", string.Empty));
        p_stat.SharpeRatio = float.Parse(finalStat["Sharpe Ratio"]);
        if (p_stat.SharpeRatio > 100f)
            p_stat.SharpeRatio = float.NaN; // if value is obviously wrong, indicate that with NaN
        p_stat.StDev = float.Parse(finalStat["Annual Standard Deviation"]);
        // Ulcer - To be added
        // p_stat.TradingDays = int.Parse(finalStat["Trading Days"]);
        p_stat.NTrades = int.Parse(finalStat["Total Trades"]);
        p_stat.WinRate = float.Parse(finalStat["Win Rate"].Replace("%", string.Empty));
        p_stat.LossRate = float.Parse(finalStat["Loss Rate"].Replace("%", string.Empty));
        p_stat.Sortino = float.Parse(finalStat["Sortino Ratio"].Replace("%", string.Empty));
        if (p_stat.Sortino > 100f)
            p_stat.Sortino = float.NaN; // if value is obviously wrong, indicate that with NaN
        p_stat.Turnover = float.Parse(finalStat["Portfolio Turnover"]);
        p_stat.LongShortRatio = float.Parse(finalStat["Long/Short Ratio"].Replace("%", string.Empty));
        p_stat.Fees = float.Parse(finalStat["Total Fees"].Replace("$", string.Empty));
        // BenchmarkCAGR - To be added
        // BenchmarkMaxDrawDown - To be added
        // CorrelationWithBenchmark - To be added

        // We need these in the Statistic: "Net Profit" => TotalReturn, "Compounding Annual Return" =>CAGR, "Drawdown" => MaxDD,  "Sharpe Ratio" =>Sharpe, "Win Rate" =>WinRate, "Annual Standard Deviation" =>StDev, "Sortino Ratio" => Sortino, "Portfolio Turnover" => Turnover, "Long/Short Ratio" =>LongShortRatio, "Total Fees" => Fees,

        // To be worked upon - Daya
        var prtfPositions = backtestResults.Algorithm;
        foreach (var item in prtfPositions.UniverseManager.ActiveSecurities.Values)
        {
            PortfolioPosition posStckItem = new()
            {
                SqTicker = "S/" + item.Holdings.Symbol.ToString(),
                Quantity = (int)item.Holdings.Quantity,
                AvgPrice = (float)item.Holdings.AveragePrice,
                LastPrice = (float)item.Holdings.Price
            }; // Stock Tickers
            p_prtfPoss.Add(posStckItem);
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
}