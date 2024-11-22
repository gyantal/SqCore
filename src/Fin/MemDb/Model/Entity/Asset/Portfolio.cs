using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json.Serialization;
using Fin.Base;

namespace Fin.MemDb;

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
    public string AlgorithmParam { get; set; } = string.Empty;
    [JsonPropertyName("TradeHistory")]
    public int? TradeHistoryId { get; set; } = null; // 'int?' is used instead of 'int' with the -1 default, because if it is the default, we don't want to store it in the RedisDb JSON

    public string LegacyDbPortfName { get; set; } = string.Empty;

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
        AlgorithmParam = p_prtf.AlgorithmParam.ToString();
        TradeHistoryId = p_prtf.TradeHistoryId == -1 ? null : p_prtf.TradeHistoryId;
        LegacyDbPortfName = p_prtf is LegacyPortfolio legacyPf ? legacyPf.LegacyDbPortfName : string.Empty;
    }
}

[DebuggerDisplay("{Id}, Name:{Name}, User:{User?.Username??\"-NoUser-\"}")]
public partial class Portfolio : Asset // this inheritance makes it possible that a Portfolio can be part of an Uber-portfolio
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
    public string AlgorithmParam { get; set; } = string.Empty;

    public int TradeHistoryId { get; set; } = -1;

    // public List<Asset> Assets { get; set; } = new List<Asset>();    // TEMP. Delete this later when Portfolios are finalized.

    public Portfolio() { }

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
        AlgorithmParam = portfolioInDb.AlgorithmParam;
        TradeHistoryId = portfolioInDb.TradeHistoryId ?? -1;
    }

    public Portfolio(int p_id, User? p_user, string p_name, int p_parentFldId, string p_creationTime, CurrencyId p_currency, PortfolioType p_type, string p_algorithm, string p_algorithmParam, SharedAccess p_sharedAccess, string p_note, List<User> p_sharedUsersWith, int p_tradeHistoryId)
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
        AlgorithmParam = p_algorithmParam;
        SharedAccess = p_sharedAccess;
        SharedUsersWith = p_sharedUsersWith;
        TradeHistoryId = p_tradeHistoryId;
    }
}