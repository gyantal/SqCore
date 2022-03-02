using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using SqCommon;
using FinTechCommon;
using BrokerCommon;
using Microsoft.AspNetCore.Authorization;   // needed in PROD, not in DBG

namespace SqCoreWeb.Controllers
{
    //[Route("WebServer")]
    public class WebServerController : Microsoft.AspNetCore.Mvc.Controller
    {
#pragma warning disable IDE0052  // keep example in the code for future reference (IDE0052: 'Private member can be removed as the value assigned to it is never read')
        private readonly ILogger<Program> m_loggerKestrelStyleDontUse; // Kestrel sends the logs to AspLogger, which will send it back to NLog. It can be used, but practially never use it. Even though this is the official ASP practice. It saves execution resource to not use it. Also, it is more consistent to use Utils.Logger global everywhere in our code.
        private readonly IConfigurationRoot m_configKestrelStyleDontUse; // use the global Utils.Configuration instead. That way you don't have to pass down further in the call stack later
#pragma warning restore IDE0052
        private readonly IWebAppGlobals m_webAppGlobals;

        public WebServerController(ILogger<Program> p_logger, IConfigurationRoot p_config, IWebAppGlobals p_webAppGlobals)
        {
            m_loggerKestrelStyleDontUse = p_logger;
            m_configKestrelStyleDontUse = p_config;
            m_webAppGlobals = p_webAppGlobals;
        }

        [HttpGet]   // Ping is accessed by the HealthMonitor every 9 minutes (to keep it alive), no no GoogleAuth there
        public ActionResult Ping()
        {
            // pinging Index.html do IO file operation. Also currently it is a Redirection. There must be a quicker way to ping our Webserver. (for keeping it alive)
            // a ping.html or better a c# code that gives back only some bytes, not reading files. E.G. it gives back UTcTime. It has to be quick.
            return Content(@"<HTML><body>Ping. Webserver UtcNow:" + DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture) + "</body></HTML>", "text/html");
        }

        [HttpGet]
#if !DEBUG
        [Authorize]     // we can live without it, because ControllerCommon.CheckAuthorizedGoogleEmail() will redirect to /login anyway, but it is quicker that this automatically redirects without clicking another URL link.
#endif
        public ActionResult HttpRequestActivityLog()
        {
            HttpRequestLog[] logsPointerArr = Array.Empty<HttpRequestLog>();
            lock (m_webAppGlobals.HttpRequestLogs)  // prepare for multiple threads
            {
                logsPointerArr = m_webAppGlobals.HttpRequestLogs.ToArray();     // it copies only max 50 pointers to Array. Quick.
            }

            StringBuilder sb = new();
            for (int i = logsPointerArr.Length - 1; i >= 0; i--)        // foreach loop iterates over Queue starting from the oldest item and ending with newest.
            {
                var requestLog = logsPointerArr[i];
                string msg = String.Format("{0}#{1}{2} {3} '{4}' from {5} (u: {6}) ret: {7} in {8:0.00}ms", requestLog.StartTime.ToString("HH':'mm':'ss.f"), requestLog.IsError ? "ERROR in " : string.Empty, requestLog.IsHttps ? "HTTPS" : "HTTP", requestLog.Method, requestLog.Path + (String.IsNullOrEmpty(requestLog.QueryString) ? "" : requestLog.QueryString), requestLog.ClientIP, requestLog.ClientUserEmail, requestLog.StatusCode, requestLog.TotalMilliseconds);
                sb.Append(msg + "<br />");
            }

            return Content(@"<HTML><body><h1>HttpRequests Activity Log</h1><br />" + sb.ToString() + "</body></HTML>", "text/html");
        }

         [HttpGet]
#if !DEBUG
        [Authorize]     // we can live without it, because ControllerCommon.CheckAuthorizedGoogleEmail() will redirect to /login anyway, but it is quicker that this automatically redirects without clicking another URL link.
#endif
        public ActionResult ServerDiagnostics()
        {
            StringBuilder sb = new(@"<HTML><body><h1>ServerDiagnostics</h1>");
            Program.ServerDiagnostic(sb);
            BrokersWatcher.gWatcher.ServerDiagnostic(sb);
            MemDb.gMemDb.ServerDiagnostic(sb);
            SqWebsocketMiddleware.ServerDiagnostic(sb);
            DashboardClient.ServerDiagnostic(sb);

            return Content(sb.Append("</body></HTML>").ToString(), "text/html");
        }

