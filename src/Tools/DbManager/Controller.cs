using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Data.SqlClient;
using SqCommon;
using System.IO;

namespace DbManager;

class Controller
{
    public static Controller g_controller = new();
    private SqlConnection? g_connection = null;

    internal static void Start()
    {
    }

    internal static void Exit()
    {
    }

    public void TestLegacyDb()
    {
        string legacySqlConnString = Program.gConfiguration.GetConnectionString("LegacyMsSqlDefault") ?? throw new SqException("ConnectionString is missing from Config");
        g_connection = new SqlConnection(legacySqlConnString);
        g_connection.Open();

        // Create a command to execute a simple SELECT query
        string queryStr = "SELECT COUNT(*) FROM [dbo].[Stock]";
        SqlCommand command = new(queryStr, g_connection);

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

        g_connection.Close();
    }

    public void LegacyDbBackup(string backupPath) // e.g, backupPath:"C:/SqCoreWeb_LegacyDb"
    {
        string legacyDbConnString = Program.gConfiguration.GetConnectionString("LegacyMsSqlDefault") ?? throw new SqException("ConnectionString is missing from Config");
        Utils.Logger.Info($"LegacyDbBackup(). ConnStr:{legacyDbConnString}");
        g_connection = new SqlConnection(legacyDbConnString);
        g_connection.Open();

        if (g_connection?.State != System.Data.ConnectionState.Open)
        {
            Utils.Logger.Error("LegacyDbBackup Error. Connection to SQL Server has not established successfully.");
            return;
        }

        // step1: check if the backupPath exists
        if (!Directory.Exists(backupPath))
        {
            Console.WriteLine($"Directory '{backupPath}' does not exist. Creating it now...");
            Directory.CreateDirectory(backupPath);
            Console.WriteLine("Directory created successfully.");
        }

        try
        {
            // step2: Biuld the query
            string queryStr = $"SELECT TOP 100 * FROM PortfolioItem"; // quering only 100 rows of data for testing purpose.
            SqlCommand command = new(queryStr, g_connection);
            SqlDataReader reader = command.ExecuteReader();
            string fileName = $"legacyDbBackup_{DateTime.Now:yyyy-MM-dd}.csv";
            string exportFilePath = Path.Combine(backupPath, fileName);

            // step3: write the data to csv file
            using StreamWriter writer = new StreamWriter(exportFilePath);
            // Write column headers
            for (int i = 0; i < reader.FieldCount; i++)
            {
                writer.Write(reader.GetName(i)); // Write the column name
                if (i < reader.FieldCount - 1) // Add a comma between columns (except for the last column)
                    writer.Write(",");
            }
            writer.WriteLine(); // End line

            // Write row data
            while (reader.Read())
            {
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    writer.Write($"\"{reader[i]?.ToString()?.Replace("\"", "\"\"") ?? ""}\""); // ensures the values containing quotes or any other special charaters are correctly written to the csv file
                    if (i < reader.FieldCount - 1) // Add a comma between columns (except for the last column)
                        writer.Write(",");
                }
                writer.WriteLine(); // End line
            }
            Console.WriteLine($"CSV file created successfully at: {exportFilePath}");
        }
        catch (Exception e)
        {
            Utils.Logger.Error($"An error occurred: {e.Message}");
        }
        g_connection.Close();
    }
}