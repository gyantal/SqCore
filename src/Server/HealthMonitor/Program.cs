using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using SqCommon;

namespace HealthMonitor
{
    class Program
    {
        private static readonly NLog.Logger gLogger = NLog.LogManager.GetLogger("Program");   // the name of the logger will be not the "Namespace.Class", but whatever you prefer: "Program"
        static Timer? gHeartbeatTimer = null; // If timer object goes out of scope and gets erased by Garbage Collector after some time, which stops callbacks from firing. Save reference to it in a member of class.
        static long gNheartbeat = 0;
        const int cHeartbeatTimerFrequencyMinutes = 5;
        static void Main(string[] args)
        {
            string appName = System.Reflection.MethodBase.GetCurrentMethod()?.ReflectedType?.Namespace ?? "UnknownNamespace";
            string systemEnvStr = $"(v1.0.14,{Utils.RuntimeConfig() /* Debug | Release */},CLR:{System.Environment.Version},{System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription},OS:{System.Environment.OSVersion},usr:{System.Environment.UserName},CPU:{System.Environment.ProcessorCount},ThId-{Thread.CurrentThread.ManagedThreadId})";
            Console.WriteLine($"Hi {appName}.{systemEnvStr}");
            gLogger.Info($"********** Main() START {systemEnvStr}");
            if (Utils.RunningPlatform() != Platform.Linux) // https://stackoverflow.com/questions/47059468/get-or-set-the-console-title-in-linux-and-macosx-with-net-core
                Console.Title = $"{appName} v1.0.15"; // "SqCoreWeb v1.0.15", but on Linux use it only in GUI mode. It works with graphical Xterm in VNC, but with 'screen' or with Putty it is buggy and after this, the next 200 characters are not written to console. T

            gHeartbeatTimer = new System.Threading.Timer((e) =>    // Heartbeat log is useful to find out when VM was shut down, or when the App crashed
            {
                Utils.Logger.Info($"**g_nHeartbeat: {gNheartbeat} (at every {cHeartbeatTimerFrequencyMinutes} minutes)");
                gNheartbeat++;
            }, null, TimeSpan.FromMinutes(0.5), TimeSpan.FromMinutes(cHeartbeatTimerFrequencyMinutes));

            string sensitiveConfigFullPath = Utils.SensitiveConfigFolderPath() + $"SqCore.Server.{appName}.NoGitHub.json";
            string systemEnvStr2 = $"Current working directory of the app: '{Directory.GetCurrentDirectory()}',{Environment.NewLine}SensitiveConfigFullPath: '{sensitiveConfigFullPath}'";
            gLogger.Info(systemEnvStr2);

            var builder = new ConfigurationBuilder()
               .SetBasePath(Directory.GetCurrentDirectory())        // GetCurrentDirectory() is the folder of the '*.csproj'.
               .AddJsonFile(sensitiveConfigFullPath, optional: true, reloadOnChange: true);
            Utils.Configuration = builder.Build();

            Email.SenderName = Utils.Configuration["Emails:HQServer"];
            Email.SenderPwd = Utils.Configuration["Emails:HQServerPwd"];
            PhoneCall.TwilioSid = Utils.Configuration["PhoneCall:TwilioSid"];
            PhoneCall.TwilioToken = Utils.Configuration["PhoneCall:TwilioToken"];
            PhoneCall.PhoneNumbers[Caller.Gyantal] = Utils.Configuration["PhoneCall:PhoneNumberGyantal"];

            Utils.MainThreadIsExiting = new ManualResetEventSlim(false);
            StrongAssert.G_strongAssertEvent += StrongAssertMessageSendingEventHandler;
            AppDomain.CurrentDomain.UnhandledException += new UnhandledExceptionEventHandler(AppDomain_BckgThrds_UnhandledException);
            TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException; // Occurs when a faulted task's unobserved exception is about to trigger exception which, by default, would terminate the process.

            Caretaker.g_caretaker.Init("HealthMonitor", Utils.Configuration["Emails:ServiceSupervisors"], p_needDailyMaintenance: true, TimeSpan.FromHours(2));
            SqTaskScheduler.gTaskScheduler.Init();
            HealthMonitor.g_healthMonitor.Init();

            string userInput = string.Empty;
            do
            {
                userInput = DisplayMenuAndExecute();
            } while (userInput != "UserChosenExit" && userInput != "ConsoleIsForcedToShutDown");

            Utils.MainThreadIsExiting.Set(); // broadcast main thread shutdown
            gHeartbeatTimer.Dispose();

            // Try to gracefully stop services.
            int timeBeforeExitingSec = 2;
            Console.WriteLine($"Exiting in {timeBeforeExitingSec}sec...");
            Thread.Sleep(TimeSpan.FromSeconds(timeBeforeExitingSec)); // give some seconds for long running background threads to quit

            HealthMonitor.g_healthMonitor.Exit();
            SqTaskScheduler.gTaskScheduler.Exit();
            Caretaker.g_caretaker.Exit();

            gLogger.Info("****** Main() END");
            NLog.LogManager.Shutdown();
        }

