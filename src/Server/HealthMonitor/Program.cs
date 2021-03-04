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

        static void Main(string[] args)
        {
            string appName = System.Reflection.MethodBase.GetCurrentMethod()?.ReflectedType?.Namespace ?? "UnknownNamespace";
            Console.Title = $"{appName} v1.0.14";
            string systemEnvStr = $"(v1.0.14,{Utils.RuntimeConfig() /* Debug | Release */},CLR:{System.Environment.Version},{System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription},OS:{System.Environment.OSVersion},usr:{System.Environment.UserName},CPU:{System.Environment.ProcessorCount},ThId-{Thread.CurrentThread.ManagedThreadId})";
            Console.WriteLine($"Hi {appName}.{systemEnvStr}");
            gLogger.Info($"********** Main() START {systemEnvStr}");

            string sensitiveConfigFullPath = Utils.SensitiveConfigFolderPath() + $"SqCore.Server.{appName}.NoGitHub.json";
            string systemEnvStr2 = $"Current working directory of the app: '{Directory.GetCurrentDirectory()}',{Environment.NewLine}SensitiveConfigFullPath: '{sensitiveConfigFullPath}'";
            gLogger.Info(systemEnvStr2);

            var builder = new ConfigurationBuilder()
               .SetBasePath(Directory.GetCurrentDirectory())        // GetCurrentDirectory() is the folder of the '*.csproj'.
               .AddJsonFile(sensitiveConfigFullPath, optional: true, reloadOnChange: true);
            Utils.Configuration = builder.Build();

            // HealthMonitorMessage.InitGlobals(ServerIp.HealthMonitorPublicIp, HealthMonitorMessage.DefaultHealthMonitorServerPort);       // until HealthMonitor runs on the same Server, "localhost" is OK
            Email.SenderName = Utils.Configuration["Emails:HQServer"];
            Email.SenderPwd = Utils.Configuration["Emails:HQServerPwd"];
            PhoneCall.TwilioSid = Utils.Configuration["PhoneCall:TwilioSid"];
            PhoneCall.TwilioToken = Utils.Configuration["PhoneCall:TwilioToken"];
            PhoneCall.PhoneNumbers[Caller.Gyantal] = Utils.Configuration["PhoneCall:PhoneNumberGyantal"];

            Utils.MainThreadIsExiting = new ManualResetEventSlim(false);
            StrongAssert.g_strongAssertEvent += StrongAssertMessageSendingEventHandler;
            TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException; // Occurs when a faulted task's unobserved exception is about to trigger exception which, by default, would terminate the process.

            Caretaker.gCaretaker.Init(Utils.Configuration["Emails:ServiceSupervisors"], p_needDailyMaintenance: true, TimeSpan.FromHours(2));
            //HealthMonitor.g_healthMonitor.Init();

            string? userInput = String.Empty;
            do
            {

                userInput = DisplayMenu();
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
                        //HealthMonitor.g_healthMonitor.CheckAmazonAwsInstances_Elapsed("ConsoleMenu");
                        break;
                    case "5":
                        //Console.WriteLine(HealthMonitor.g_healthMonitor.DailySummaryReport(false).ToString());
                        break;
                    case "6":
                        //HealthMonitor.g_healthMonitor.DailyReportTimer_Elapsed(null);
                        Console.WriteLine("DailyReport email was sent.");
                        break;
                }

            } while (userInput != "7" && userInput != "ConsoleIsForcedToShutDown");

            Utils.MainThreadIsExiting.Set(); // broadcast main thread shutdown
            Thread.Sleep(2000); // give 2 seconds for long running background threads to quit
            Caretaker.gCaretaker.Exit();
            //HealthMonitor.g_healthMonitor.Exit();

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
                    ToAddresses = Utils.Configuration["EmailGyantal"],
                    Subject = "SQ HealthMonitor: StrongAssert failed.",
                    Body = "SQ HealthMonitor: StrongAssert failed. " + p_msg.Message + "/" + p_msg.StackTrace,
                    IsBodyHtml = false
                }.Send();
                gLastStrongAssertEmailTime = DateTime.UtcNow;
            }
        }

        private static void TaskScheduler_UnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
        {
            gLogger.Error(e.Exception, $"TaskScheduler_UnobservedTaskException()");

            // bool isSendable = true;
            // string msg = "Exception in SqCore.Website.C#.TaskScheduler_UnobservedTaskException.";
            // if (e.Exception != null) {
            //     isSendable = SqFirewallMiddlewarePreAuthLogger.IsSendableToHealthMonitorForEmailing(e.Exception);
            //     if (isSendable)
            //         msg += $" Exception: '{ e.Exception.ToStringWithShortenedStackTrace(600)}'.";
            // }

            // if (sender != null)
            // {
            //     Task? senderTask = sender as Task;
            //     if (senderTask != null)
            //     {
            //         msg += $" Sender is a task. TaskId: {senderTask.Id}, IsCompleted: {senderTask.IsCompleted}, IsCanceled: {senderTask.IsCanceled}, IsFaulted: {senderTask.IsFaulted}, TaskToString(): {senderTask.ToString()}.";
            //         msg += (senderTask.Exception == null) ? " SenderTask.Exception is null" : $" SenderTask.Exception {senderTask.Exception.ToStringWithShortenedStackTrace(800)}";
            //     }
            //     else
            //         msg += " Sender is not a task.";
            // }

            // if (isSendable)
            //     HealthMonitorMessage.SendAsync(msg, HealthMonitorMessageID.SqCoreWebCsError).TurnAsyncToSyncTask();
            // else 
            //     gLogger.Warn(msg);
            // e.SetObserved();        //  preventing it from triggering exception escalation policy which, by default, terminates the process.
        }

        static bool gHasBeenCalled = false;
        static public string? DisplayMenu()
        {
            if (gHasBeenCalled)
            {
                Console.WriteLine();
            }
            gHasBeenCalled = true;

            ColorConsole.WriteLine(ConsoleColor.Magenta, "----  (type and press Enter)  ----");
            Console.WriteLine("1. Say Hello. Don't do anything. Check responsivenes.");
            Console.WriteLine("2. Crash App intentionaly (for simulation purposes).");
            Console.WriteLine("3. Test Twilio phone call service.");
            Console.WriteLine("4. Test AmazonAWS API:DescribeInstances()");
            Console.WriteLine("5. VirtualBroker Report: show on Console.");
            Console.WriteLine("6. VirtualBroker Report: send Html email.");
            Console.WriteLine("7. Exit gracefully (Avoid Ctrl-^C).");
            string? result = null;
            try
            {
                result = Console.ReadLine();
            }
            catch (System.IO.IOException e) // on Linux, of somebody closes the Terminal Window, Console.Readline() will throw an Exception with Message "Input/output error"
            {
                Utils.Logger.Info($"Console.ReadLine() exception. Somebody closes the Terminal Window: {e.Message}");
                return "ConsoleIsForcedToShutDown";
            }
            return result;
            //return Convert.ToInt32(result);
        }

        static public void TestPhoneCall()
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
                bool didTwilioAcceptedTheCommand = call.MakeTheCall();
                if (didTwilioAcceptedTheCommand)
                {
                    Utils.Logger.Debug("PhoneCall instruction was sent to Twilio.");
                }
                else
                    Utils.Logger.Error("PhoneCall instruction was NOT accepted by Twilio.");
            } catch (Exception e)
            {
                Utils.Logger.Error(e, "Exception in TestPhoneCall().");
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
            ThreadPool.QueueUserWorkItem(new WaitCallback(ignored =>
            {
                throw new Exception();
            }));

        }
    }
}
