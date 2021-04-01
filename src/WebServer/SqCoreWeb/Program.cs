using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using SqCommon;
using DbCommon;
using FinTechCommon;

namespace SqCoreWeb
{
    public interface IWebAppGlobals
    {
        DateTime WebAppStartTime { get; set; }
        IWebHostEnvironment? KestrelEnv { get; set; }   // instead of the 3 member variables separately, store the container p_env
        Queue<HttpRequestLog> HttpRequestLogs { get; set; }     // Fast Insert, limited size. Better that List
    }

    public class WebAppGlobals : IWebAppGlobals
    {
        DateTime IWebAppGlobals.WebAppStartTime { get; set; } = DateTime.UtcNow;
        // KestrelEnv.ContentRootPath: "...\SqCore\src\WebServer\SqCoreWeb"
        // KestrelEnv.WebRootPath: 	 "...\SqCore\src\WebServer\SqCoreWeb\wwwroot"
        IWebHostEnvironment? IWebAppGlobals.KestrelEnv { get; set; }
        Queue<HttpRequestLog> IWebAppGlobals.HttpRequestLogs { get; set; } = new Queue<HttpRequestLog>();
    }

    public partial class Program
    {
        public static IWebAppGlobals g_webAppGlobals { get; set; } = new WebAppGlobals();
        private static readonly NLog.Logger gLogger = NLog.LogManager.GetLogger("Program");   // the name of the logger will be not the "Namespace.Class", but whatever you prefer: "Program"

        static Timer? gHeartbeatTimer = null; // If timer object goes out of scope and gets erased by Garbage Collector after some time, which stops callbacks from firing. Save reference to it in a member of class.
        static long gNheartbeat = 0;
        const int cHeartbeatTimerFrequencyMinutes = 10;

        public static void Main(string[] args)
        {
            string appName = System.Reflection.MethodBase.GetCurrentMethod()?.ReflectedType?.Namespace ?? "UnknownNamespace";
            string systemEnvStr = $"(v1.0.15,{Utils.RuntimeConfig() /* Debug | Release */},CLR:{System.Environment.Version},{System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription},OS:{System.Environment.OSVersion},usr:{System.Environment.UserName},CPU:{System.Environment.ProcessorCount},ThId-{Thread.CurrentThread.ManagedThreadId})";
            Console.WriteLine($"Hi {appName}.{systemEnvStr}");
            gLogger.Info($"********** Main() START {systemEnvStr}");
            // Setting Console.Title
            // on Linux use it only in GUI mode. It works with graphical Xterm in VNC, but with 'screen' or with Putty it is buggy and after this, the next 200 characters are not written to console.
            // Future work if needed: bring a flag to use it in string[] args, but by default, don't set Title on Linux 
            if (Utils.RunningPlatform() != Platform.Linux) // https://stackoverflow.com/questions/47059468/get-or-set-the-console-title-in-linux-and-macosx-with-net-core
                Console.Title = $"{appName} v1.0.15"; // "SqCoreWeb v1.0.15"

            gHeartbeatTimer = new System.Threading.Timer((e) =>    // Heartbeat log is useful to find out when VM was shut down, or when the App crashed
            {
                Utils.Logger.Info($"**g_nHeartbeat: {gNheartbeat} (at every {cHeartbeatTimerFrequencyMinutes} minutes)");
                gNheartbeat++;
            }, null, TimeSpan.FromMinutes(0.5), TimeSpan.FromMinutes(cHeartbeatTimerFrequencyMinutes));

            string sensitiveConfigFullPath = Utils.SensitiveConfigFolderPath() + $"SqCore.WebServer.{appName}.NoGitHub.json";
            string systemEnvStr2 = $"Current working directory of the app: '{Directory.GetCurrentDirectory()}',{Environment.NewLine}SensitiveConfigFullPath: '{sensitiveConfigFullPath}'";
            gLogger.Info(systemEnvStr2);

            var builder = new ConfigurationBuilder()
               .SetBasePath(Directory.GetCurrentDirectory())        // GetCurrentDirectory() is the folder of the '*.csproj'.
               .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)      // no need to copy appsettings.json to the sub-directory of the EXE. 
               .AddJsonFile(sensitiveConfigFullPath, optional: true, reloadOnChange: true);
            //.AddUserSecrets<Program>()    // Used mostly in Development only, not in Production. Stored in a JSON configuration file in a system-protected user profile folder on the local machine. (e.g. user's %APPDATA%\Microsoft\UserSecrets\), the secret values aren't encrypted, but could be in the future.
            // do we need it?: No. Sensitive files are in separate folders, not up on GitHub. If server is not hacked, we don't care if somebody who runs the code can read the settings file. Also, scrambling secret file makes it more difficult to change it realtime.
            //.AddEnvironmentVariables();   // not needed in general. We dont' want to clutter op.sys. environment variables with app specific values.
            Utils.Configuration = builder.Build();
            Utils.MainThreadIsExiting = new ManualResetEventSlim(false);
            // HealthMonitorMessage.InitGlobals(ServerIp.HealthMonitorPublicIp, ServerIp.DefaultHealthMonitorServerPort);       // until HealthMonitor runs on the same Server, "localhost" is OK