        static DateTime gLastStrongAssertEmailTime = DateTime.MinValue;
        internal static void StrongAssertMessageSendingEventHandler(StrongAssertMessage p_msg)
        {
            Utils.Logger.Info("StrongAssertEmailSendingEventHandler()");
            if ((DateTime.UtcNow - gLastStrongAssertEmailTime).TotalMinutes > 30)   // don't send it in every minute, just after 30 minutes
            {
                new Email
                {
                    ToAddresses = Utils.Configuration["Emails:Gyant"],
                    Subject = "SQ HealthMonitor: StrongAssert failed.",
                    Body = "SQ HealthMonitor: StrongAssert failed. " + p_msg.Message + "/" + p_msg.StackTrace,
                    IsBodyHtml = false
                }.Send();
                gLastStrongAssertEmailTime = DateTime.UtcNow;
            }
        }

        private static void AppDomain_BckgThrds_UnhandledException(object p_sender, UnhandledExceptionEventArgs p_e)
        {
            Exception exception = (p_e.ExceptionObject as Exception) ?? new SqException($"Unhandled exception doesn't derive from System.Exception: {p_e.ToString() ?? "Null ExceptionObject"}");
            Utils.Logger.Error(exception, $"AppDomain_BckgThrds_UnhandledException(). Terminating '{p_e?.IsTerminating.ToString() ?? "Null ExceptionObject"}'. Exception: '{ exception.ToStringWithShortenedStackTrace(1600)}'");

            // isSendable check is not required. This background thread crash will terminate the main app. We should surely notify HealthMonitor.
            string msg = $"App 'HealthMonitor' is terminated because exception in background thread. C#.AppDomain_BckgThrds_UnhandledException(). See log files.";
            new Email   // no need to check that we don't spam emails, because it will terminate the app anyway
            {
                ToAddresses = Utils.Configuration["Emails:Gyant"],
                Subject = "SQ HealthMonitor: AppDomain_BckgThrds_UnhandledException in HealthMonitor.",
                Body = "SQ HealthMonitor: AppDomain_BckgThrds_UnhandledException in HealthMonitor. " + msg,
                IsBodyHtml = false
            }.Send();
        }

        static DateTime gLastUnobservedTaskExceptionEmailTime = DateTime.MinValue;
        private static void TaskScheduler_UnobservedTaskException(object? p_sender, UnobservedTaskExceptionEventArgs p_e)
        {
            gLogger.Error(p_e.Exception, $"TaskScheduler_UnobservedTaskException()");

            string msg = $"Exception in SqCore.Server.HealthMonitor.C#.TaskScheduler_UnobservedTaskException. Exception: '{ p_e.Exception.ToStringWithShortenedStackTrace(1600)}'. ";
            msg += Utils.TaskScheduler_UnobservedTaskExceptionMsg(p_sender, p_e);
            gLogger.Warn(msg);
            p_e.SetObserved();        //  preventing it from triggering exception escalation policy which, by default, terminates the process.

            if ((DateTime.UtcNow - gLastUnobservedTaskExceptionEmailTime).TotalMinutes > 30)   // don't send it in every minute, just after 30 minutes
            {
                new Email
                {
                    ToAddresses = Utils.Configuration["Emails:Gyant"],
                    Subject = "SQ HealthMonitor: UnobservedTaskException in HealthMonitor.",
                    Body = "SQ HealthMonitor: UnobservedTaskException in HealthMonitor. " + msg,
                    IsBodyHtml = false
                }.Send();
                gLastUnobservedTaskExceptionEmailTime = DateTime.UtcNow;
            }
        }

