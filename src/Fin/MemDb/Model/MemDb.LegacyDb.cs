using System;
using System.Collections.Generic;
using Fin.Base;

namespace Fin.MemDb;

public partial class MemDb
{
    public void TestLegacyDbConnection()
    {
        m_legacyDb.TestIsConnectionWork();
    }

    public List<Trade>? GetLegacyPortfolioTradeHistoryToList(string p_legacyDbPortfName, int p_numTop = Int32.MaxValue)
    {
        return m_legacyDb.GetTradeHistory(p_legacyDbPortfName, p_numTop); // assume tradeHistory is ordered by Trade.Time
    }

    public List<(string Ticker, int Id)> GetLegacyStockIds(List<string> p_tickers)
    {
        return m_legacyDb.GetStockIds(p_tickers);
    }

    public bool InsertLegacyPortfolioTrade(string p_legacyDbPortfName, Trade p_newTrade)
    {
        return m_legacyDb.InsertTrade(p_legacyDbPortfName, p_newTrade);
    }

    public string? InsertLegacyPortfolioTrades(string p_legacyDbPortfName, List<Trade> p_newTrades) // Insert trades with StockID check with only 2x SQL Queries. Returns errorStr or null if success.
    {
        return m_legacyDb.InsertTrades(p_legacyDbPortfName, p_newTrades);
    }
}