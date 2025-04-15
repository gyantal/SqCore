using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Data;
using System.Collections.Generic;
using System.Globalization;
using System.Diagnostics;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using CsvHelper;
using CsvHelper.Configuration;
using SqCommon;

namespace DbManager;

class Controller
{
    public static Controller g_controller = new();
    private SqlConnection? g_connection = null;
    static bool g_isUseLiveSqlDb = false; // to switch easily between Live (default) or Test (Developer local SQL)

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
            Console.WriteLine($"Success - Backup process completed");
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

    // 2025-04-09: LiveDB BackupFull to *.bacpac time (from India): ~20 minutes, filesize: 2,062,797 bytes.
    public void BackupLegacyDbFull(string p_backupPath)
    {
        (string? sqlPackageExePath, string? errorMsg) = GetSqlPackageExePath();
        if (errorMsg != null)
        {
            Console.WriteLine($"{errorMsg}");
            return;
        }

        // Step3: Export Legacy Database to BACPAC
        string legacyDbConnString = Program.gConfiguration.GetConnectionString("LegacyMsSqlDefault") ?? throw new SqException("ConnectionString is missing from Config"); // apporxiamte Time : 2 mins 30 secs
        // string legacyDbConnString = "Data Source=DAYA-DESKTOP\\MSSQLSERVER1;Initial Catalog=HqSqlDb20250225_old;User ID=sa;Password=11235;TrustServerCertificate=True"; // To be deleted, just showing as a refernce.
        string bacpacLegacyDbFileName = $"legacyDbBackup_{DateTime.UtcNow.ToYYMMDDTHHMM()}.bacpac";
        string bacpacBackupFilePath = Path.Combine(p_backupPath, bacpacLegacyDbFileName);
        string bacpacExportArgs = $"/Action:Export /SourceConnectionString:\"{legacyDbConnString}\" /TargetFile:\"{bacpacBackupFilePath}\"";
        (string bacpacOutputMsg, string bacpacErrorMsg) = ProcessCommandHelper(sqlPackageExePath!, bacpacExportArgs);
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

    public void RestoreLegacyDbTables(string p_backupPathFileOrDir)
    {
        // Step 1: Identify the backup file (.7z) in the specified directory
        Console.WriteLine($"Restore {p_backupPathFileOrDir}");
        string zipFileFullPath;
        string backupDir;
        if (p_backupPathFileOrDir.EndsWith(".7z")) // If it ends with .7z assume the file was given as parameter
        {
            zipFileFullPath = p_backupPathFileOrDir;
            backupDir = Path.GetDirectoryName(p_backupPathFileOrDir) ?? throw new SqException("Invalid path: Directory doesnt exists");
        }
        else
        {
            FileInfo? latestZipFile = new DirectoryInfo(p_backupPathFileOrDir).GetFiles("*.7z").OrderByDescending(f => f.LastWriteTime).FirstOrDefault();
            zipFileFullPath = latestZipFile!.FullName;
            backupDir = p_backupPathFileOrDir;
        }
        // Step 2: Extract the contents of the ZIP file to the backup path using 7-Zip
        string zipExePath = @"C:\Program Files\7-Zip\7z.exe";
        string zipProcessArgs = $"x \"{zipFileFullPath}\" -o\"{backupDir}\" -y";
        (string zipOutputMsg, string zipErrorMsg) = ProcessCommandHelper(zipExePath, zipProcessArgs);
        if (!string.IsNullOrWhiteSpace(zipErrorMsg))
        {
            Console.WriteLine($"SqlPackage Path Error: {zipErrorMsg}");
            return;
        }
        Console.WriteLine($"Extraction completed {zipOutputMsg}");
        // We have to pay attention to the dependency relations between data tables.
        // PortfolioItem.PortfolioID refers to rows in the FileSystemItem table.
        // PortfolioItem.AssetSubTableID refers to rows in the Stock (or Options) table. (although this in not strictly enforced in the SQL table)
        // Therefore, when we delete old data, delete in the following order: 1. PortfolioItem, 2. FileSystemItem and Stock.
        // When we create the new data tables, do it in the following order: 1. FileSystemItem and Stock. 2. PortfolioItem

        // Step 3: Connect to the legacy database and delete existing data (in reverse dependency order)
        string legacyDbConnString = "Data Source=DAYA-DESKTOP\\MSSQLSERVER1;Initial Catalog=HqSqlDb20250225_Copy;User ID=sa;Password=11235;TrustServerCertificate=True"; // Try it with your localDbConn string
        g_connection = new SqlConnection(legacyDbConnString);
        g_connection.Open();
        List<string> legacyDbTables = [ "FileSystemItem", "Stock", "PortfolioItem" ];
        for (int i = legacyDbTables.Count - 1; i >= 0; i--) // Deleting in reverse order to ensure PortfolioItem is deleted before FileSystem and Stock entries
        {
            SqlCommand cmd = new SqlCommand($"DELETE FROM {legacyDbTables[i]}", g_connection);
            cmd.CommandTimeout = 300;
            cmd.ExecuteNonQuery();
            Console.WriteLine($"Deletion complete for table: {legacyDbTables[i]}");
        }
        // Step 4: Insert data from extracted CSV files into the respective tables
        string[] csvFiles = Directory.GetFiles(backupDir, "*.csv");
        foreach (string file in csvFiles)
        {
            string fileName = Path.GetFileName(file);
            string? matchedTable = legacyDbTables.FirstOrDefault(table => fileName.Contains(table, StringComparison.OrdinalIgnoreCase)); // Find matching table name from the list
            if (matchedTable != null)
                InsertCsvFileToLegacyDbTable(g_connection, file, matchedTable);
        }
        Console.WriteLine("Legacy DB restoration complete.");
        g_connection.Close();
        // step5: Delete the csv files after Inserting
        foreach (string fileName in csvFiles)
        {
            string filePath = Path.Combine(backupDir, fileName);
            if (File.Exists(filePath))
                File.Delete(filePath);
        }
    }

    public void RestoreLegacyDbTablesSafe(string p_backupPathFileOrDir)
    {
        string legacyDbConnString;
        if (g_isUseLiveSqlDb)
            legacyDbConnString = Program.gConfiguration.GetConnectionString("LegacyMsSqlDefault") ?? throw new SqException("ConnectionString is missing from Config");
        else
            legacyDbConnString = "Data Source=DAYA-DESKTOP\\MSSQLSERVER1;Initial Catalog=legacyDbFull;User ID=sa;Password=11235;TrustServerCertificate=True"; // For testing. (Developer local SQL)
        g_connection = new SqlConnection(legacyDbConnString);
        g_connection.Open();
        string utcDateTimeStr = DateTime.UtcNow.ToYYMMDDTHHMM();
        // Step1: Create New tables
        string? createQueryErrorMsg = CreateStockTable(g_connection, utcDateTimeStr);
        if (createQueryErrorMsg != null)
        {
            Console.WriteLine(createQueryErrorMsg);
            return;
        }
        // Step2: InsertData
        string? InsertDataErrMsg = InsertData(p_backupPathFileOrDir, g_connection, utcDateTimeStr);
        if (InsertDataErrMsg != null)
        {
            Console.WriteLine(InsertDataErrMsg);
            return;
        }
        // Step3: Rename and Drop
        string? renameAndDropTableErrMsg = RenameAndDropTable(g_connection, utcDateTimeStr);
        if (renameAndDropTableErrMsg != null)
        {
            Console.WriteLine(renameAndDropTableErrMsg);
            return;
        }
        Console.WriteLine("Success - Restored legacyDb tables");
        g_connection.Close();
    }

    private static string? CreateStockTable(SqlConnection p_connection, string utcDateTimeStr)
    {
        try
        {
            string createQueryStr = $@" CREATE TABLE [dbo].[Stock_New{utcDateTimeStr}](
            [ID] [int] IDENTITY(1,1) NOT NULL,
            [CompanyID] [int] NULL,
            [FundID] [int] NULL,
            [ISIN] [varchar](12) NULL,
            [Ticker] [varchar](20) NOT NULL,
            [IsAlive] [bit] NOT NULL,
            [CurrencyID] [smallint] NULL,
            [StockExchangeID] [tinyint] NULL,
            [Name] [nvarchar](128) NULL,
        CONSTRAINT [PK_Stock_New{utcDateTimeStr}] PRIMARY KEY CLUSTERED 
            ( [ID] ASC )WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, 
                            ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
        ) ON [PRIMARY];
        ALTER TABLE [dbo].[Stock_New{utcDateTimeStr}]
            ADD CONSTRAINT [DF_Stock_IsAlive_New{utcDateTimeStr}] DEFAULT ((1)) FOR [IsAlive];
        ALTER TABLE [dbo].[Stock_New{utcDateTimeStr}] WITH NOCHECK
            ADD CONSTRAINT [FK_Stock_Company_New{utcDateTimeStr}] FOREIGN KEY([CompanyID]) REFERENCES [dbo].[Company] ([ID]);
        ALTER TABLE [dbo].[Stock_New{utcDateTimeStr}] NOCHECK CONSTRAINT [FK_Stock_Company_New{utcDateTimeStr}];
        ALTER TABLE [dbo].[Stock_New{utcDateTimeStr}] WITH NOCHECK
            ADD CONSTRAINT [FK_Stock_Currency_New{utcDateTimeStr}] FOREIGN KEY([CurrencyID]) REFERENCES [dbo].[Currency] ([ID]);
        ALTER TABLE [dbo].[Stock_New{utcDateTimeStr}] NOCHECK CONSTRAINT [FK_Stock_Currency_New{utcDateTimeStr}];
        ALTER TABLE [dbo].[Stock_New{utcDateTimeStr}] WITH NOCHECK
            ADD CONSTRAINT [FK_Stock_StockExchange_New{utcDateTimeStr}] FOREIGN KEY([StockExchangeID]) REFERENCES [dbo].[StockExchange] ([ID]);
        ALTER TABLE [dbo].[Stock_New{utcDateTimeStr}] NOCHECK CONSTRAINT [FK_Stock_StockExchange_New{utcDateTimeStr}];";
        SqlCommand createSqlCmd = new(createQueryStr, p_connection);
        createSqlCmd.ExecuteNonQuery();
        return null;
        }
        catch (SqlException ex)
        {
            return $"Error - SQL exception occurred while creating the table: {ex.Message}";
        }
        catch (Exception ex)
        {
            return $"Error - Unexpected exception during table creation: {ex.Message}";
        }
    }