        [HttpGet]
        public async Task<ActionResult> MemDbReloadHistData()
        {
            StringBuilder sb = new(@"<HTML><body><h1>MemDb: Force Reload Only Historical Data and Set New Timer</h1>");
            StringBuilder memDbSb = await MemDb.gMemDb.ForceReloadHistData(true);
            return Content(sb.Append(memDbSb).Append("</body></HTML>").ToString(), "text/html");
        }

        [HttpGet]
        public async Task<ActionResult> MemDbReloadDbData()
        {
            StringBuilder sb = new(@"<HTML><body><h1>MemDb: Reload All Db Data only If changed and Set New Timer</h1>");
            StringBuilder memDbSb = await MemDb.gMemDb.ReloadDbDataIfChanged(true);
            return Content(sb.Append(memDbSb).Append("</body></HTML>").ToString(), "text/html");
        }

        [HttpGet]
        public ActionResult TaskSchedulerNextTimes()
        {
            StringBuilder sb = new(@"<HTML><body><h1>TaskScheduler Next Times</h1>");
            StringBuilder scheduleTimesSb = SqTaskScheduler.gTaskScheduler.PrintNextScheduleTimes(true);
            return Content(sb.Append(scheduleTimesSb).Append("</body></HTML>").ToString(), "text/html");
        }


        [HttpGet]
        public ActionResult HttpRequestHeader()
        {
            StringBuilder sb = new();
            sb.Append("<html><body>");
            sb.Append("Request.Headers: <br><br>");
            foreach (var header in Request.Headers)
            {
                sb.Append($"{header.Key} : {header.Value} <br>");
            }
            sb.Append("</body></html>");

            return Content(sb.ToString(), "text/html");
        }

        [HttpGet]
        public ActionResult TestHealthMonitorByRaisingExceptionInController()
        {
            var parts = "www.domain.com".Split('.');
            Console.WriteLine(parts[12]);       // raises System.IndexOutOfRangeException()

            StringBuilder sb = new(); // The Code will not arrive here.
            sb.Append("<html><body>");
            sb.Append("TestHealthMonitorEmailByRaisingException: <br><br>");
            sb.Append("</body></html>");

            return Content(sb.ToString(), "text/html");
        }

        [HttpGet]
        public ActionResult TestHealthMonitorByRaisingStrongAssert()
        {
            StrongAssert.Fail(Severity.NoException, "Testing TestHealthMonitorByRaisingStrongAssert() with NoException. ThrowException version of StrongAssert can survive if it is catched.");

            return Content(@"<HTML><body>TestHealthMonitorByRaisingStrongAssert() finished OK. HealthMonitor should have received the message. </body></HTML>", "text/html");
        }

        static void RunUnobservedTaskException()
        {
            Task task1 = new(() =>
            {
                throw new ArgumentNullException();
            });

            Task task2 = new(() =>
            {
                throw new ArgumentOutOfRangeException();
            });

            task1.Start();
            task2.Start();

            while (!task1.IsCompleted || !task2.IsCompleted)
            {
                Thread.Sleep(50);
            }
        }

