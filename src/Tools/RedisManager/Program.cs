﻿using System;
using SqCommon;
using NLog;
using System.Xml;
using System.Threading;
using System.Net.NetworkInformation;
using Microsoft.Extensions.Configuration;
using System.IO;

namespace RedisManager
{
    class Program
    {
        //private static readonly NLog.Logger gLogger = NLog.LogManager.GetCurrentClassLogger();   // the name of the logger will be the "Namespace.Class"
        private static readonly NLog.Logger gLogger = NLog.LogManager.GetLogger("Program");   // the name of the logger will be not the "Namespace.Class", but whatever you prefer: "Program"

         public static IConfigurationRoot gConfiguration = new ConfigurationBuilder().Build();

        static void Main(string[] args)
        {
            string appName = System.Reflection.MethodBase.GetCurrentMethod()?.ReflectedType?.Namespace ?? "UnknownNamespace";
            Console.Title = $"{appName} v1.0.14";
            string systemEnvStr = $"(v1.0.14, {Utils.RuntimeConfig() /* Debug | Release */}, CLR: {System.Environment.Version}, {System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription},  OS: {System.Environment.OSVersion}, user: {System.Environment.UserName}, CPU: {System.Environment.ProcessorCount}, ThId-{Thread.CurrentThread.ManagedThreadId})";
            Console.WriteLine($"Hello {appName}. {systemEnvStr}");
            gLogger.Info($"********** Main() START {systemEnvStr}");

            string sensitiveConfigFullPath = Utils.SensitiveConfigFolderPath() + $"SqCore.Tools.{appName}.NoGitHub.json";
            string systemEnvStr2 = $"Current working directory of the app: '{Directory.GetCurrentDirectory()}',{Environment.NewLine}SensitiveConfigFullPath: '{sensitiveConfigFullPath}'";
            gLogger.Info(systemEnvStr2);

            var builder = new ConfigurationBuilder()
               .SetBasePath(Directory.GetCurrentDirectory())        // GetCurrentDirectory() is the folder of the '*.csproj'.
               .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)      // no need to copy appsettings.json to the sub-directory of the EXE. 
               .AddJsonFile(sensitiveConfigFullPath, optional: true, reloadOnChange: true);
            //.AddUserSecrets<Program>()    // Used mostly in Development only, not in Production. Stored in a JSON configuration file in a system-protected user profile folder on the local machine. (e.g. user's %APPDATA%\Microsoft\UserSecrets\), the secret values aren't encrypted, but could be in the future.
            // do we need it?: No. Sensitive files are in separate folders, not up on GitHub. If server is not hacked, we don't care if somebody who runs the code can read the settings file. Also, scrambling secret file makes it more difficult to change it realtime.
            //.AddEnvironmentVariables();   // not needed in general. We dont' want to clutter op.sys. environment variables with app specific values.
            gConfiguration = builder.Build();

           

            Controller.g_controller.Start();

            string userInput = string.Empty;
            do
            {
                userInput = DisplayMenuAndExecute();
            } while (userInput != "UserChosenExit" && userInput != "ConsoleIsForcedToShutDown");

            gLogger.Info("****** Main() END");
            Controller.g_controller.Exit();
            NLog.LogManager.Shutdown();
        }

        

