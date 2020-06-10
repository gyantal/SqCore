using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Globalization;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Https;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NLog.Web;
using SqCommon;
using DbCommon;
using FinTechCommon;
using StackExchange.Redis;

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
        DateTime m_webAppStartTime = DateTime.UtcNow;
        DateTime IWebAppGlobals.WebAppStartTime { get => m_webAppStartTime; set => m_webAppStartTime = value; }

        IWebHostEnvironment? m_kestrelEnv = null;
        IWebHostEnvironment? IWebAppGlobals.KestrelEnv { get => m_kestrelEnv; set => m_kestrelEnv = value; }
        
        Queue<HttpRequestLog> m_httpRequestLogs = new Queue<HttpRequestLog>();
        Queue<HttpRequestLog> IWebAppGlobals.HttpRequestLogs { get => m_httpRequestLogs; set => m_httpRequestLogs = value; }
    }

    public class Program
    {
        public static IWebAppGlobals g_webAppGlobals { get; set; } = new WebAppGlobals();
        private static readonly NLog.Logger gLogger = NLog.LogManager.GetLogger("Program");   // the name of the logger will be not the "Namespace.Class", but whatever you prefer: "Program"
        //public static IConfigurationRoot gConfiguration = new ConfigurationBuilder().Build();
        public static void Main(string[] args)
        {
            string appName = System.Reflection.MethodBase.GetCurrentMethod()?.ReflectedType?.Namespace ?? "UnknownNamespace";
            Console.Title = $"{appName} v1.0.15";
            string systemEnvStr = $"(v1.0.15, {Utils.RuntimeConfig() /* Debug | Release */}, CLR: {System.Environment.Version}, {System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription},  OS: {System.Environment.OSVersion}, user: {System.Environment.UserName}, CPU: {System.Environment.ProcessorCount}, ThId-{Thread.CurrentThread.ManagedThreadId})";
            Console.WriteLine($"Hello {appName}. {systemEnvStr}");
            gLogger.Info($"********** Main() START {systemEnvStr}");

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
            HealthMonitorMessage.InitGlobals(ServerIp.HealthMonitorPublicIp, HealthMonitorMessage.DefaultHealthMonitorServerPort);       // until HealthMonitor runs on the same Server, "localhost" is OK
            Email.SenderName = Utils.Configuration["Emails:HQServer"];
            Email.SenderPwd = Utils.Configuration["Emails:HQServerPwd"];
            StrongAssert.g_strongAssertEvent += StrongAssertMessageSendingEventHandler;
            TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException; // Occurs when a faulted task's unobserved exception is about to trigger exception which, by default, would terminate the process.

            try
            {
                DashboardPushHub.EarlyInit();    // services add handlers to the MemDb.EvMemDbInitialized event.

                var redisConnString = (Utils.RunningPlatform() == Platform.Windows) ? Utils.Configuration["ConnectionStrings:RedisDefault"] : Utils.Configuration["ConnectionStrings:RedisLinuxLocalhost"];
                IDatabase redisDb = RedisManager.GetDb(redisConnString, 0);

                MemDb.gMemDb.Init(redisDb);
                Caretaker.gCaretaker.Init(Utils.Configuration["Emails:ServiceSupervisors"], p_needDailyMaintenance: true, TimeSpan.FromHours(2));
                CreateHostBuilder(args).Build().Run();
            }
            catch (Exception e)
            {
                gLogger.Error(e, $"CreateHostBuilder(args).Build().Run() exception.");
                if (e is System.Net.Sockets.SocketException)
                {
                    gLogger.Error("Linux. See 'Allow non-root process to bind to port under 1024.txt'. If Dotnet.exe was updated, it lost privilaged port. Try 'whereis dotnet','sudo setcap 'cap_net_bind_service=+ep' /usr/share/dotnet/dotnet'.");
                }
                HealthMonitorMessage.SendAsync($"Exception in SqCoreWebsite.C#.MainThread. Exception: '{ e.ToStringWithShortenedStackTrace(1200)}'", HealthMonitorMessageID.SqCoreWebCsError).GetAwaiter().GetResult();
            }
            Caretaker.gCaretaker.Exit();
            MemDb.gMemDb.Exit();

            gLogger.Info("****** Main() END");
            NLog.LogManager.Shutdown();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.ConfigureKestrel(serverOptions =>
                    {
                        // Safe to leave ports 5000, 5001 on IPAddress.Loopback (localhost), because localhost can be accessed only from local machine. From the public web, the ports 5000, 5001 is not accessable.
                        serverOptions.Listen(IPAddress.Loopback, 5000); // In IPv4, 127.0.0.1 is the most commonly used loopback address, in IP6, it is [::1],  "localhost" means either 127.0.0.1 or  [::1] 
                        serverOptions.Listen(IPAddress.Loopback, 5001, listenOptions =>  // On Linux server: only 'localhost:5001' is opened, but '<PublicIP>:5001>' is not. We would need PublicAny for that. But for security, it is fine.
                        {
                            //listenOptions.UseHttps("testCert.pfx", "testPassword");
                            listenOptions.UseHttps();   // Configure Kestrel to use HTTPS with the default certificate. Throws an exception if no default certificate is configured.
                        });

                        // from the public web only port 443 is accessable. However, on that port, both HTTP and HTTPS traffic is allowed. Although we redirect HTTP to HTTPS later.
                        serverOptions.ListenAnyIP(443, listenOptions =>    // Both 'localhost:443' and '<PublicIP>:443>' is listened on Linux server.
                        {
                            string sensitiveConfigFullPath = Utils.SensitiveConfigFolderPath() + $"sqcore.net.merged_pubCert_privKey.pfx";
                            Console.WriteLine($"Pfx file: " + sensitiveConfigFullPath);
                            listenOptions.UseHttps(sensitiveConfigFullPath, @"haha");

                            // I don't actually need all of these. Because the wildcart cert is both root and subdomain 'checked by 'certbot certificates''. So, don't need branching here based on context.
                            // from here https://docs.microsoft.com/en-us/aspnet/core/fundamentals/servers/kestrel?view=aspnetcore-3.0#endpoint-configuration  (find: SNI)
                            // listenOptions.UseHttps(httpsOptions =>
                            // {

                            //     // see 'certmgr.msc'
                            //     // https://localhost:5005/ with this turns out to be 'valid' in Chrome. Cert is issued by 'localhost', issued to 'localhost'. 
                            //     // https://127.0.0.1:5005/ will say: invalid. (as the 'name' param is null in the callback down)
                            //     var localhostCert = CertificateLoader.LoadFromStoreCert("localhost", "My", StoreLocation.CurrentUser, allowInvalid: true);  // that is the local machine certificate store

                            //     X509Certificate2 letsEncryptCert = new X509Certificate2(@"g:\agy\myknowledge\programming\_ASP.NET\https cert\letsencrypt Folder from Ubuntu\letsencrypt\live\sqcore.net\merged_pubCert_privKey_pwd_haha.pfx", @"haha", X509KeyStorageFlags.Exportable);
                            //     //var exampleCert = CertificateLoader.LoadFromStoreCert("example.com", "My", StoreLocation.CurrentUser, allowInvalid: true);
                            //     //var subExampleCert = CertificateLoader.LoadFromStoreCert("sub.example.com", "My", StoreLocation.CurrentUser, allowInvalid: true);

                            //     var certs = new Dictionary<string, X509Certificate2>(StringComparer.OrdinalIgnoreCase);
                            //     certs["localhost"] = localhostCert;
                            //     certs["sqcore.net"] = letsEncryptCert;
                            //     certs["dashboard.sqcore.net"] = letsEncryptCert;  // it seems the same certificate is used for the root and the sub-domain.
                            //     //certs["example.com"] = exampleCert;
                            //     //certs["sub.example.com"] = subExampleCert;

                            //     httpsOptions.ServerCertificateSelector = (connectionContext, name) =>
                            //     {
                            //         if (name != null && certs.TryGetValue(name, out var cert))
                            //         {
                            //             return cert;
                            //         }

                            //         return localhostCert;
                            //         //return exampleCert;
                            //     };
                            // }); // UseHttps()

                        });
                    })
                    .UseStartup<Startup>()
                    .ConfigureLogging(logging =>
                    {
                        // for very detailed logging:
                        // set "Microsoft": "Trace" in appsettings.json or appsettings.dev.json
                        // set set this ASP logging.SetMinimumLevel to Trace, 
                        // set minlevel="Trace" in NLog.config
                        logging.ClearProviders();   // this deletes the Console logger which is a default in ASP.net
                        if (String.Equals(Utils.RuntimeConfig(), "DEBUG", StringComparison.OrdinalIgnoreCase)) 
                        {
                            logging.AddConsole();   // in vscode at F5, launching a web browser works by finding a pattern in Console
                            logging.SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Trace);
                        } else 
                        {
                            // in production, logging slows down.
                            logging.SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Warning);
                        }
                    })
                    .UseNLog();  // NLog: Setup NLog for Dependency injection; LoggerProvider under the ASP.NET Core platform.
                });

        internal static void StrongAssertMessageSendingEventHandler(StrongAssertMessage p_msg)
        {
            gLogger.Info("StrongAssertEmailSendingEventHandler()");
            HealthMonitorMessage.SendAsync($"Msg from SqCore.Website.C#.StrongAssert. StrongAssert Warning (if Severity is NoException, it is just a mild Warning. If Severity is ThrowException, that exception triggers a separate message to HealthMonitor as an Error). Severity: {p_msg.Severity}, Message: { p_msg.Message}, StackTrace: { p_msg.StackTrace.ToStringWithShortenedStackTrace(800)}", HealthMonitorMessageID.SqCoreWebCsError).FireParallelAndForgetAndLogErrorTask();
        }

        // Called by the GC.FinalizerThread. Occurs when a faulted task's unobserved exception is about to trigger exception which, by default, would terminate the process.
        private static void TaskScheduler_UnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
        {
            gLogger.Error(e.Exception, $"TaskScheduler_UnobservedTaskException()");

            bool isSendable = true;
            string msg = "Exception in SqCore.Website.C#.TaskScheduler_UnobservedTaskException.";
            if (e.Exception != null) {
                isSendable = SqFirewallMiddlewarePreAuthLogger.IsSendableToHealthMonitorForEmailing(e.Exception);
                if (isSendable)
                    msg += $" Exception: '{ e.Exception.ToStringWithShortenedStackTrace(600)}'.";
            }

            if (sender != null)
            {
                Task? senderTask = sender as Task;
                if (senderTask != null)
                {
                    msg += $" Sender is a task. TaskId: {senderTask.Id}, IsCompleted: {senderTask.IsCompleted}, IsCanceled: {senderTask.IsCanceled}, IsFaulted: {senderTask.IsFaulted}, TaskToString(): {senderTask.ToString()}.";
                    msg += (senderTask.Exception == null) ? " SenderTask.Exception is null" : $" SenderTask.Exception {senderTask.Exception.ToStringWithShortenedStackTrace(800)}";
                }
                else
                    msg += " Sender is not a task.";
            }

            if (isSendable)
                HealthMonitorMessage.SendAsync(msg, HealthMonitorMessageID.SqCoreWebCsError).GetAwaiter().GetResult();
            else 
                gLogger.Warn(msg);
            e.SetObserved();        //  preventing it from triggering exception escalation policy which, by default, terminates the process.
        }

        public static void ServerDiagnostic(StringBuilder p_sb)
        {
            p_sb.Append("<H2>Program.exe</H2>");
            var timeSinceAppStart = DateTime.UtcNow - g_webAppGlobals.WebAppStartTime;
            p_sb.Append($"WebAppStartTimeUtc: {g_webAppGlobals.WebAppStartTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)}({timeSinceAppStart:dd} days {timeSinceAppStart:hh\\:mm} hours ago)<br>");
        }

    }
}
