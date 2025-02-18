using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Data.SqlClient;
using SqCommon;

namespace DbManager;

class Controller
{
    public static Controller g_controller = new();
    private static SqlConnection? m_connection = null;

    internal static void Start()
    {
    }

    internal static void Exit()
    {
    }

    public static void TestLegacyDb()
    {
        string? legacySqlConnString = Program.gConfiguration.GetConnectionString("LegacyMsSqlDefault");
        m_connection = new SqlConnection(legacySqlConnString);
        m_connection.Open();

        // Create a command to execute a simple SELECT query
        string queryStr = "SELECT COUNT(*) FROM [dbo].[Stock]";
        SqlCommand command = new(queryStr, m_connection);

        try
        {
            using SqlDataReader reader = command.ExecuteReader();
            if (reader.Read())
            {
                int rowCount = reader.GetInt32(0); // Get the count value
                Console.WriteLine($"Total rows in Stock table: {rowCount}");
            }
            else
                Utils.Logger.Error("TestLegacyDb Error. No data found in Stock table.");
        }
        catch (Exception e)
        {
            Utils.Logger.Error($"TestLegacyDb Error. An error occurred while executing the query: {e.Message}");
        }
    }
}