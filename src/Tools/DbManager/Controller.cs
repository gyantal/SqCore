using System;
using System.IO;
using System.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Data.SqlClient;
using SqCommon;
using System.Collections.Generic;
using System.Linq;
using System.Text;

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

    public void BackupLegacyDb(string p_backupPath) // e.g, backupPath:"C:/SqCoreWeb_LegacyDb"
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
        if (!Directory.Exists(p_backupPath))
        {
            Console.WriteLine($"Directory '{p_backupPath}' does not exist. Creating it now...");
            Directory.CreateDirectory(p_backupPath);
            Console.WriteLine("Directory created successfully.");
        }

        try
        {
            string utcDateTimeStr = DateTime.UtcNow.ToYYMMDDTHHMM();
            List<(string TableName, string FileName)> legacyDbTablesAndFileNames = [ ("PortfolioItem", $"portfolioItemBackup{utcDateTimeStr}.csv"), ("FileSystemItem", $"fileSystemItemBackup{utcDateTimeStr}.csv"), ("Stock", $"stockBackup{utcDateTimeStr}.csv") ];
            // step2: export legacyDb selected tables to csv file
            foreach ((string TableName, string FileName) item in legacyDbTablesAndFileNames)
                ExportLegacyDbTableToCsv(g_connection, p_backupPath, item.TableName, item.FileName);
            // step3: compress all csv files using 7z tool
            CompressLegacyDbBackupFiles(p_backupPath, legacyDbTablesAndFileNames.Select(r => r.FileName).ToList(), utcDateTimeStr);
            Console.WriteLine($"Backup process completed. zip file created");
        }
        catch (Exception e)
        {
            Utils.Logger.Error($"An error occurred: {e.Message}");
        }
        g_connection.Close();
    }

    static void ExportLegacyDbTableToCsv(SqlConnection p_connection, string p_backupPath, string p_tableName, string p_fileName)
    {
        string queryStr = $"SELECT TOP 100 * FROM {p_tableName}"; // Limit to 100 rows for testing
        using SqlCommand command = new(queryStr, p_connection);
        using SqlDataReader reader = command.ExecuteReader();

        string exportFilePath = Path.Combine(p_backupPath, p_fileName);
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
        Console.WriteLine($"CSV file created successfully for {p_tableName} at: {exportFilePath}");
    }

    static void CompressLegacyDbBackupFiles(string p_backupPath, List<string> p_fileNames, string p_utcDateTimeStr)
    {
        // step1: Define File Paths and Check for 7-Zip
        string compressedLegacyDbFileName = $"legacyDbBackup_{p_utcDateTimeStr}.7z";
        string compressedBackupFilePath = Path.Combine(p_backupPath, compressedLegacyDbFileName);
        string compressionToolPath = @"C:\Program Files\7-Zip\7z.exe"; // Path to 7z.exe
        if (!File.Exists(compressionToolPath))
        {
            Console.WriteLine("7z.exe not found. Please install 7-Zip and update the path.");
            return;
        }
        // step2: Prepare File List for Compression
        StringBuilder sbFilesToCompress = new StringBuilder();
        foreach (string fileName in p_fileNames)
        {
            if (sbFilesToCompress.Length > 0)
                sbFilesToCompress.Append(" ");
            sbFilesToCompress.Append($"\"{Path.Combine(p_backupPath, fileName)}\"");
        }
        // step3: Configure ProcessStartInfo(PSI) to execute 7-Zip
        ProcessStartInfo psi = new()
        {
            FileName = compressionToolPath,
            Arguments = $"a \"{compressedBackupFilePath}\" {sbFilesToCompress}",
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
        // step4: Delete the csv files after zipping
        foreach (string fileName in p_fileNames)
        {
            string filePath = Path.Combine(p_backupPath, fileName);
            if (File.Exists(filePath))
                File.Delete(filePath);
        }
    }

    public void ExportLegacyDbAsBacpac(string p_backupPath)
    {
        // Step1: Check SqlPackage Version
        string cmdExePath = "cmd.exe";
        string sqlPackageVersionArgs = "/c SqlPackage /Version";
        (string sqlPackageVersion, string sqlPackageErr) = ProcessCommandHelper(cmdExePath, sqlPackageVersionArgs);
        if (!string.IsNullOrWhiteSpace(sqlPackageErr))
        {
            Console.WriteLine($"SqlPackage Version Error: {sqlPackageErr}");
            return;
        }
        Console.WriteLine($"SqlPackage Version: {sqlPackageVersion}");
        // Step2: Locate SqlPackage.exe Path
        string sqlPackageLocateArgs = "/c where SqlPackage";
        (string sqlPackagePath, string sqlPackagePathErr) = ProcessCommandHelper(cmdExePath, sqlPackageLocateArgs);
        if (!string.IsNullOrWhiteSpace(sqlPackagePathErr))
        {
            Console.WriteLine($"SqlPackage Path Error: {sqlPackagePathErr}");
            return;
        }
        // If multiple paths are returned, select the first one that is non-empty and points to an existing file
        string? sqlPackageExePath = sqlPackagePath.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault(path => !string.IsNullOrWhiteSpace(path) && File.Exists(path));
        if (sqlPackageExePath == null)
        {
            Console.WriteLine("SqlPackage.exe not found or invalid path returned.");
            return;
        }
        Console.WriteLine($"SqlPackage Path: {sqlPackageExePath}");
        // Step3: Export Legacy Database to BACPAC
        string legacyDbConnString = Program.gConfiguration.GetConnectionString("LegacyMsSqlDefault") ?? throw new SqException("ConnectionString is missing from Config"); // apporxiamte Time : 2 mins 30 secs
        // string legacyDbConnString = "Data Source=DAYA-DESKTOP\\MSSQLSERVER1;Initial Catalog=HqSqlDb20250225_old;User ID=sa;Password=11235;TrustServerCertificate=True"; // To be deleted, just showing as a refernce.
        string bacpacLegacyDbFileName = $"legacyDbBackup_{DateTime.UtcNow.ToYYMMDDTHHMM()}.bacpac";
        string bacpacBackupFilePath = Path.Combine(p_backupPath, bacpacLegacyDbFileName);
        string bacpacExportArgs = $"/Action:Export /SourceConnectionString:\"{legacyDbConnString}\" /TargetFile:\"{bacpacBackupFilePath}\"";
        (string bacpacOutputMsg, string bacpacErrorMsg) = ProcessCommandHelper(sqlPackageExePath, bacpacExportArgs);
        if (!string.IsNullOrWhiteSpace(bacpacErrorMsg))
            Console.WriteLine("Error: " + bacpacErrorMsg);
        else
            Console.WriteLine("BACPAC exported successfully: " + bacpacOutputMsg);
    }

    private static (string Output, string Error) ProcessCommandHelper(string p_exePath, string p_arguments)
    {
        try
        {
            Process process = new Process();
            process.StartInfo.FileName = p_exePath;
            process.StartInfo.Arguments = p_arguments;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;
            process.StartInfo.CreateNoWindow = true;

            process.Start();
            string output = process.StandardOutput.ReadToEnd().Trim();
            string error = process.StandardError.ReadToEnd().Trim();
            process.WaitForExit();
            return (output, error);
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error while executing command: " + ex.Message);
            return (string.Empty, $"Exception: {ex.Message}");
        }
    }
}