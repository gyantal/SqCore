using System;
using System.Collections.Generic;
using System.Text;
using Fin.Base;
using Microsoft.Data.SqlClient;
using SqCommon;

// SqlCommand ctor complains a Warning CA2100 about possible SQL injection attack. But we prefer fast execution, and we control the parameters properly ourself (our params don't come from User Input)
#pragma warning disable CA2100 // CA2100: Review if the query string passed to 'string SqlCommand.CommandText' accepts any user input (Warning to prevent SQL injection attack)

public class LegacyDb : IDisposable
{
    Dictionary<string, string> c_legacyDb2QcTickerMap = new() { { "VXX-20190130", "VXX" }, { "VXZ-20190130", "VXZ" }, { "ZIVZF", "ZIV" }, { "UGAZF", "UGAZ" } };
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

    public List<Trade>? GetTradeHistory(string p_legacyDbPortfName, int p_numTop)
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
        string queryStr = $"SELECT TOP {p_numTop} portfolioitem.*, COALESCE(stock.ticker, 'USD') AS ticker FROM portfolioitem LEFT JOIN stock ON portfolioitem.assetsubtableid = stock.id WHERE portfolioitem.portfolioid = {portfolioId} ORDER BY portfolioitem.Date";
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
                        AssetType = MapLegacyDbAssetTypeToAssetType(reader.GetByte(reader.GetOrdinal("AssetTypeID"))),
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
        command.Parameters.AddWithValue("@AssetTypeID", MapAssetTypeToLegacyDbAssetType(p_newTrade.AssetType));
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

    public string? InsertTrades(string p_legacyDbPortfName, List<Trade> p_newTrades) // Insert trades with StockID check with only 2x SQL Queries. Returns errorStr or null if success.
    {
        // Step1: Check the connection state
        if (m_connection?.State != System.Data.ConnectionState.Open)
            return "LegacyDb Error. Connection to SQL Server has not established successfully";

        foreach (Trade trade in p_newTrades)
        {
            if (trade.AssetType != AssetType.Stock)
                return "LegacyDb Error. Only AssetType.Stock is supported.";
        }

        // Step2: Check the if the PortfolioId exists
        int portfolioId = GetPortfolioId(p_legacyDbPortfName);
        if (portfolioId == -1)
            return $"LegacyDb Error. Could not find portfolio ID for portfolio '{p_legacyDbPortfName}'.";
        // Step3: Get the uniqueTickers from p_newTrades (since p_newTrades may contain duplicates)
        List<string> uniqueTickers = new List<string>();
        foreach (Trade trade in p_newTrades)
        {
            if (!uniqueTickers.Contains(trade.Symbol!))
                uniqueTickers.Add(trade.Symbol!);
        }
        // Step4: Get the stockIds for the tickers
        List<(string Ticker, int Id)> stockIdsResult = GetStockIds(uniqueTickers);
        List<string> missingTickers = new();
        foreach ((string Ticker, int Id) stock in stockIdsResult) // Check if any ticker from trades doesn't exist in the stock data
        {
            if (stock.Id == -1) // Check if the stock ID is -1, indicating the symbol does not exist in LegacyDb
                missingTickers.Add(stock.Ticker);
        }

        if (missingTickers.Count > 0)
            return $"LegacyDb Error. TestInsertTrade failed, missing SQL Tickers: '{string.Join(",", missingTickers)}'";

        // Step5: Build the QueryBuilder for inserting the trades
        StringBuilder queryBuilder = new();
        queryBuilder.Append("INSERT INTO PortfolioItem (PortfolioID, TransactionType, AssetTypeID, AssetSubTableID, Volume, Price, Date, Note) VALUES ");
        for (int i = 0; i < p_newTrades.Count; i++)
        {
            Trade trade = p_newTrades[i];
            queryBuilder.Append($"({portfolioId}, {MapTradeActionToLegacyDbTransactionType(trade.Action)}, {MapAssetTypeToLegacyDbAssetType(trade.AssetType)}, ");
            for(int j = 0; j < stockIdsResult.Count; j++) // Extract the stockId from existing stockIdsResult
            {
                if (trade.Symbol == stockIdsResult[j].Ticker)
                {
                    queryBuilder.Append($"{stockIdsResult[j].Id}, ");
                    break;
                }
            }
            queryBuilder.Append($"{trade.Quantity}, {trade.Price}, '{trade.Time:yyyy-MM-dd HH:mm:ss}', ");
            queryBuilder.Append(string.IsNullOrEmpty(trade.Note) ? "NULL" : $"'<Note><UserNote Text=\"{trade.Note}\" /></Note>'");

            if (i < p_newTrades.Count - 1)
                queryBuilder.Append("), "); // If it's not the last ticker, add a closing parenthesis and a comma separator
            else
                queryBuilder.Append(")"); // If it's the last ticker, add a closing parenthesis without a comma
        }

        try
        {
            string queryStr = queryBuilder.ToString();
            using SqlCommand command = new(queryStr, m_connection);
            // Luckily, we don't have to do defensive coding because:
            // Firstly, we send checked PortfolioID and StockID data, so insertion cannot fail on those.
            // Secondly, according to our SQL Management Studio checks, if the batch insert fails, none of the trades are inserted.
            // E.g., "(357386, 5, 2, 9728, 1000, 36.84, '2024-11-15BBBBBB21:00:00', NULL)" fails due to a syntax check.
            // E.g., "(357386, 5, 999999, 9728, 1000, 36.84, '2024-11-15 21:00:00', NULL)" fails with 'The INSERT statement conflicted with the FOREIGN KEY constraint "FK_PortfolioItem_AssetType". The statement has been terminated.'
            // If an error occurs in any trade, the whole batch insert is terminated. The logical reason is that the SQL server itself performs a batch insertion into its file system, so it checks all data correctness beforehand.
            int rowsAffected = command.ExecuteNonQuery();
            int insertedRows = rowsAffected - 1; // One(1) row is for the index table in the SQL database, so subtract 1 from the total rowsAffected.
            if (insertedRows != p_newTrades.Count)
                return $"LegacyDb Error. Failed to insert trades for portfolio '{p_legacyDbPortfName}'.";
        }
        catch (Exception ex)
        {
            return $"LegacyDb Error. An error occurred while inserting the trades: {ex.Message}";
        }
        return null; // ErrorStr: null indicates successful insertion.
    }

