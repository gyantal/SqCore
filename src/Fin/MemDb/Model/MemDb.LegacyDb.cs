using System.Collections.Generic;
using Fin.Base;

namespace Fin.MemDb;

public partial class MemDb
{
    public void TestLegacyDbConnection()
    {
        m_legacyDb.TestIsConnectionWork();
    }

    public List<Trade>? GetLegacyPortfolioTradeHistoryToList(string p_legacyDbPortfName)
    {
        return m_legacyDb.GetTradeHistory(p_legacyDbPortfName); // assume tradeHistory is ordered by Trade.Time
    }

    public bool InsertLegacyPortfolioTrade(string p_legacyDbPortfName, Trade p_newTrade)
    {
        return m_legacyDb.InsertTrade(p_legacyDbPortfName, p_newTrade); // assume tradeHistory is ordered by Trade.Time
    }
}