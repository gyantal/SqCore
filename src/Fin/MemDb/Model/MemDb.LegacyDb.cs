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
        return m_legacyDb.InsertTrade(p_legacyDbPortfName, p_newTrade);
    }

    public List<(string Ticker, int Id)> GetLegacyDbStockIds(List<string> p_tickers)
    {
        return m_legacyDb.GetStockIds(p_tickers);
    }

    public bool InsertLegacyPortfolioTrades(string p_legacyDbPortfName, List<Trade> p_newTrades) // if we are sure that the ticker in the trade is valid, we can save 1 SQL query of checking it.
    {
        return m_legacyDb.InsertTrades(p_legacyDbPortfName, p_newTrades);
    }
}