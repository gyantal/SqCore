using System;
using SqCommon;
using Microsoft.Extensions.Configuration;
using System.IO;
using System.Collections.Generic;
using Microsoft.IdentityModel.Tokens;

namespace DbManager;

enum WorkMode { UserConsoleMenu, LegacyDbBackup, LegacyDbRestore, RedisDbBackup, RedisDbRestore, PostgreDbBackup, PostgreDbRestore };

class Program
{
    //private static readonly NLog.Logger gLogger = NLog.LogManager.GetCurrentClassLogger();   // the name of the logger will be the "Namespace.Class"
    private static readonly NLog.Logger gLogger = NLog.LogManager.GetLogger("Program");   // the name of the logger will be not the "Namespace.Class", but whatever you prefer: "Program"
    public static IConfigurationRoot gConfiguration = new ConfigurationBuilder().Build();

    static List<WorkMode> gWorkModes = new();

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
        for (int i = 0; i < p_args.Length; i++)
            gWorkModes.Add(gStrToWorkMode[p_args[i]]);

        // Step 3: Process the 'automatic' workmodes
        if (gWorkModes.Contains(WorkMode.LegacyDbBackup))
            Controller.g_controller.BackupLegacyDb("C:/SqCoreWeb_LegacyDb");

        // Step 4: Show the User Console menu if necessary (if UserConsoleMenu)
        if (gWorkModes.IsNullOrEmpty() || gWorkModes.Contains(WorkMode.UserConsoleMenu))
            ShowUserConsoleMenu(appName, sensitiveConfigFullPath);

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
                Controller.g_controller.BackupLegacyDb("C:/SqCoreWeb_LegacyDb");
                break;
            case "4":
                Controller.g_controller.RestoreLegacyDbTables("C:/SqCoreWeb_LegacyDb");
                break;
            case "5":
                Controller.g_controller.ExportLegacyDbAsBacpac("C:/SqCoreWeb_LegacyDb");
                break;
            case "9":
                return "UserChosenExit";
        }
        return string.Empty;
    }

    public static readonly Dictionary<string, WorkMode> gStrToWorkMode = new()
    {
        { "legacybackup", WorkMode.LegacyDbBackup },
        { "legacyDbRestore", WorkMode.LegacyDbRestore },
        { "redisDbBackup", WorkMode.RedisDbBackup },
        { "redisDbRestore", WorkMode.RedisDbRestore },
        { "postgreDbBackup", WorkMode.PostgreDbBackup },
        { "postgreDbRestore", WorkMode.PostgreDbRestore },
    };
}
