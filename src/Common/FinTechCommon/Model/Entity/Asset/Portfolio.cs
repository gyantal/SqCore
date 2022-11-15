using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;

namespace FinTechCommon;

[DebuggerDisplay("{Id}, Name:{Name}, User:{User?.Username??\"-NoUser-\"}")]
public class Portfolio : Asset  // this inheritance makes it possible that a Portfolio can be part of an Uber-portfolio
{
    public int Id { get; set; } = -1;

    public User? User { get; set; } = null; // Some portfolios in SqExperiments, Backtest UserId = -1, so no user.

    public List<Asset> Assets { get; set; } = new List<Asset>();

    public Portfolio(JsonElement row, User[] users) : base(AssetType.Portfolio, row)
    {
        User = users.FirstOrDefault(r => r.Username == row[5].ToString()!);
    }
}