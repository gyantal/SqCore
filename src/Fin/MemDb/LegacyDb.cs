using System;
using System.Collections.Generic;
using Fin.Base;
using Microsoft.Data.SqlClient;
using SqCommon;

// SqlCommand ctor complains a Warning CA2100 about possible SQL injection attack. But we prefer fast execution, and we control the parameters properly ourself (our params don't come from User Input)
#pragma warning disable CA2100 // CA2100: Review if the query string passed to 'string SqlCommand.CommandText' accepts any user input (Warning to prevent SQL injection attack)

public class LegacyDb : IDisposable
{
    Dictionary<string, string> c_legacyDb2QcTickerMap = new() { { "VXX-20190130", "VXX" }, { "ZIVZF", "ZIV" }, { "UGAZF", "UGAZ" } };
    private SqlConnection? m_connection = null;
    private bool m_disposed = false;

    public void Init_WT() // Init Legacy SQL DB in a separate thread. The main MemDb.Init_WT() doesn't require its existence. We only need it for backtesting legacy portfolios much later.
    {
        // TODO: 2024-07-02: there is no Fix yet. Hoping Microsoft.Data.SqlClient 5.2.2 fix Linux deployment problem in 1-2 months
        // TODO: clean this section After it is fixed.
        // TODO: at the moment, SQL m_connection only works on Windows, not on Linux.
        // >#22|ERROR|Sq: LegacyDb Error. Init_WT() exception: Could not load file or assembly 'Microsoft.Data.SqlClient, Version=5.0.0.0, Culture=neutral, PublicKeyToken=23ec7fc2d6eaa4a5'. The system cannot find the file specified.
        // https://github.com/dotnet/SqlClient/issues/1945
        // " 2 weeks ago" "I'm using .net 8 and the 5.2.1 package and the problem persists:"
        // https://github.com/dotnet/SqlClient/pull/2093 Fix was merged on "Jul 21, 2023" SqlClient: 5.2.0-preview4
        // https://github.com/dotnet/SqlClient/issues/2146
        // "3 weeks ago: I have the same issue. .NET Core 6 with EFCore 7, deployed on a linux docker image."
        // >https://www.nuget.org/packages/Microsoft.Data.SqlClient/5.2.1
        // we use <PackageReference Include="Microsoft.Data.SqlClient" Version="5.2.1" />
        // "[Stable release 5.2.1] - 2024-05-31"
        // >Hope next version in 1-2 months will fix this.
        // ><Didn't work> After doing 'dotnet purge' to eliminate bin, obj folders.
        // ><Didn't work> after changing In
        // c:\agy\GitHub\SqCore\src\WebServer\SqCoreWeb\bin\Release\net8.0\publish\SqCoreWeb.deps.json
        // Change Microsoft.Data.SqlClient.dll "assemblyVersion" to FileVersion.
        // >But that didn't work.
        Utils.Logger.Info("LegacyDb.Init_WT() START");
        try
        {
            Init_WT_With_Microsoft_Data_SqlClient_dll_Dependency();
        }
        catch (System.Exception e)
        {
            Utils.Logger.Error($"LegacyDb Error. Init_WT() exception: {e.Message}");
        }
    }