    private static string? InsertData(string p_backupPathFileOrDir, SqlConnection p_connection, string p_utcDateTimeStr)
    {
        try
        {
            string zipFileFullPath;
            string backupDir;
            if (p_backupPathFileOrDir.EndsWith(".7z"))
            {
                zipFileFullPath = p_backupPathFileOrDir;
                backupDir = Path.GetDirectoryName(p_backupPathFileOrDir) ?? throw new SqException("Invalid path: Directory doesn't exist");
            }
            else
            {
                FileInfo? latestZipFile = new DirectoryInfo(p_backupPathFileOrDir).GetFiles("*.7z").OrderByDescending(f => f.LastWriteTime).FirstOrDefault();
                if (latestZipFile == null)
                    return "No .7z backup file found in the directory.";

                zipFileFullPath = latestZipFile.FullName;
                backupDir = p_backupPathFileOrDir;
            }

            // Extract the contents of the ZIP file
            string zipExePath = @"C:\Program Files\7-Zip\7z.exe";
            string zipProcessArgs = $"x \"{zipFileFullPath}\" -o\"{backupDir}\" -y";
            (string zipOutputMsg, string zipErrorMsg) = ProcessCommandHelper(zipExePath, zipProcessArgs);

            if (!string.IsNullOrWhiteSpace(zipErrorMsg))
                return zipErrorMsg;

            string[] csvFiles = Directory.GetFiles(backupDir, "*.csv");
            foreach (string file in csvFiles)
            {
                string fileName = Path.GetFileName(file);
                if (fileName.StartsWith("stock"))
                    InsertCsvFileToLegacyDbTable(p_connection, file, $"Stock_New{p_utcDateTimeStr}");
            }
            // Delete the csv files after inserting
            foreach (string fileName in csvFiles)
            {
                string filePath = Path.Combine(backupDir, fileName);
                if (File.Exists(filePath))
                    File.Delete(filePath);
            }
            return null;
        }
        catch (Exception ex)
        {
            return $"Error: {ex.Message}";
        }
    }

