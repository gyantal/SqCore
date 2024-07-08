using System;
using System.Collections.Generic;
using System.Diagnostics;
using Fin.Base;

namespace Fin.MemDb;

[DebuggerDisplay("{Id}, Name:{Name}, User:{User?.Username??\"-NoUser-\"}")]
public partial class LegacyPortfolio : Portfolio
{
    public string LegacyDbPortfName { get; set; } = string.Empty;

    public LegacyPortfolio(int id, PortfolioInDb portfolioInDb, User[] users)
    : base(id, portfolioInDb, users)
    {
        if (!string.IsNullOrEmpty(portfolioInDb.LegacyDbPortfName))
            LegacyDbPortfName = portfolioInDb.LegacyDbPortfName;
    }

    public override List<Trade>? GetTradeHistory()
    {
        return MemDb.gMemDb.GetLegacyPortfolioTradeHistoryToList(LegacyDbPortfName);
    }
}