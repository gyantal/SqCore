using System;
using System.IO;
using System.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Data.SqlClient;
using SqCommon;
using System.Collections.Generic;

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
            List<(string TableName, string FileName)> legacyDbTablesAndFileNames = [ ("PortfolioItem", "portfolioItemBackup.csv"), ("FileSystemItem", "fileSystemItemBackup.csv"), ("Stock", "stockBackup.csv") ];
            // step2: export legacyDb selected tables to csv file
            foreach ((string TableName, string FileName) item in legacyDbTablesAndFileNames)
                ExportLegacyDbTableToCsv(g_connection, backupPath, item.TableName, item.FileName);
            // step3: compress all csv files using 7z tool
            CompressLegacyDbBackupFiles(backupPath);
            Console.WriteLine($"Backup process completed. zip file created");
        }
        catch (Exception e)
        {
            Utils.Logger.Error($"An error occurred: {e.Message}");
        }
        g_connection.Close();
    }

    static void ExportLegacyDbTableToCsv(SqlConnection connection, string backupPath, string tableName, string fileName)
    {
        string queryStr = $"SELECT TOP 100 * FROM {tableName}"; // Limit to 100 rows for testing
        using SqlCommand command = new(queryStr, connection);
        using SqlDataReader reader = command.ExecuteReader();

        string exportFilePath = Path.Combine(backupPath, fileName);
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
        Console.WriteLine($"CSV file created successfully for {tableName} at: {exportFilePath}");
    }

    static void CompressLegacyDbBackupFiles(string backupPath)
    {
        string compressedLegacyDbFileName = $"legacyDbBackup_{DateTime.Now:yyyy-MM-dd}.7z";
        string compressedBackupFilePath = Path.Combine(backupPath, compressedLegacyDbFileName);
        string compressionToolPath = @"C:\Program Files\7-Zip\7z.exe"; // Path to 7z.exe
        string csvFileSelectionPattern = "*.csv"; // select all csv files in the folder

        if (!File.Exists(compressionToolPath))
        {
            Console.WriteLine("7z.exe not found. Please install 7-Zip and update the path.");
            return;
        }

        ProcessStartInfo psi = new() // Configuring the 7-Zip process using ProcessStartInfo (PSI)
        {
            FileName = compressionToolPath,
            Arguments = $"a \"{compressedBackupFilePath}\" \"{Path.Combine(backupPath, csvFileSelectionPattern)}\"",
            RedirectStandardOutput = true,
            CreateNoWindow = true
        };

        Process? process = Process.Start(psi);
        if (process == null)
        {
            Console.WriteLine("Failed to start the 7-Zip process.");
            return;
        }
        process.WaitForExit();
        Console.WriteLine($"zip file created successfully: {compressedBackupFilePath}");

        foreach (var file in Directory.GetFiles(backupPath, "*.csv"))
            File.Delete(file); // Delete csv files after zipping
    }
}