            Email.SenderName = Utils.Configuration["Emails:HQServer"];
            Email.SenderPwd = Utils.Configuration["Emails:HQServerPwd"];
            PhoneCall.TwilioSid = Utils.Configuration["PhoneCall:TwilioSid"];
            PhoneCall.TwilioToken = Utils.Configuration["PhoneCall:TwilioToken"];
            PhoneCall.PhoneNumbers[Caller.Gyantal] = Utils.Configuration["PhoneCall:PhoneNumberGyantal"];
            PhoneCall.PhoneNumbers[Caller.Charmat0] = Utils.Configuration["PhoneCall:PhoneNumberCharmat0"];

            StrongAssert.g_strongAssertEvent += StrongAssertMessageSendingEventHandler;
            TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException; // Occurs when a faulted task's unobserved exception is about to trigger exception which, by default, would terminate the process.

            try
            {
                // 1. PreInit services. They might add callbacks to MemDb's events.
                DashboardClient.PreInit();    // services add handlers to the MemDb.EvMemDbInitialized event.

                // 2. Init services
                var redisConnString = (Utils.RunningPlatform() == Platform.Windows) ? Utils.Configuration["ConnectionStrings:RedisDefault"] : Utils.Configuration["ConnectionStrings:RedisLinuxLocalhost"];
                var redisDb = RedisManager.GetDb(redisConnString, 0);   // lowest level DB module
                var db = new Db(redisDb, null);   // mid-level DB wrapper above low-level DB
                MemDb.gMemDb.Init(db); // high level DB used by functionalities

                Caretaker.gCaretaker.Init(Utils.Configuration["Emails:ServiceSupervisors"], p_needDailyMaintenance: true, TimeSpan.FromHours(2));
                SqTaskScheduler.gTaskScheduler.Init();

                Services_Init();

                // 3. Run services.
                // Create a dedicated thread for a single task that is running for the lifetime of my application.
                KestrelWebServer_Run(args);

                string userInput = string.Empty;
                do
                {
                    userInput = DisplayMenuAndExecute();
                } while (userInput != "UserChosenExit" && userInput != "ConsoleIsForcedToShutDown");
            }
            catch (Exception e)
            {
                gLogger.Error(e, $"CreateHostBuilder(args).Build().Run() exception.");
                if (e is System.Net.Sockets.SocketException)
                {
                    gLogger.Error("Linux. See 'Allow non-root process to bind to port under 1024.txt'. If Dotnet.exe was updated, it lost privilaged port. Try 'whereis dotnet','sudo setcap 'cap_net_bind_service=+ep' /usr/share/dotnet/dotnet'.");
                }
                HealthMonitorMessage.SendAsync($"Exception in SqCoreWebsite.C#.MainThread. Exception: '{ e.ToStringWithShortenedStackTrace(1200)}'", HealthMonitorMessageID.SqCoreWebCsError).TurnAsyncToSyncTask();
            }

            Utils.MainThreadIsExiting.Set(); // broadcast main thread shutdown
            // 4. Try to gracefully stop services.
            KestrelWebServer_Stop();

            int timeBeforeExitingSec = 2;
            Console.WriteLine($"Exiting in {timeBeforeExitingSec}sec...");
            Thread.Sleep(TimeSpan.FromSeconds(timeBeforeExitingSec)); // give some seconds for long running background threads to quit

             // 5. Dispose service resources
            KestrelWebServer_Exit();
            Services_Exit();