        static bool gIsFirstCall = true;
        static public string DisplayMenuAndExecute()
        {
            if (!gIsFirstCall)
                Console.WriteLine();
            gIsFirstCall = false;

            ColorConsole.WriteLine(ConsoleColor.Magenta, "----  (type and press Enter)  ----");
            Console.WriteLine("1. Say Hello. Don't do anything. Check responsivenes.");
            Console.WriteLine("2. Test Ping");
            Console.WriteLine("3. Test PostgreSQL");
            Console.WriteLine("4. Test Redis Cache");
            Console.WriteLine("5. Convert [sq_user] table from PostgreSql to Redis data");
            Console.WriteLine("6. Convert [some important] tables from PostgreSql to Redis data (Quick)");
            Console.WriteLine("7. Convert [all] tables from PostgreSql to Redis data (Full)");
            Console.WriteLine("8. Manage NAV assets...");
            Console.WriteLine("9. Exit gracefully (Avoid Ctrl-^C).");
            string userInput = string.Empty;
            try
            {
                userInput = Console.ReadLine() ?? string.Empty;
            }
            catch (System.IO.IOException e) // on Linux, of somebody closes the Terminal Window, Console.Readline() will throw an Exception with Message "Input/output error"
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
                    Controller.g_controller.TestPing();
                    break;
                case "3":
                    Controller.g_controller.TestPostgreSql();
                    break;
                case "4":
                    Controller.g_controller.TestRedisCache();
                    break;
                case "5":
                    Controller.g_controller.ConvertTableDataToRedis(new string[] { "sq_user" });
                    break;
                case "6":
                    Controller.g_controller.ConvertTableDataToRedis(new string[] { "sq_user", "sq_user" });
                    break;
                case "7":
                    Controller.g_controller.ConvertTableDataToRedis(new string[] { "sq_user", "sq_user", "sq_user" });
                    break;
                case "8":
                    string userInputSub = string.Empty;
                    do
                    {
                        userInputSub = DisplaySubMenuAndExecuteManageNavs();
                    } while (userInputSub != "UserChosenExit" && userInputSub != "ConsoleIsForcedToShutDown");
                   break;
                case "9":
                    return "UserChosenExit";
            }
            return string.Empty;
        }

        static public string DisplaySubMenuAndExecuteManageNavs()
        {
            ColorConsole.WriteLine(ConsoleColor.Magenta, "---- Manage NAV assets...Create Redis backup!!!  ----");
            Console.WriteLine("1. Convert NAV asset CSV file to RedisDb");
            Console.WriteLine("2. Export Nav Asset To Txt file: 9:1");
            Console.WriteLine("3. Export Nav Asset To Txt file: 9:2");
            Console.WriteLine("4. Export Nav Asset To Txt file: 9:3");
            Console.WriteLine("5. Import Nav Asset From Txt file: 9:1");
            Console.WriteLine("6. Import Nav Asset From Txt file: 9:2");
            Console.WriteLine("7. Import Nav Asset From Txt file: 9:3");
            
            Console.WriteLine("9. Exit to main menu.");
            string userInput = string.Empty;
            try
            {
                userInput = Console.ReadLine() ?? string.Empty;
            }
            catch (System.IO.IOException e) // on Linux, of somebody closes the Terminal Window, Console.Readline() will throw an Exception with Message "Input/output error"
            {
                gLogger.Info($"Console.ReadLine() exception. Somebody closes the Terminal Window: {e.Message}");
                return "ConsoleIsForcedToShutDown";
            }

            switch (userInput)
            {
                case "1":
                    Console.WriteLine("Warning! Commented lines. Doing nothing at the moment. Be very careful! Create a RedisDb backup, before uncommenting lines, because it will overwrite RedisDb and the daily NAV updates will be lost.");
                    //Controller.g_controller.InsertNavAssetFromCsvFile("9:1", @"g:\agy\money\Investment\IB\Reports\PortfolioAnalyst\2020-11-18\Gyorgy_Antal_Inception_November_17_2020.csv");
                    //Controller.g_controller.InsertNavAssetFromCsvFile("9:2", @"g:\work\Archi-data\Projects\IB-PortfolioAnalyst\2020-11-17\Didier_Charmat_and_Jean-Marc_Charmat_Inception_November_17_2020.csv");
                    //Controller.g_controller.InsertNavAssetFromCsvFile("9:3", @"g:\work\Archi-data\Projects\IB-PortfolioAnalyst\2020-11-17\DE_BLANZAC_LTD_Inception_November_17_2020.csv");
                    break;
                case "2":
                    Controller.g_controller.ExportNavAssetToTxt("9:1", @"assetQuoteRaw-unbrotlied-9-1.txt");
                    break;
                case "3":
                    Controller.g_controller.ExportNavAssetToTxt("9:2", @"assetQuoteRaw-unbrotlied-9-2.txt");
                    break;
                case "4":
                    Controller.g_controller.ExportNavAssetToTxt("9:3", @"assetQuoteRaw-unbrotlied-9-3.txt");
                    break;
                case "5":
                    Controller.g_controller.ImportNavAssetFromTxt("9:1", @"assetQuoteRaw-unbrotlied-9-1.txt");
                    break;
                case "6":
                    Controller.g_controller.ImportNavAssetFromTxt("9:2", @"assetQuoteRaw-unbrotlied-9-2.txt");
                    break;
                case "7":
                    Controller.g_controller.ImportNavAssetFromTxt("9:3", @"assetQuoteRaw-unbrotlied-9-3.txt");
                    break;
                case "9":
                    return "UserChosenExit";
            }
            return string.Empty;
        }

    }
}
