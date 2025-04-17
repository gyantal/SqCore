using System;
using SqCommon;
using Microsoft.Extensions.Configuration;
using System.IO;
using System.Collections.Generic;
using Microsoft.IdentityModel.Tokens;

namespace DbManager;

enum WorkMode { UserConsoleMenu, LegacyDbTablesBackup, LegacyDbTablesRestore, LegacyDbFullBackup, LegacyDbFullRestore, RedisDbBackup, RedisDbRestore, PostgreDbBackup, PostgreDbRestore };

class Program
{
    //private static readonly NLog.Logger gLogger = NLog.LogManager.GetCurrentClassLogger();   // the name of the logger will be the "Namespace.Class"
    private static readonly NLog.Logger gLogger = NLog.LogManager.GetLogger("Program");   // the name of the logger will be not the "Namespace.Class", but whatever you prefer: "Program"
    public static IConfigurationRoot gConfiguration = new ConfigurationBuilder().Build();
    static Dictionary<WorkMode, string> gWorkModes = new();

    static void Main(string[] p_args)
    {
        // Step 1: initialyze gConfiguration
        string appName = System.Reflection.MethodBase.GetCurrentMethod()?.ReflectedType?.Namespace ?? "UnknownAppName";
        string sensitiveConfigFullPath = Utils.SensitiveConfigFolderPath() + $"SqCore.Tools.{appName}.NoGitHub.json";
        IConfigurationBuilder builder = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())        // GetCurrentDirectory() is the folder of the '*.csproj'.
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)      // no need to copy appsettings.json to the sub-directory of the EXE. 
            .AddJsonFile(sensitiveConfigFullPath, optional: true, reloadOnChange: true);
        //.AddUserSecrets<Program>()    // Used mostly in Development only, not in Production. Stored in a JSON configuration file in a system-protected user profile folder on the local machine. (e.g. user's %APPDATA%\Microsoft\UserSecrets\), the secret values aren't encrypted, but could be in the future.
        // do we need it?: No. Sensitive files are in separate folders, not up on GitHub. If server is not hacked, we don't care if somebody who runs the code can read the settings file. Also, scrambling secret file makes it more difficult to change it realtime.
        //.AddEnvironmentVariables();   // not needed in general. We dont' want to clutter op.sys. environment variables with app specific values.
        gConfiguration = builder.Build();

        // Step 2: Process command line args
        for (int i = 0; i < p_args.Length; i += 2) // Arguments are provided in pairs (e.g., "-legacytablesbackup" "C:/SqCoreWeb_LegacyDb"), so we increment i by 2 to process multiple work modes correctly.
        {
            string workmodeStr = p_args[i].Trim();
            string folderPath = (i + 1 < p_args.Length) ? p_args[i + 1].Trim().Trim('"') : string.Empty;
            if (gStrToWorkMode.TryGetValue(workmodeStr, out WorkMode workMode))
                gWorkModes[workMode] = folderPath;
            else
                Console.WriteLine($"Warning: Unrecognized work mode '{workmodeStr}'");
        }

        // Step 3: Process the 'automatic' workmodes
        foreach (WorkMode key in gWorkModes.Keys)
        {
            switch (key)
            {
                case WorkMode.LegacyDbTablesBackup:
                    Controller.g_controller.BackupLegacyDb(gWorkModes[key]);
                    break;
                case WorkMode.LegacyDbTablesRestore:
                    Controller.g_controller.RestoreLegacyDbTables(gWorkModes[key]);
                    break;
                case WorkMode.LegacyDbFullBackup:
                    Controller.g_controller.BackupLegacyDbFull(gWorkModes[key]);
                    break;
                case WorkMode.LegacyDbFullRestore:
                    Controller.g_controller.RestoreLegacyDbFull(gWorkModes[key]);
                    break;
            }
        }

        // Step 4: Show the User Console menu if necessary (if UserConsoleMenu)
        if (gWorkModes.IsNullOrEmpty() || gWorkModes.Keys.Contains(WorkMode.UserConsoleMenu))
            ShowUserConsoleMenu(appName, sensitiveConfigFullPath);
        else
        {
            Console.WriteLine("\nPress ENTER to end.");
            Console.ReadLine();
        }
        NLog.LogManager.Shutdown();
    }

    static public void ShowUserConsoleMenu(string p_appName, string p_sensitiveConfigFullPath)
    {
        Console.Title = $"{p_appName} v1.0.14";
        string systemEnvStr = $"(v1.0.14, {Utils.RuntimeConfig() /* Debug | Release */}, CLR: {System.Environment.Version}, {System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription},  OS: {System.Environment.OSVersion}, user: {System.Environment.UserName}, CPU: {System.Environment.ProcessorCount}, ThId-{Environment.CurrentManagedThreadId})";
        Console.WriteLine($"Hello {p_appName}. {systemEnvStr}");
        gLogger.Info($"********** Main() START {systemEnvStr}");
        string systemEnvStr2 = $"Current working directory of the app: '{Directory.GetCurrentDirectory()}',{Environment.NewLine}SensitiveConfigFullPath: '{p_sensitiveConfigFullPath}'";
        gLogger.Info(systemEnvStr2);

        Controller.Start();

        string userInput;
        do
        {
            userInput = DisplayMenuAndExecute();
        } while (userInput != "UserChosenExit" && userInput != "ConsoleIsForcedToShutDown");

        gLogger.Info("****** Main() END");
        Controller.Exit();
    }

    static bool gIsFirstCall = true;
    static public string DisplayMenuAndExecute()
    {
        if (!gIsFirstCall)
            Console.WriteLine();
        gIsFirstCall = false;

        ColorConsole.WriteLine(ConsoleColor.Magenta, "----  (type and press Enter)  ----");
        Console.WriteLine("1. Say Hello. Don't do anything. Check responsivenes.");
        Console.WriteLine("2. Test LegacyDb");
        Console.WriteLine("3. Backup LegacyDb (important tables)");
        Console.WriteLine("4. Restore LegacyDb (important tables)");
        Console.WriteLine("5. Backup LegacyDb (all, into *.bacpac)");
        Console.WriteLine("6. Restore LegacyDb (all, from *.bacpac)"); // warning SQL server should be configured as: EXEC sp_configure 'contained database authentication', 1; RECONFIGURE;
        Console.WriteLine("7. Restore LegacyDb Safe (important tables)");
        Console.WriteLine("9. Exit gracefully (Avoid Ctrl-^C).");
        string userInput;
        try
        {
            userInput = Console.ReadLine() ?? string.Empty;
        }
        catch (IOException e) // on Linux, of somebody closes the Terminal Window, Console.Readline() will throw an Exception with Message "Input/output error"
        {
            gLogger.Info($"Console.ReadLine() exception. Somebody closes the Terminal Window: {e.Message}");
            return "ConsoleIsForcedToShutDown";
        }

        switch (userInput)
        {
            case "1":
                Console.WriteLine("Hello. I am not crashed yet! :)");
                gLogger.Info("Hello. I am not crashed yet! :)");
                break;
            case "2":
                Controller.g_controller.TestLegacyDb();
                break;
            case "3":
                string? backupDirPath = GetDirOrFullPathFromUser();
                if (!string.IsNullOrEmpty(backupDirPath) && backupDirPath != "ConsoleIsForcedToShutDown")
                    Controller.g_controller.BackupLegacyDb(backupDirPath);
                break;
            case "4":
                string? restoreDirPath = GetDirOrFullPathFromUser();
                if (!string.IsNullOrEmpty(restoreDirPath) && restoreDirPath != "ConsoleIsForcedToShutDown")
                    Controller.g_controller.RestoreLegacyDbTables(restoreDirPath);
                break;
            case "5":
                string? fullBackupDirPath = GetDirOrFullPathFromUser();
                if (!string.IsNullOrEmpty(fullBackupDirPath) && fullBackupDirPath != "ConsoleIsForcedToShutDown")
                    Controller.g_controller.BackupLegacyDbFull(fullBackupDirPath);
                break;
            case "6":
                string? fullRestoreDirPath = GetDirOrFullPathFromUser();
                if (!string.IsNullOrEmpty(fullRestoreDirPath) && fullRestoreDirPath != "ConsoleIsForcedToShutDown")
                    Controller.g_controller.RestoreLegacyDbFull(fullRestoreDirPath);
                break;
            case "7": // Temp: to be deleted
                string? restoreTableDirPath = GetDirOrFullPathFromUser();
                if (!string.IsNullOrEmpty(restoreTableDirPath) && restoreTableDirPath != "ConsoleIsForcedToShutDown")
                    Controller.g_controller.RestoreLegacyDbTablesSafe(restoreTableDirPath);
                break;
            case "9":
                return "UserChosenExit";
        }
        return string.Empty;
    }

    public static string? GetDirOrFullPathFromUser()
    {
        Console.Write("Do you want to provide the full path to a file (Y/N)? ");
        string userInputFirstStr = Console.ReadLine() ?? string.Empty;
        bool isInputFolder = !userInputFirstStr.Equals("y", StringComparison.OrdinalIgnoreCase);
        Console.WriteLine(isInputFolder ? "Please enter the directory path (e.g., C:/SqCoreWeb_LegacyDb):" : "Please enter the full file path (e.g., C:/SqCoreWeb_LegacyDb/backup.7z):");

        try
        {
            string userInputSecondStr = Console.ReadLine()?.Trim('"', '\'') ?? string.Empty;
            if (string.IsNullOrWhiteSpace(userInputSecondStr))
            {
                Console.WriteLine("Path cannot be empty.");
                return string.Empty;
            }

            if (isInputFolder)
            {
                if (!Directory.Exists(userInputSecondStr))
                {
                    Console.WriteLine($"The directory path '{userInputSecondStr}' does not exist.");
                    return string.Empty;
                }
            }
            else
            {
                if (!File.Exists(userInputSecondStr))
                {
                    Console.WriteLine($"The file path '{userInputSecondStr}' does not exist.");
                    return string.Empty;
                }

                if (!userInputSecondStr.EndsWith(".7z", StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine("The file must have a .7z extension.");
                    return string.Empty;
                }
            }
            return userInputSecondStr;
        }
        catch (IOException e)
        {
            gLogger.Info($"Console.ReadLine() exception. Somebody closes the Terminal Window: {e.Message}");
            return "ConsoleIsForcedToShutDown";
        }
    }

    public static readonly Dictionary<string, WorkMode> gStrToWorkMode = new()
    {
        { "-legacytablesbackup", WorkMode.LegacyDbTablesBackup },
        { "-legacytablesrestore", WorkMode.LegacyDbTablesRestore },
        { "-legacyfullbackup", WorkMode.LegacyDbFullBackup },
        { "-legacyfullrestore", WorkMode.LegacyDbFullRestore },
        { "-redisbackup", WorkMode.RedisDbBackup },
        { "-redisrestore", WorkMode.RedisDbRestore },
        { "-postgrebackup", WorkMode.PostgreDbBackup },
        { "-postgrerestore", WorkMode.PostgreDbRestore },
    };
}
