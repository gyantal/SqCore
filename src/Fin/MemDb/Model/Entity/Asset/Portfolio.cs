using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using QuantConnect;

namespace Fin.MemDb;

// Temporary here. Will be refactored to another file.
public class BacktestResultsStatistics
{
    public float StartingPortfolioValue = 1000.0f;
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
    }

    public Portfolio(int p_id, User? p_user, string p_name, int p_parentFldId, string p_creationTime, CurrencyId p_currency, PortfolioType p_type, SharedAccess p_sharedAccess, string p_note, List<User> p_sharedUsersWith)
    {
        Id = p_id;
        User = p_user;
        Name = p_name;
        ParentFolderId = p_parentFldId;
        CreationTime = p_creationTime;
        Note = p_note;
        BaseCurrency = p_currency;
        Type = p_type;
        SharedAccess = p_sharedAccess;
        SharedUsersWith = p_sharedUsersWith;
    }

    public Portfolio()
    {
    }

    public string? GetBacktestResults(out BacktestResultsStatistics p_stat, out List<ChartPoint> p_pv)
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
            StartingPortfolioValue = 1000.0f,
            EndPortfolioValue = 1400.0f,
            SharpeRatio = 0.8f
        }; // output
        return null; // No Error
    }
}