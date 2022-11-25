using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FinTechCommon;

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
}