            SqTaskScheduler.gTaskScheduler.Exit();
            Caretaker.gCaretaker.Exit();
            MemDb.gMemDb.Exit();

            gLogger.Info("****** Main() END");
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
            Console.WriteLine("2. Show next schedule times (only earliest trigger)");
            Console.WriteLine("3. Elapse Task: Overmind, Trigger1-MorningCheck");
            Console.WriteLine("4. Elapse Task: Overmind, Trigger2-MiddayCheck");
            Console.WriteLine("5. Elapse Task: WebsitesMonitor, Crawl SpIndexChanges");
            Console.WriteLine("6. Elapse Task: VBroker-HarryLong (First Simulation)");
            Console.WriteLine("7. Elapse Task: VBroker-UberVxx (First Simulation)");
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
                    Console.WriteLine(SqTaskScheduler.gTaskScheduler.PrintNextScheduleTimes(false).ToString());
                    break;
                case "3":
                    SqTaskScheduler.gTaskScheduler.TestElapseTrigger("Overmind", 0);
                    break;
                case "4":
                    SqTaskScheduler.gTaskScheduler.TestElapseTrigger("Overmind", 1);
                    break;
                case "5":
                    SqTaskScheduler.gTaskScheduler.TestElapseTrigger("WebsitesMonitor", 0);
                    break;
                case "9":
                    return "UserChosenExit";
            }
            return string.Empty;
        }

        internal static void StrongAssertMessageSendingEventHandler(StrongAssertMessage p_msg)
        {
            gLogger.Info("StrongAssertEmailSendingEventHandler()");
            HealthMonitorMessage.SendAsync($"Msg from SqCore.Website.C#.StrongAssert. StrongAssert Warning (if Severity is NoException, it is just a mild Warning. If Severity is ThrowException, that exception triggers a separate message to HealthMonitor as an Error). Severity: {p_msg.Severity}, Message: { p_msg.Message}, StackTrace: { p_msg.StackTrace.ToStringWithShortenedStackTrace(800)}", HealthMonitorMessageID.SqCoreWebCsError).FireParallelAndForgetAndLogErrorTask();
        }

        // Called by the GC.FinalizerThread. Occurs when a faulted task's unobserved exception is about to trigger exception which, by default, would terminate the process.
        private static void TaskScheduler_UnobservedTaskException(object? p_sender, UnobservedTaskExceptionEventArgs p_e)
        {
            gLogger.Error(p_e.Exception, $"TaskScheduler_UnobservedTaskException()");

            string msg = "Exception in SqCore.Website.C#.TaskScheduler_UnobservedTaskException.";
            bool isSendable = true;
            if (p_e.Exception != null) {
                isSendable = SqFirewallMiddlewarePreAuthLogger.IsSendableToHealthMonitorForEmailing(p_e.Exception);
                if (isSendable)
                    msg += $" Exception: '{ p_e.Exception.ToStringWithShortenedStackTrace(600)}'.";
            }

            if (p_sender != null)
            {
                Task? senderTask = p_sender as Task;
                if (senderTask != null)
                {
                    msg += $" Sender is a task. TaskId: {senderTask.Id}, IsCompleted: {senderTask.IsCompleted}, IsCanceled: {senderTask.IsCanceled}, IsFaulted: {senderTask.IsFaulted}, TaskToString(): {senderTask.ToString()}.";
                    msg += (senderTask.Exception == null) ? " SenderTask.Exception is null" : $" SenderTask.Exception {senderTask.Exception.ToStringWithShortenedStackTrace(800)}";
                }
                else
                    msg += " Sender is not a task.";
            }

            if (isSendable)
                HealthMonitorMessage.SendAsync(msg, HealthMonitorMessageID.SqCoreWebCsError).TurnAsyncToSyncTask();
            else 
                gLogger.Warn(msg);
            p_e.SetObserved();        //  preventing it from triggering exception escalation policy which, by default, terminates the process.
        }

        public static void ServerDiagnostic(StringBuilder p_sb)
        {
            p_sb.Append("<H2>Program.exe</H2>");
            var timeSinceAppStart = DateTime.UtcNow - g_webAppGlobals.WebAppStartTime;
            p_sb.Append($"WebAppStartTimeUtc: {g_webAppGlobals.WebAppStartTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)}({timeSinceAppStart:dd} days {timeSinceAppStart:hh\\:mm} hours ago)<br>");
        }

    }
}