    public void Init_WT_With_Microsoft_Data_SqlClient_dll_Dependency() // Init Legacy SQL DB in a separate thread. The main MemDb.Init_WT() doesn't require its existence. We only need it for backtesting legacy portfolios much later.
    {
        Utils.Logger.Info("LegacyDb.Init_WT_With_Microsoft_Data_SqlClient_dll_Dependency() START");
        try
        {
            string legacyDbConnString = Utils.Configuration["ConnectionStrings:LegacyMsSqlDefault"] ?? throw new SqException("Redis ConnectionStrings is missing from Config");
            Utils.Logger.Info($"LegacyDb.Init_WT(). ConnStr:{legacyDbConnString}");
            m_connection = new SqlConnection(legacyDbConnString);
            m_connection.Open();
        }
        catch (System.Exception e)
        {
            Utils.Logger.Error($"LegacyDb Error. Init_WT() exception: {e.Message}");
            throw;
        }
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
                    string? xmlNote = reader.IsDBNull(reader.GetOrdinal("Note")) ? null : reader.GetString(reader.GetOrdinal("Note"));
                    string? note = null;
                    if (!string.IsNullOrEmpty(xmlNote))
                    {
                        // Extract the value between 'Text="' and the next '"'
                        int startIndex = xmlNote.IndexOf("Text=\"") + "Text=\"".Length;
                        int endIndex = xmlNote.IndexOf("\"", startIndex);

                        if (startIndex >= "Text=\"".Length && endIndex > startIndex)
                            note = xmlNote.Substring(startIndex, endIndex - startIndex);
                    }

                    string ticker = reader.GetString(reader.GetOrdinal("ticker"));
                    if (c_legacyDb2QcTickerMap.TryGetValue(ticker, out string? newTicker))
                        ticker = newTicker;
                    Trade trade = new() // GetOrdinal() get the column index of the field identified by the name
                    {
                        Id = reader.GetInt32(reader.GetOrdinal("ID")),
                        Time = reader.GetDateTime(reader.GetOrdinal("Date")),
                        Action = MapLegacyDbTransactionTypeToTradeAction(reader.GetByte(reader.GetOrdinal("TransactionType"))),
                        Symbol = ticker,
                        Quantity = reader.GetInt32(reader.GetOrdinal("Volume")),
                        Price = reader.GetFloat(reader.GetOrdinal("Price")),
                        Note = note
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

    public bool InsertTrade(string p_legacyDbPortfName, Trade p_newTrade)
    {
        if (m_connection?.State != System.Data.ConnectionState.Open)
        {
            Utils.Logger.Error("LegacyDb Error. Connection to SQL Server has not established successfully.");
            return false;
        }

        // Step 1: Get the portfolio ID from FileSystemItem table
        int portfolioId = GetPortfolioId(p_legacyDbPortfName);
        if (portfolioId == -1)
        {
            Utils.Logger.Error($"LegacyDb Error. Could not find portfolio ID for portfolio '{p_legacyDbPortfName}'.");
            return false;
        }

        // Step 2: Insert the new trade into portfolioitem table
        string queryStr = @"
        INSERT INTO portfolioitem (PortfolioID, TransactionType, AssetTypeID, AssetSubTableID, Volume, Price, Date, Note)
        VALUES (@PortfolioID, @TransactionType, @AssetTypeID, @AssetSubTableID, @Volume, @Price, @Date, @Note)";

        SqlCommand command = new(queryStr, m_connection);
        command.Parameters.AddWithValue("@PortfolioID", portfolioId);
        command.Parameters.AddWithValue("@TransactionType", MapTradeActionToLegacyDbTransactionType(p_newTrade.Action));
        command.Parameters.AddWithValue("@AssetTypeID", 2);
        command.Parameters.AddWithValue("@AssetSubTableID", GetStockId(p_newTrade.Symbol!));
        command.Parameters.AddWithValue("@Volume", p_newTrade.Quantity);
        command.Parameters.AddWithValue("@Price", p_newTrade.Price);
        command.Parameters.AddWithValue("@Date", p_newTrade.Time);
        command.Parameters.AddWithValue("@Note", string.IsNullOrEmpty(p_newTrade.Note) ? DBNull.Value : $@"<Note><UserNote Text=""{p_newTrade.Note}"" /></Note>");

        try
        {
            int rowsAffected = command.ExecuteNonQuery();
            if (rowsAffected > 0)
            {
                Utils.Logger.Info($"Trade successfully inserted for portfolio '{p_legacyDbPortfName}'.");
                return true;
            }
            else
            {
                Utils.Logger.Error($"LegacyDb Error. Failed to insert trade for portfolio '{p_legacyDbPortfName}'.");
                return false;
            }
        }
        catch (Exception ex)
        {
            Utils.Logger.Error($"LegacyDb Error. An error occurred while inserting the trade: {ex.Message}");
            return false;
        }
    }

    int GetStockId(string p_ticker)
    {
        string queryStr = $"SELECT Id FROM Stock WHERE Ticker = @Ticker";
        SqlCommand command = new(queryStr, m_connection);
        command.Parameters.AddWithValue("@Ticker", p_ticker);

        try
        {
            using SqlDataReader reader = command.ExecuteReader();
            if (reader.HasRows && reader.Read())
                return reader.GetInt32(reader.GetOrdinal("Id"));
        }
        catch (Exception ex)
        {
            Utils.Logger.Error($"LegacyDb Error. An error occurred while fetching stock ID: {ex.Message}");
        }

        return -1;
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

    static byte MapTradeActionToLegacyDbTransactionType(TradeAction p_tradeAction)
    {
        return p_tradeAction switch
        {
            TradeAction.Deposit => 1,
            TradeAction.Withdrawal => 2,
            TradeAction.Buy => 4,
            TradeAction.Sell => 5,
            TradeAction.Exercise => 10,
            _ => 0 // Unknown or unhandled cases
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