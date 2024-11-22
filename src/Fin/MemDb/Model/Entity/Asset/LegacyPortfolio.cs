using System;
using System.Collections.Generic;
using System.Diagnostics;
using Fin.Base;

namespace Fin.MemDb;

[DebuggerDisplay("{Id}, Name:{Name}, User:{User?.Username??\"-NoUser-\"}")]
public partial class LegacyPortfolio : Portfolio
{
    public string LegacyDbPortfName { get; set; } = string.Empty;

    public LegacyPortfolio() { }

    public LegacyPortfolio(int id, PortfolioInDb portfolioInDb, User[] users)
    : base(id, portfolioInDb, users)
    {
        if (!string.IsNullOrEmpty(portfolioInDb.LegacyDbPortfName))
            LegacyDbPortfName = portfolioInDb.LegacyDbPortfName;
    }

    public LegacyPortfolio(int p_id, User? p_user, string p_name, int p_parentFldId, string p_creationTime, CurrencyId p_currency, PortfolioType p_type, string p_algorithm, string p_algorithmParam, SharedAccess p_sharedAccess, string p_note, List<User> p_sharedUsersWith, int p_tradeHistoryId, string p_legacyDbPortfName)
    : base(p_id, p_user, p_name, p_parentFldId, p_creationTime, p_currency, p_type, p_algorithm, p_algorithmParam, p_sharedAccess, p_note, p_sharedUsersWith, p_tradeHistoryId)
    {
        LegacyDbPortfName = p_legacyDbPortfName;
    }

    public override List<Trade>? GetTradeHistory()
    {
        return MemDb.gMemDb.GetLegacyPortfolioTradeHistoryToList(LegacyDbPortfName);
    }
}