using System;
using System.Collections.Generic;
using Fin.Base;
using Microsoft.Data.SqlClient;
using SqCommon;

// SqlCommand ctor complains a Warning CA2100 about possible SQL injection attack. But we prefer fast execution, and we control the parameters properly ourself (our params don't come from User Input)
#pragma warning disable CA2100 // CA2100: Review if the query string passed to 'string SqlCommand.CommandText' accepts any user input (Warning to prevent SQL injection attack)

public class LegacyDb : IDisposable
{
    private SqlConnection? m_connection = null;
    private bool m_disposed = false;

    public void Init_WT() // Init Legacy SQL DB in a separate thread. The main MemDb.Init_WT() doesn't require its existence. We only need it for backtesting legacy portfolios much later.
    {
        string legacyDbConnString = Utils.Configuration["ConnectionStrings:LegacyMsSqlDefault"] ?? throw new SqException("Redis ConnectionStrings is missing from Config");
        m_connection = new SqlConnection(legacyDbConnString);
        m_connection.Open();
    }

    public void Exit()
    {
        Dispose();
        Console.WriteLine("Connection to SQL Server closed.");
    }

    public void TestIsConnectionWork()
    {
        if (m_connection?.State != System.Data.ConnectionState.Open)
        {
            Utils.Logger.Error("LegacyDb Error. Connection to SQL Server has not established successfully.");
            return;
        }

        // Create a command to execute a simple SELECT query
        string queryStr = "SELECT * FROM [dbo].[Stock] WHERE Ticker = 'TSLA'";
        SqlCommand command = new(queryStr, m_connection);

        try
        {
            // Execute the query and read the results
            using SqlDataReader reader = command.ExecuteReader();
            if (reader.HasRows)
            {
                while (reader.Read())
                {
                    int tslaId = reader.GetInt32(0); // ID is an integer and is in the first column
                    Console.WriteLine($"The TSLA ID is: {tslaId}");
                }
            }
            else
                Utils.Logger.Error("LegacyDb Error. No data found for TSLA.");
        }
        catch (Exception e)
        {
            Utils.Logger.Error($"LegacyDb Error. An error occurred while executing the query: {e.Message}");
        }
    }

    public List<Trade>? GetTradeHistory(string p_legacyDbPortfName)
    {
        if (m_connection?.State != System.Data.ConnectionState.Open)
        {
            Utils.Logger.Error("LegacyDb Error. Connection to SQL Server has not established successfully.");
            return null;
        }

        // Step 1: Get the portfolioid from FileSystemItem table
        int portfolioId = GetPortfolioId(p_legacyDbPortfName);
        if (portfolioId == -1)
        {
            Utils.Logger.Error($"LegacyDb Error. Could not find portfolio ID for portfolio '{p_legacyDbPortfName}'.");
            return null;
        }

        // Step 2: Query trades using the obtained portfolioId
        string queryStr = $"SELECT portfolioitem.*, COALESCE(stock.ticker, 'USD') AS ticker FROM portfolioitem LEFT JOIN stock ON portfolioitem.assetsubtableid = stock.id WHERE portfolioitem.portfolioid = {portfolioId} ORDER BY portfolioitem.Date";
        SqlCommand command = new(queryStr, m_connection);

        List<Trade> trades = new();
        try
        {
            using SqlDataReader reader = command.ExecuteReader();
            if (reader.HasRows)
            {
                while (reader.Read())
                {
                    Trade trade = new() // GetOrdinal() get the column index of the field identified by the name
                    {
                        Id = reader.GetInt32(reader.GetOrdinal("ID")),
                        Time = reader.GetDateTime(reader.GetOrdinal("Date")),
                        Action = MapLegacyDbTransactionTypeToTradeAction(reader.GetByte(reader.GetOrdinal("TransactionType"))),
                        Symbol = reader.GetString(reader.GetOrdinal("ticker")),
                        Quantity = reader.GetInt32(reader.GetOrdinal("Volume")),
                        Price = reader.GetFloat(reader.GetOrdinal("Price")),
                        Note = reader.IsDBNull(reader.GetOrdinal("Note")) ? null : reader.GetString(reader.GetOrdinal("Note"))
                    };
                    trades.Add(trade);
                }
            }
            else
                Utils.Logger.Error($"LegacyDb Error. No trade data found for portfolio '{p_legacyDbPortfName}.");
        }
        catch (Exception ex)
        {
            Utils.Logger.Error($"LegacyDb Error. An error occurred while executing the query: {ex.Message}");
            return null;
        }
        return trades;
    }

    int GetPortfolioId(string p_legacyDbPortfName)
    {
        string queryStr = $"SELECT Id FROM FileSystemItem WHERE Name = '{p_legacyDbPortfName}'";
        SqlCommand command = new(queryStr, m_connection);

        try
        {
            using SqlDataReader reader = command.ExecuteReader();
            if (reader.HasRows && reader.Read())
                return reader.GetInt32(reader.GetOrdinal("Id"));
        }
        catch (Exception ex)
        {
            Utils.Logger.Error($"LegacyDb Error. An error occurred while executing the query to get portfolio ID: {ex.Message}");
        }

        return -1;
    }

    static TradeAction MapLegacyDbTransactionTypeToTradeAction(byte p_legacyDbtransactionType) // conversion from HedgeQuant:: enum PortfolioItemTransactionType
    {
        return p_legacyDbtransactionType switch
        {
            0 => TradeAction.Unknown,
            1 => TradeAction.Deposit,
            2 => TradeAction.Withdrawal,
            // 3 => TransactionCost, we convert it to _ => TradeAction.Unknown
            4 => TradeAction.Buy,
            5 => TradeAction.Sell,
            6 => TradeAction.Sell, // ShortAsset
            7 => TradeAction.Buy, // CoverAsset
            8 => TradeAction.Sell, // WriteOption
            9 => TradeAction.Buy, // BuybackWrittenOption
            10 => TradeAction.Exercise, // ExerciseOption
            _ => TradeAction.Unknown
        };
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!m_disposed)
        {
            if (disposing)
            {
                // Dispose managed resources
                if (m_connection != null)
                {
                    if (m_connection.State == System.Data.ConnectionState.Open)
                    {
                        m_connection.Close();
                    }
                    m_connection.Dispose();
                }
            }
            // Dispose unmanaged resources if any
            m_disposed = true;
        }
    }

    ~LegacyDb()
    {
        Dispose(false);
    }
}