        static bool gIsFirstCall = true;
        static public string DisplayMenuAndExecute()
        {
            if (!gIsFirstCall)
                Console.WriteLine();
            gIsFirstCall = false;

            ColorConsole.WriteLine(ConsoleColor.Magenta, "----  (type and press Enter)  ----");
            Console.WriteLine("1. Say Hello. Don't do anything. Check responsivenes.");
            Console.WriteLine("2. Crash App intentionaly (for simulation purposes).");
            Console.WriteLine("3. Test Twilio phone call service.");
            Console.WriteLine("4. Test AmazonAWS API:DescribeInstances()");
            Console.WriteLine("5. VirtualBroker Report: show on Console.");
            Console.WriteLine("6. VirtualBroker Report: send Html email.");
            Console.WriteLine("7. Exit gracefully (Avoid Ctrl-^C).");
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
                    Utils.Logger.Info("Hello. I am not crashed yet! :)");
                    break;
                case "2":
                    TestIntentionalCrash();
                    break;
                case "3":
                    TestPhoneCall();
                    break;
                case "4":
                    HealthMonitor.g_healthMonitor.CheckAmazonAwsInstances_Elapsed("ConsoleMenu");
                    break;
                case "5":
                    Console.WriteLine(HealthMonitor.g_healthMonitor.DailySummaryReport(false).ToString());
                    break;
                case "6":
                    HealthMonitor.g_healthMonitor.DailyReportTimer_Elapsed(null);
                    Console.WriteLine("DailyReport email was sent.");
                    break;
                case "7":
                    return "UserChosenExit";
            }
            return string.Empty;
        }

        public static async void TestPhoneCall()
        {
            Console.WriteLine("Calling phone number via Twilio. It should ring out.");
            Utils.Logger.Info("Calling phone number via Twilio. It should ring out.");

            try
            {
                var call = new PhoneCall
                {
                    FromNumber = Caller.Gyantal,
                    ToNumber = PhoneCall.PhoneNumbers[Caller.Gyantal],
                    Message = "This is a test phone call from Health Monitor.",
                    NRepeatAll = 2
                };
                // skipped temporarily
                bool didTwilioAcceptedTheCommand = await call. MakeTheCallAsync();
                if (didTwilioAcceptedTheCommand)
                {
                    Utils.Logger.Debug("TestPhoneCall(): PhoneCall instruction was sent to Twilio.");
                }
                else
                    Utils.Logger.Error("TestPhoneCall(): PhoneCall instruction was NOT accepted by Twilio.");
            }
            catch (Exception e)
            {
                Utils.Logger.Error(e, "TestPhoneCall(): Exception in TestPhoneCall().");
            }
        }

        static public void TestIntentionalCrash()
        {
            Console.Write("Test Intentional Crash. Are you sure (y/n)? ");
            Utils.Logger.Info("Test Intentional Crash. Are you sure (y/n)?");

            string? result = null;
            try
            {
                result = Console.ReadLine();
            }
            catch (System.IO.IOException e) // on Linux, of somebody closes the Terminal Window, Console.Readline() will throw an Exception with Message "Input/output error"
            {
                Utils.Logger.Info($"Console.ReadLine() exception. Somebody closes the Terminal Window: {e.Message}");
            }

            if (result?.ToLower() != "y")
                return;

            // https://stackoverflow.com/questions/17996738/how-to-make-c-sharp-application-crash
            // All the others can be handled by the top level ApplicationDomain.OnUnhandledException and the like.
            ThreadPool.QueueUserWorkItem(ignored =>
            {
                throw new Exception();
            });

        }
    }
}
