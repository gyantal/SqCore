using System;
using SqCommon;
using NLog;
using System.Xml;
using System.Threading;
using System.Net.NetworkInformation;
using Microsoft.Extensions.Configuration;
using System.IO;

namespace BenchmarkDB
{
    class Program
    {
        //private static readonly NLog.Logger gLogger = NLog.LogManager.GetCurrentClassLogger();   // the name of the logger will be the "Namespace.Class"
        private static readonly NLog.Logger gLogger = NLog.LogManager.GetLogger("Program");   // the name of the logger will be not the "Namespace.Class", but whatever you prefer: "Program"

        public static IConfigurationRoot gConfiguration = new ConfigurationBuilder().Build();

        static void Main(string[] _)
        {
            string appName = System.Reflection.MethodBase.GetCurrentMethod()?.ReflectedType?.Namespace ?? "UnknownNamespace";
            Console.Title = $"{appName} v1.0.14";
            string systemEnvStr = $"(v1.0.14, {Utils.RuntimeConfig() /* Debug | Release */}, CLR: {System.Environment.Version}, {System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription},  OS: {System.Environment.OSVersion}, user: {System.Environment.UserName}, CPU: {System.Environment.ProcessorCount}, ThId-{Environment.CurrentManagedThreadId})";
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



            //Test_Csharp80_Features();
            Controller.Start();

            string userInput;
            do
            {
                userInput = DisplayMenuAndExecute();
            } while (userInput != "UserChosenExit" && userInput != "ConsoleIsForcedToShutDown");

            gLogger.Info("********** Main() END");
            Controller.Exit();
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
            Console.WriteLine("5. Benchmark all and make conclusions (target: remote, execute: PC, Linux)");
            Console.WriteLine("6. Benchmark all and make conclusions (target: localhost, execute: PC)");
            Console.WriteLine("7. Benchmark all and make conclusions (target: localhost, execute: Linux)");
            Console.WriteLine("8. Test downloading api.nasdaq.com (tricky HttpHeaders).");
            Console.WriteLine("9. Exit gracefully (Avoid Ctrl-^C).");
            string userInput;
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
                    Controller.TestPing();
                    break;
                case "3":
                    Controller.TestPostgreSql();
                    break;
                case "4":
                    Controller.TestRedisCache();
                    break;
                case "5":
                    // Console.WriteLine("5. Benchmark all and make conclusions (target: remote, execute: PC, Linux)");
                    Controller.BenchmarkAllAndConclusions(
                        Program.gConfiguration.GetConnectionString("PingDefault"),
                        Program.gConfiguration.GetConnectionString("PostgreSqlDefault"),
                        Program.gConfiguration.GetConnectionString("RedisDefault"));
                    break;
                case "6":
                    // Console.WriteLine("6. Benchmark all and make conclusions (target: localhost, execute: PC)");
                    // start redis on WSL: in "ubuntu@gyantal-PC:~/redis/redis-stable$" type 'redis-server'     (check if it works in another terminal: 'redis-cli ping')
                    Controller.BenchmarkAllAndConclusions(
                        "localhost",
                        Program.gConfiguration.GetConnectionString("PostgreSqlWinLocalhost"),
                        Program.gConfiguration.GetConnectionString("RedisWinLocalhost"));
                    break;
                case "7":
                    // Console.WriteLine("7. Benchmark all and make conclusions (target: localhost, execute: Linux)");
                    Controller.BenchmarkAllAndConclusions(
                        "localhost",
                        Program.gConfiguration.GetConnectionString("PostgreSqlLinuxLocalhost"),
                        Program.gConfiguration.GetConnectionString("RedisLinuxLocalhost"));
                    break;
                case "8":
                    Utils.TestDownloadApiNasdaqCom();
                    break;
                case "9":
                    return "UserChosenExit";
            }
            return string.Empty;
        }


        public struct TestCSharp80Class
        {
            public double X { get; set; }
            public double Y { get; set; }
            public readonly double Distance => Math.Sqrt(X * X + Y * Y);        // Readonly members of a struct is a C#8.0 feature. Doesn't work for classes.
        }

        public static void Test_Csharp80_Features()
        {
            try
            {
                gLogger.Info("Hello NLog");
                gLogger.Info("Hello {0}", "Earth");          // output: Hello Earth |BenchmarkDB.Program|
                // output: Hello "Earth" |BenchmarkDB.Program|Name=Earth // Agy: this is a shorthand cheating to support C# string interpolation with variables.
                // that way, it is easy to change any other string interpolation that contains variable names (like Name) is just formatted easily in Nlog.
                // the only thing to remember that the next parameters should be in order of appearance. If it is mistaken the log file will contain these associations if '${all-event-properties}' is given in layout format.
                // The $ special character identifies a string literal as an interpolated string.
                // don't write this with the $, because that will evaluate the string interpolator: Logger.Info($"Hello {Name}{Bela}", "Earth", "Moon");    
                gLogger.Info("Hello {Name}{Bela}", "Earth", "Moon");   // this should be written without the $ in front of the first string, to prevent to string interpolate it.


                //System.Console.ReadKey(); 
            }
            catch (Exception ex)
            {
                gLogger.Error(ex, "Goodbye cruel world");
            }


            Console.WriteLine(new TestCSharp80Class() { X = 5, Y = 10 }.Distance);

            var words = new string[]
                {
                                // index from start    index from end
                    "The",      // 0                   ^9
                    "quick",    // 1                   ^8
                    "brown",    // 2                   ^7
                    "fox",      // 3                   ^6
                    "jumped",   // 4                   ^5
                    "over",     // 5                   ^4
                    "the",      // 6                   ^3
                    "lazy",     // 7                   ^2
                    "dog"       // 8                   ^1
                };              // 9 (or words.Length) ^0
            var words_subArrayWithRange = words[1..^0];              // { 2, 3, 4, 5 }, The start of the range is inclusive, but the end of the range is exclusive, meaning the start is included in the range but the end is not included in the range. The range [0..^0] represents the entire range

            //string myStr = null;    // C# 8.0 feature: "Nullable references in C# 8.0". just a warning. Build will be succesful.
            string myStr = string.Empty;

            Console.WriteLine($"The last word is {words_subArrayWithRange[^1]} {myStr}"); // writes "lazy". this is C# 8.0 feature. Index + Ranges.

            using var file = new System.IO.StreamWriter("WriteLines2.txt");     // using for variable declaration (not in a scope), instead of "using (var file = new "

            System.Console.WriteLine($"CLR version, System.Environment.Version: {System.Environment.Version}");
            System.Console.WriteLine($"RuntimeInformation.FrameworkDescription: {System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription}");

            System.Console.WriteLine($"System.Environment.ProcessorCount: {System.Environment.ProcessorCount}");
            System.Console.WriteLine($"System.Environment.OSVersion: {System.Environment.OSVersion}");
            System.Console.WriteLine($"System.Environment.UserName: {System.Environment.UserName}");

            //// " using var file = new " is disposed here, when it goes out of scope
        }
    }
}
