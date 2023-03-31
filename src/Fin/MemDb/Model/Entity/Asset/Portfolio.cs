using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using QuantConnect;
using QuantConnect.Lean.Engine.Results;

namespace Fin.MemDb;

// Temporary here. Will be refactored to another file.
public class BacktestResultsStatistics
{
    public float StartPortfolioValue = 1000.0f;
    public float EndPortfolioValue = 1400.0f;
    public float SharpeRatio = 0.8f;
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

    public PortfolioInDb(Portfolio prtfId)
    {
        UserId = prtfId.User?.Id ?? -1;
        Name = prtfId.Name;
        ParentFolderId = prtfId.ParentFolderId;
        SharedAccess = prtfId.SharedAccess.ToString();
        SharedUsersWith = string.Join(",", prtfId.SharedUsersWith);
        CreationTime = prtfId.CreationTime;
        Note = prtfId.Note;
        BaseCurrency = prtfId.BaseCurrency.ToString();
        Type = prtfId.Type.ToString();
        Algorithm = prtfId.Algorithm.ToString();
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

    public string? GetPortfolioRunResults(out BacktestResultsStatistics p_stat, out List<ChartPoint> p_pv)
    {
        #pragma warning disable IDE0066 // disable the switch suggestion warning only locally
        switch (Type)
        {
            case PortfolioType.Simulation:
                return GetBacktestResult(out p_stat, out p_pv);
            case PortfolioType.Trades:
            case PortfolioType.SqClassicTrades:
            default:
                return GetBacktestResultsDefault(out p_stat, out p_pv);
        }
    }

    public string? GetBacktestResultsDefault(out BacktestResultsStatistics p_stat, out List<ChartPoint> p_pv)
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
        p_stat = new BacktestResultsStatistics
        {
            StartPortfolioValue = 1000.0f,
            EndPortfolioValue = 1400.0f,
            SharpeRatio = 0.8f
        }; // output
        return null; // No Error
    }

    public string? GetBacktestResult(out BacktestResultsStatistics p_stat, out List<ChartPoint> p_pv)
    {
        p_stat = new BacktestResultsStatistics();
        p_pv = new List<ChartPoint>();

        Thread.Sleep(1 + Id);   // temporary here for simulation.

        string algorithm = String.IsNullOrEmpty(Algorithm) ? "BasicTemplateFrameworkAlgorithm" : Algorithm;
        BacktestingResultHandler backtestResults = Backtester.BacktestInSeparateThreadWithTimeout(algorithm, @"{""ema-fast"":10,""ema-slow"":20}");
        if (backtestResults == null)
            return "Error in Backtest";

        Console.WriteLine("BacktestResults.LogStore (from Algorithm)"); // we can force the Trade Logs into a text file. ("SaveListOfTrades(AlgorithmHandlers.Transactions, csvTransactionsFileName);"). But our Algo also can put it into the LogStore
        backtestResults.LogStore.ForEach(r => Console.WriteLine(r.Message)); // Trade Logs. "Time: 10/07/2013 13:31:00 OrderID: 1 EventID: 2 Symbol: SPY Status: Filled Quantity: 688 FillQuantity: 688 FillPrice: 144.7817 USD OrderFee: 3.44 USD"

        Console.WriteLine($"BacktestResults.PV. startPV:{backtestResults.StartingPortfolioValue:N0}, endPV:{backtestResults.DailyPortfolioValue:N0} ({(backtestResults.DailyPortfolioValue / backtestResults.StartingPortfolioValue - 1) * 100:N2}%)");

        var equityChart = backtestResults.Charts["Strategy Equity"].Series["Equity"].Values;
        Console.WriteLine($"#Charts:{backtestResults.Charts.Count}. The Equity (PV) chart: {equityChart[0].y:N0}, {equityChart[1].y:N0} ... {equityChart[^2].y:N0}, {equityChart[^1].y:N0}");

        Dictionary<string, string> finalStat = backtestResults.FinalStatistics;
        var statisticsStr = $"{Environment.NewLine}" + $"{string.Join(Environment.NewLine, finalStat.Select(x => $"STATISTICS:: {x.Key} {x.Value}"))}";
        Console.WriteLine(statisticsStr);

        p_stat.StartPortfolioValue = (float)backtestResults.StartingPortfolioValue;
        p_stat.EndPortfolioValue = (float)backtestResults.DailyPortfolioValue;
        if (!Single.TryParse(finalStat["Sharpe Ratio"], out p_stat.SharpeRatio))
            p_stat.SharpeRatio = 0.0f;

        // We need these in the Statistic: "Net Profit" => TotalReturn, "Compounding Annual Return" =>CAGR, {[Drawdown, 2.200%]} => MaxDD,  "Sharpe Ratio" =>Sharpe, "Win Rate" =>WinRate, "Annual Standard Deviation" =>StDev, "Sortino Ratio" => Sortino, "Portfolio Turnover" => Turnover, "Long/Short Ratio" =>LongShortRatio, "Total Fees" => Fees,

        return null; // No Error
    }
}