        [HttpGet]
        public ActionResult TestHealthMonitorByRaisingUnobservedTaskException()
        {
            Utils.Logger.Info("TestUnobservedTaskException BEGIN");
            // https://stackoverflow.com/questions/3284137/taskscheduler-unobservedtaskexception-event-handler-never-being-triggered

            RunUnobservedTaskException();

            GC.Collect();
            GC.WaitForPendingFinalizers();
            return Content(@"<HTML><body>TestHealthMonitorByRaisingUnobservedTaskException() finished OK. HealthMonitor should have received 2 different exceptions, but it will only send 1 email to admin. <br> Webserver UtcNow:" + DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture) + "</body></HTML>", "text/html");
        }

        [HttpGet]
        public ActionResult TestGoogleApiGsheet1()
        {
            Utils.Logger.Info("TestGoogleApiGsheet1() BEGIN");

            string? valuesFromGSheetStr = "Error. Make sure GoogleApiKeyKey, GoogleApiKeyKey is in SQLab.WebServer.SQLab.NoGitHub.json !";
            if (!String.IsNullOrEmpty(Utils.Configuration["Google:GoogleApiKeyName"]) && !String.IsNullOrEmpty(Utils.Configuration["Google:GoogleApiKeyKey"]))
            {
                // https://developers.google.com/sheets/api/guides/concepts
                // This gives back text colour and formatting of each cell. Not needed in general: https://sheets.googleapis.com/v4/spreadsheets/<spreadsheetId>?ranges=A1:C10&fields=properties.title,sheets(properties,data.rowData.values(effectiveValue,effectiveFormat))&key=<key>
                // This gives back only the values: https://sheets.googleapis.com/v4/spreadsheets/<spreadsheetId>/values/General!A:A?key=<key>
                
                // gSheet is public: https://docs.google.com/spreadsheets/d/1onwqrdxQIIUJytd_PMbdFKUXnBx3YSRYok0EmJF8ppM
                valuesFromGSheetStr = Utils.DownloadStringWithRetryAsync("https://sheets.googleapis.com/v4/spreadsheets/1onwqrdxQIIUJytd_PMbdFKUXnBx3YSRYok0EmJF8ppM/values/A1%3AA3?key=" + Utils.Configuration["Google:GoogleApiKeyKey"]).TurnAsyncToSyncTask();
                if (valuesFromGSheetStr == null)
                    valuesFromGSheetStr = "Error in DownloadStringWithRetry().";
            }

            Utils.Logger.Info("TestGoogleApiGsheet1() END");
            return Content($"<HTML><body>TestGoogleApiGsheet1() finished OK. <br> Received data: '{valuesFromGSheetStr}'</body></HTML>", "text/html");
        }

        [HttpGet]
        public ActionResult TestCaretakerCheckFreeDiskSpace()
        {
            Utils.Logger.Info("TestCaretakerCheckFreeDiskSpace() BEGIN");

            StringBuilder noteToClient = new();
            bool success = Caretaker.g_caretaker.CheckFreeDiskSpace(noteToClient);

            Utils.Logger.Info("TestCaretakerCheckFreeDiskSpace() END");
            return Content($"<HTML><body>TestCaretakerCheckFreeDiskSpace() finished with { (success ? "OK" : "Error") }. <br> Note To Client '<br>{noteToClient.Replace(Environment.NewLine, "<br>").Replace("    ", "&nbsp;&nbsp;&nbsp;&nbsp;")}'</body></HTML>", "text/html");
        }

        [HttpGet]
        public ActionResult TestCaretakerCleanLogfiles()
        {
            Utils.Logger.Info("TestCaretakerCleanLogfiles() BEGIN");

            StringBuilder noteToClient = new();
            bool success = Caretaker.g_caretaker.CleanLogfiles(noteToClient);

            Utils.Logger.Info("TestCaretakerCleanLogfiles() END");
            return Content($"<HTML><body>TestCaretakerCleanLogfiles() finished with { (success ? "OK" : "Error") }. <br> Note To Client '{noteToClient}'</body></HTML>", "text/html");
        }

        // just pass the HealthMonitorWebsite TS query to the HealthMonitor.EXE without further processing.
        [HttpPost, HttpGet]     // message from HealthMonitor webApp arrives as a Post, not a Get
        public async Task<ActionResult> ReportHealthMonitorCurrentStateToDashboardInJSON()
        {
            Utils.Logger.Info("ReportHealthMonitorCurrentStateToDashboardInJSON() BEGIN");
            // TODO: we should check here if it is a HttpGet (or a message without data package) and return gracefully

            string response = string.Empty;
            try
            {
                if (Request.Body.CanSeek)
                    Request.Body.Position = 0;                 // Reset the position to zero to read from the beginning.
                string jsonToBackEnd = await new StreamReader(Request.Body).ReadToEndAsync();

                Task<string?> tcpMsgTask = TcpMessage.Send(jsonToBackEnd, (int)HealthMonitorMessageID.GetHealthMonitorCurrentStateToHealthMonitorWebsite, ServerIp.HealthMonitorPublicIp, ServerIp.DefaultHealthMonitorServerPort, TcpMessageResponseFormat.JSON);
                string? tcpMsgResponse = await tcpMsgTask;
                if (tcpMsgTask.Exception != null || String.IsNullOrEmpty(tcpMsgResponse))
                {
                    Utils.Logger.Error("Error:HealthMonitor SendMessage exception.");
                    response = @"{""ResponseToFrontEnd"" : ""Error:HealthMonitor SendMessage exception. Check log file of the WepApp: ";
                }
                else
                    response = tcpMsgResponse;
            }
            catch (Exception ex)
            {
                Utils.Logger.Error(ex, "Error:HealthMonitor GetMessage exception.");
                response = @"{""ResponseToFrontEnd"" : ""Error: " + ex.Message + @"""}";
            }

            Utils.Logger.Info("ReportHealthMonitorCurrentStateToDashboardInJSON() END");
            return Content(response, "application/json");
        }
    }
}