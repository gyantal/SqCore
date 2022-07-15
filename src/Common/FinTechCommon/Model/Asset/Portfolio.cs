using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace FinTechCommon;

public class Portfolio : Asset  // this inheritance makes it possible that a Portfolio can be part of an Uber-portfolio
{
    public User? User { get; set; } = null;

    public List<Asset> Assets { get; set; } = new List<Asset>();

    public Portfolio(JsonElement row, User[] users) : base(AssetType.Portfolio, row)
    {
        User = users.FirstOrDefault(r => r.Username == row[5].ToString()!);
    }
}