    private static void InsertCsvFileToLegacyDbTable(SqlConnection p_connection, string p_filePath, string p_tableName)
    {
        Console.WriteLine($"Inserting data from: {Path.GetFileName(p_filePath)}");
        SqlTransaction transaction = p_connection.BeginTransaction();
        try
        {
            StreamReader streamReader = new StreamReader(p_filePath);
            CsvReader csvReader = new CsvReader(streamReader, new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                Delimiter = ",",
                HasHeaderRecord = true,
            });

            CsvDataReader csvDataReader = new CsvDataReader(csvReader);
            // While inserting CSV data into the Stock table, an error occurred for the FundID column because the empty value ('') from the CSV, read as a string, could not be converted to an int.
            // CSV columns are read as strings by default, and empty cells are treated as empty strings ("") rather than null.
            // When this data is loaded into a DataTable, string columns accept these values, but non-string columns like int, decimal, or DateTime cannot interpret empty strings as null, leading to type conversion failures during SqlBulkCopy.
            // To resolve this, empty strings in the DataTable are replaced with DBNull.Value before the bulk insert, ensuring that empty values are treated as proper nulls and preventing runtime errors during data insertion.
            DataTable dataTable = new DataTable();
            dataTable.Load(csvDataReader);
            // Ensure columns are writable
            foreach (DataColumn col in dataTable.Columns)
                col.ReadOnly = false;

            // Replace empty or whitespace-only string cells in string columns with DBNull
            foreach (DataColumn col in dataTable.Columns)
            {
                if (col.DataType == typeof(string))
                {
                    foreach (DataRow row in dataTable.Rows)
                    {
                        if (row[col] is string val && string.IsNullOrWhiteSpace(val))
                            row[col] = DBNull.Value;
                    }
                }
            }
            // Error: IDENTITY_INSERT conflict
            // As the CSV files contain values for the identity column, IDENTITY_INSERT must be turned ON and OFF dynamically during insertion.
            using (SqlCommand cmd = new SqlCommand($"SET IDENTITY_INSERT {p_tableName} ON", p_connection, transaction))
            {
                cmd.ExecuteNonQuery();
            }

            // Create a SqlBulkCopy object to efficiently insert the data into SQL Server
            SqlBulkCopy bulkCopy = new SqlBulkCopy(p_connection, SqlBulkCopyOptions.KeepIdentity, transaction);
            bulkCopy.BulkCopyTimeout = 300;
            bulkCopy.DestinationTableName = p_tableName;
            bulkCopy.WriteToServer(dataTable);

            using (SqlCommand cmd = new SqlCommand($"SET IDENTITY_INSERT {p_tableName} OFF", p_connection, transaction)) // turning OFF
            {
                cmd.ExecuteNonQuery();
            }

            transaction.Commit(); // Commit the transaction after successful insertion
        }
        catch (Exception ex)
        {
            transaction.Rollback(); // Roll back the transaction if any error occurs during processing or insertion
            Console.WriteLine($"Error - During insert: {ex.Message}");
        }
    }

    private static string? RenameAndDropTable(SqlConnection p_connection, string p_utcDateTimeStr)
    {
        try
        {
            // Rename original table and constraints
            string renameOriginalQuery = $@"
                EXEC sp_rename N'dbo.Stock', N'Stock_Old{p_utcDateTimeStr}';
                EXEC sp_rename N'PK_Stock', N'PK_Stock_Old{p_utcDateTimeStr}';
                EXEC sp_rename N'DF_Stock_IsAlive', N'DF_Stock_IsAlive_Old{p_utcDateTimeStr}';
                EXEC sp_rename N'FK_Stock_Company', N'FK_Stock_Company_Old{p_utcDateTimeStr}';
                EXEC sp_rename N'FK_Stock_Currency', N'FK_Stock_Currency_Old{p_utcDateTimeStr}';
                EXEC sp_rename N'FK_Stock_StockExchange', N'FK_Stock_StockExchange_Old{p_utcDateTimeStr}';";
            SqlCommand renameOriginalCmd = new(renameOriginalQuery, p_connection);
            renameOriginalCmd.ExecuteNonQuery();

            // Rename new table and constraints to original names
            string renameNewQuery = $@"
                EXEC sp_rename N'dbo.Stock_New{p_utcDateTimeStr}', N'Stock';
                EXEC sp_rename N'PK_Stock_New{p_utcDateTimeStr}', N'PK_Stock';
                EXEC sp_rename N'DF_Stock_IsAlive_New{p_utcDateTimeStr}', N'DF_Stock_IsAlive';
                EXEC sp_rename N'FK_Stock_Company_New{p_utcDateTimeStr}', N'FK_Stock_Company';
                EXEC sp_rename N'FK_Stock_Currency_New{p_utcDateTimeStr}', N'FK_Stock_Currency';
                EXEC sp_rename N'FK_Stock_StockExchange_New{p_utcDateTimeStr}', N'FK_Stock_StockExchange';";
            SqlCommand renameNewCmd = new(renameNewQuery, p_connection);
            renameNewCmd.ExecuteNonQuery();

            // Drop the old table
            string oldTableName = $"Stock_Old{p_utcDateTimeStr}";
            DropTableAfterRemovingReferences(p_connection, oldTableName);
            return null;
        }
        catch (SqlException ex)
        {
            return $"Error - SQL exception during renaming or deletion: {ex.Message}";
        }
        catch (Exception ex)
        {
            return $"Error - {ex.Message}";
        }
    }

    // Creating a safe way to delete the target table by identifying and removing all foreign key constraints that reference it.
    // SQL Server does not allow dropping a table if it is referenced by any foreign key constraints in other tables.
    private static void DropTableAfterRemovingReferences(SqlConnection p_connection, string p_tableName)
    {
        // Step 1: Query to find all foreign keys that reference the specified table
        string findFKsQuery = $@"
            SELECT 
                fk.name AS ForeignKeyName,
                referencedTable.name AS ReferencingTable
            FROM 
                sys.foreign_keys fk
            JOIN sys.foreign_key_columns fkc ON fk.object_id = fkc.constraint_object_id
            JOIN sys.tables referencingTable ON fkc.parent_object_id = referencingTable.object_id
            JOIN sys.tables referencedTable ON fkc.referenced_object_id = referencedTable.object_id
            WHERE referencedTable.name = '{p_tableName}';
        ";

        using (SqlCommand command = new SqlCommand(findFKsQuery, p_connection))
        using (SqlDataReader reader = command.ExecuteReader())
        {
            List<string> foreignKeyDropStatements = new List<string>();
            // Step 2: Build ALTER TABLE statements to drop each foreign key constraint
            while (reader.Read())
            {
                string? fkName = reader["ForeignKeyName"] as string;
                string? referenceTable = reader["ReferencingTable"] as string;

                if (!string.IsNullOrWhiteSpace(fkName) && !string.IsNullOrWhiteSpace(referenceTable))
                    foreignKeyDropStatements.Add($"ALTER TABLE [{referenceTable}] DROP CONSTRAINT [{fkName}];");
            }
            reader.Close();

            // Step 3: Drop all foreign keys
            foreach (string dropConstraintSql in foreignKeyDropStatements)
            {
                using (SqlCommand dropCmd = new SqlCommand(dropConstraintSql , p_connection))
                {
                    dropCmd.ExecuteNonQuery();
                }
            }

            // Step 4: Drop the target table after all FKs referencing are removed
            using (SqlCommand dropTableCmd = new SqlCommand($"DROP TABLE {p_tableName};", p_connection))
            {
                dropTableCmd.ExecuteNonQuery();
            }
        }
    }

    public void RestoreLegacyDbFull(string p_backupPathFileOrDir)
    {
        (string? sqlPackageExePath, string? errorMsg) = GetSqlPackageExePath();
        if (errorMsg != null)
        {
            Console.WriteLine($"{errorMsg}");
            return;
        }
        // string legacyDbConnString = Program.gConfiguration.GetConnectionString("LegacyMsSqlDefault") ?? throw new SqException("ConnectionString is missing from Config");
        string legacyDbConnString = "Data Source=DAYA-DESKTOP\\MSSQLSERVER1;Initial Catalog=master;User ID=sa;Password=11235;TrustServerCertificate=True"; // To be deleted, just showing as a refernce.
        // Step3: Delete the database
        g_connection = new SqlConnection(legacyDbConnString);
        g_connection.Open();
        using (SqlCommand cmd = new SqlCommand("DROP DATABASE legacyDbBackup_250314T0731", g_connection)) // replace "legacyDbBackup_250314T0731" with actual database to be deleted.
        {
            cmd.CommandTimeout = 300;
            cmd.ExecuteNonQuery();
        }
        Console.WriteLine("Deleted existing database: legacyDbBackup_250314T0731");

        // Step4: import the bacpac file
        string bacpacFileFullPath;
        if (p_backupPathFileOrDir.EndsWith(".bacpac")) // If it ends with .bacpac assume the file was given as parameter
            bacpacFileFullPath = p_backupPathFileOrDir;
        else
        {
            FileInfo? latestBacpacFile = new DirectoryInfo(p_backupPathFileOrDir).GetFiles("*.bacpac").OrderByDescending(f => f.LastWriteTime).FirstOrDefault();
            if (latestBacpacFile == null)
            {
                Console.WriteLine("No .bacpac files found in the specified directory.");
                return;
            }
            bacpacFileFullPath = latestBacpacFile.FullName;
        }
        string targetDbName = Path.GetFileNameWithoutExtension(bacpacFileFullPath); // This will be used as the database name
        string targetDbConnString = $"Data Source=DAYA-DESKTOP\\MSSQLSERVER1;Initial Catalog={targetDbName};User ID=sa;Password=11235;TrustServerCertificate=True";
        string bacpacImportArgs = $"/Action:Import /TargetConnectionString:\"{targetDbConnString}\" /SourceFile:\"{bacpacFileFullPath}\"";
        (string importOutputMsg, string importErrorMsg) = ProcessCommandHelper(sqlPackageExePath!, bacpacImportArgs);
        if (importErrorMsg == null)
            Console.WriteLine("Successfully imported the bacpac file");
        else
            Console.WriteLine($"importErrorMsg: {importErrorMsg}");
        g_connection.Close();
    }

    static (string? sqlPackageExePath, string? errorMsg) GetSqlPackageExePath()
    {
        string cmdExePath = "cmd.exe";
        string sqlPackageVersionArgs = "/c SqlPackage /Version";
        // Step1: Check SqlPackage Version
        (string sqlPackageVersion, string sqlPackageErr) = ProcessCommandHelper(cmdExePath, sqlPackageVersionArgs);
        if (!string.IsNullOrWhiteSpace(sqlPackageErr))
            return (null, $"SqlPackage Version Error: {sqlPackageErr}");
        Console.WriteLine($"SqlPackage Version: {sqlPackageVersion}");

        // Step 2: Locate SqlPackage.exe Path
        string sqlPackageLocateArgs = "/c where SqlPackage";
        (string sqlPackagePath, string sqlPackagePathErr) = ProcessCommandHelper(cmdExePath, sqlPackageLocateArgs);
        if (!string.IsNullOrWhiteSpace(sqlPackagePathErr))
            return (null, $"SqlPackage Path Error: {sqlPackagePathErr}");

        // Step 3: Get the first valid executable path
        string? sqlPackageExePath = sqlPackagePath.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault(path => !string.IsNullOrWhiteSpace(path) && File.Exists(path));
        if (sqlPackageExePath == null)
            return (null, "SqlPackage.exe not found or invalid path returned.");

        return (sqlPackageExePath, null);
    }
}