    public int GetStockId(string p_ticker, bool p_isAlive = true)
    {
        string queryStr = $"SELECT Id FROM Stock WHERE IsAlive = {(p_isAlive ? 1 : 0)} AND  Ticker = @Ticker";
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

    public List<(string Ticker, int Id)> GetStockIds(List<string> p_tickers, bool p_isAlive = true)
    {
        List<(string Ticker, int Id)> sqlStockIdsResult = new List<(string Ticker, int Id)>();
        StringBuilder sbTickers = new(); // Build a comma-separated string of tickers to use in the SQL query
        for (int i = 0; i < p_tickers.Count; i++)
        {
            sbTickers.Append($"'{p_tickers[i]}'"); // Append each ticker in single quotes, for use in the SQL IN clause
            if (i < p_tickers.Count - 1) // Add a comma separator between tickers, except after the last ticker
                sbTickers.Append(", ");
        }
        string queryStr = $"SELECT Ticker, Id FROM Stock WHERE IsAlive = {(p_isAlive ? 1 : 0)} AND Ticker IN ({sbTickers})"; // Construct the SQL query to retrieve the ID for each specified ticker
        SqlCommand command = new(queryStr, m_connection); // Initialize the SQL command with the query and the open connection
        using SqlDataReader reader = command.ExecuteReader(); // Execute the query and retrieve the results using a data reader
        if (reader.HasRows)
        {
            while (reader.Read())
            {
                string ticker = reader.GetString(reader.GetOrdinal("ticker"));
                int id = reader.GetInt32(reader.GetOrdinal("Id"));
                sqlStockIdsResult.Add((ticker, id)); // Add the ticker and ID pair to the results list
            }
        }
        else
            Console.WriteLine("No rows found.");

        foreach (string ticker in p_tickers) // Post-process: Add missing tickers to the result list with an ID of -1
        {
            if (!sqlStockIdsResult.Exists(item => item.Ticker == ticker)) // Check if the ticker exists in the results; if not, add it with a default ID of -1
                sqlStockIdsResult.Add((ticker, -1));
        }
        return sqlStockIdsResult;
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

    static AssetType MapLegacyDbAssetTypeToAssetType(byte p_legacyDbAssetType)
    {
        return p_legacyDbAssetType switch
        {
            0 => AssetType.Unknown,
            1 => AssetType.CurrencyCash,
            2 => AssetType.Stock,
            _ => AssetType.Unknown
        };
    }

    static byte MapAssetTypeToLegacyDbAssetType(AssetType p_assetType)
    {
        return p_assetType switch
        {
            AssetType.CurrencyCash => 1,
            AssetType.Stock => 2,
            AssetType.Futures => 3,
            AssetType.Bond => 4,
            AssetType.Option => 5,
            AssetType.Commodity => 6,
            AssetType.RealEstate => 7,
            // AssetType.BenchmarkIndex => 8,
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