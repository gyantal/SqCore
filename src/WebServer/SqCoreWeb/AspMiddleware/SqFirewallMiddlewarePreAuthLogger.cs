
using Microsoft.AspNetCore.Http;
using SqCommon;
using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using static SqCoreWeb.WsUtils;

namespace SqCoreWeb
{
    public class HttpRequestLog
    {
        public DateTime StartTime;
        public bool IsHttps;  // HTTP or HTTPS
        public string Method = String.Empty; // GET, PUT
        public HostString Host = new HostString(String.Empty);
        public string Path = String.Empty;
        public string QueryString = String.Empty;  // it is not part of the path
        public string ClientIP = String.Empty;
        public string ClientUserEmail = String.Empty;
        public int? StatusCode;
        public double TotalMilliseconds;
        public bool IsError;
        public Exception? Exception;
    }


    // we can call it SqFirewallMiddleware because it is used as a firewall too, not only logging request
    internal class SqFirewallMiddlewarePreAuthLogger
    {
        private static readonly NLog.Logger gLogger = NLog.LogManager.GetCurrentClassLogger();   // the name of the logger will be the "Namespace.Class"

        readonly RequestDelegate _next;

        public SqFirewallMiddlewarePreAuthLogger(RequestDelegate next)
        {
            if (next == null)
                throw new ArgumentNullException(nameof(next));
            _next = next;
        }

        public async Task Invoke(HttpContext httpContext)
        {
            if (httpContext == null)
                throw new ArgumentNullException(nameof(httpContext));

            // first entry point of ASP middleware
            if ((httpContext.Request.Path.Value == "/hub/dashboardpush") && httpContext.Request.QueryString.ToString().StartsWith("?id"))  // SignalR websocket connection connects once, and it never release the connection, id = connectionID number.
            {
                Utils.Logger.Info("SignalR client with clientID is connecting {httpContext.Request.Path.Value} {httpContext.Request.QueryString}");
            }
           
            // 1. Don't push it to the next Middleware if the path or IP is on the blacklist. In the future, implement a whitelist too, and only allow  requests explicitely on the whitelist.
            if (IsHttpRequestOnBlacklist(httpContext))
            {
                // silently log it and stop processing
                string msg = String.Format($"{DateTime.UtcNow.ToString("HH':'mm':'ss.f")}#Blacklisted request is terminated: {httpContext.Request.Method} '{httpContext.Request.Path}' from {WsUtils.GetRequestIP(httpContext)}");
                Console.WriteLine(msg);
                gLogger.Info(msg);
                return;
            }

            // 2.
            if (!IsHttpRequestOnWhitelist(httpContext))
            {
                // inform the user in a nice HTML page that 'for security' ask the superwisor to whitelist path ''
                return;
            }

            Exception? exception = null;
            DateTime startTime = DateTime.UtcNow;
            var sw = Stopwatch.StartNew();
            try
            {
                await _next(httpContext);
            }
            catch (Exception e)
            {
                // when NullReference exception was raised in TestHealthMonitorEmailByRaisingException(), The exception didn't fall to here. if 
                // It was handled already and I got a nice Error page to the client. So, here, we don't have the exceptions and exception messages and the stack trace.
                exception = e;
                throw;
            }
            finally
            {
                sw.Stop();  // Kestrel measures about 50ms more overhead than this measurement. Add 50ms more to estimate reaction time.

                var statusCode = httpContext.Response?.StatusCode;      // it may be null if there was an Exception
                var level = statusCode > 499 ? Microsoft.Extensions.Logging.LogLevel.Error : Microsoft.Extensions.Logging.LogLevel.Information;
                var clientIP = WsUtils.GetRequestIP(httpContext);
                var clientUserEmail = WsUtils.GetRequestUser(httpContext);

                var requestLog = new HttpRequestLog() { 
                    StartTime = DateTime.UtcNow, 
                    IsHttps = httpContext.Request.IsHttps, 
                    Method = httpContext.Request.Method,
                    Host =httpContext.Request.Host,
                    Path = httpContext.Request.Path, 
                    QueryString = httpContext.Request.QueryString.ToString(), 
                    ClientIP = clientIP, 
                    ClientUserEmail = clientUserEmail, 
                    StatusCode = statusCode, 
                    TotalMilliseconds = sw.Elapsed.TotalMilliseconds, 
                    IsError = exception != null || (level == Microsoft.Extensions.Logging.LogLevel.Error), 
                    Exception = exception };
                lock (Program.g_webAppGlobals.HttpRequestLogs)  // prepare for multiple threads
                {
                    Program.g_webAppGlobals.HttpRequestLogs.Enqueue(requestLog);
                    while (Program.g_webAppGlobals.HttpRequestLogs.Count > 50 * 10)  // 2018-02-19: MaxHttpRequestLogs was 50, but changed to 500, because RTP (RealTimePrice) rolls 50 items out after 2 hours otherwise. 500 items will last for 20 hours.
                        Program.g_webAppGlobals.HttpRequestLogs.Dequeue();
                }

                // $"{DateTime.UtcNow.ToString("MMdd'T'HH':'mm':'ss.fff")}#

                // string.Format("Value is {0}", someValue) which will check for a null reference and replace it with an empty string. It will however throw an exception if you actually pass  null like this string.Format("Value is {0}", null)
                string msg = String.Format("PreAuth.Postprocess: Returning {0}#{1}{2} {3} '{4} {5}' from {6} (u: {7}) ret: {8} in {9:0.00}ms", requestLog.StartTime.ToString("HH':'mm':'ss.f"), requestLog.IsError ? "ERROR in " : String.Empty, requestLog.IsHttps ? "HTTPS" : "HTTP", requestLog.Method, requestLog.Host, requestLog.Path, requestLog.ClientIP, requestLog.ClientUserEmail, requestLog.StatusCode, requestLog.TotalMilliseconds);
                string shortMsg = String.Format("{0}#{1} {2} '{3} {4}' from {5} ({6}) in {7:0.00}ms", requestLog.StartTime.ToString("HH':'mm':'ss.f"), requestLog.IsError ? "ERROR in " : String.Empty, requestLog.Method, requestLog.Host, requestLog.Path, requestLog.ClientIP, requestLog.ClientUserEmail, requestLog.TotalMilliseconds);
                Console.WriteLine(shortMsg);
                gLogger.Info(msg);

                if (requestLog.IsError)
                    LogDetailedContextForError(httpContext, requestLog);

                // at the moment, send only raised Exceptions to HealthMonitor, not general IsErrors, like wrong statusCodes
                if (requestLog.Exception != null && IsSendableToHealthMonitorForEmailing(requestLog.Exception))
                {
                    StringBuilder sb = new StringBuilder("Exception in SqCore.Website.C#.SqFirewallMiddlewarePreAuthLogger. \r\n");
                    var requestLogStr = String.Format("{0}#{1}{2} {3} '{4}' from {5} (u: {6}) ret: {7} in {8:0.00}ms", requestLog.StartTime.ToString("HH':'mm':'ss.f"), requestLog.IsError ? "ERROR in " : String.Empty, requestLog.IsHttps ? "HTTPS" : "HTTP", requestLog.Method, requestLog.Path + (String.IsNullOrEmpty(requestLog.QueryString) ? "" : requestLog.QueryString), requestLog.ClientIP, requestLog.ClientUserEmail, requestLog.StatusCode, requestLog.TotalMilliseconds);
                    sb.Append("Request: " + requestLogStr + "\r\n");
                    sb.Append("Exception: '" + requestLog.Exception.ToStringWithShortenedStackTrace(800) + "'\r\n");
                    HealthMonitorMessage.SendAsync(sb.ToString(), HealthMonitorMessageID.SqCoreWebCsError).TurnAsyncToSyncTask();
                }

            }

        }

        // "/robots.txt", "/ads.txt": just don't want to handle search engines. Consume resources.
        static string[] m_blacklistStarts = {"/private/", "/local/","/git/", "/app/", "/core/", "/rest/", "/.env","/robots.txt", "/ads.txt", "//", "/index.php", "/user/register", "/latest/dynamic", "/ws/stats", "/corporate/", "/imeges", "/remote"};
        // hackers always try to break the server by typical vulnerability queries. It is pointless to process them. Most of the time it raises an exception.
        static bool IsHttpRequestOnBlacklist(HttpContext p_httpContext)
        {
            // 1. check request path is allowed
            foreach (var blacklistStr in m_blacklistStarts)
            {
                if (p_httpContext.Request.Path.StartsWithSegments(blacklistStr, StringComparison.OrdinalIgnoreCase))   
                    return true;
            }

            // 2. check client IP is banned or not
            return false;
        }

        static bool IsHttpRequestOnWhitelist(HttpContext p_httpContext)
        {
            return true;
        }

        static void LogDetailedContextForError(HttpContext httpContext, HttpRequestLog requestLog)
        {
            var request = httpContext.Request;
            string headers = String.Empty;
            foreach (var key in request.Headers.Keys)
                headers += key + "=" + request.Headers[key] + Environment.NewLine;

            string msg = String.Format("{0}{1} {2} '{3}' from {4} (user: {5}) responded {6} in {7:0.00} ms. RequestHeaders: {8}", requestLog.IsError ? "ERROR in " : String.Empty, requestLog.IsHttps ? "HTTPS" : "HTTP", requestLog.Method, requestLog.Path + (String.IsNullOrEmpty(requestLog.QueryString) ? "" : requestLog.QueryString), requestLog.ClientIP, requestLog.ClientUserEmail, requestLog.StatusCode, requestLog.TotalMilliseconds, headers);
            Console.WriteLine(msg);
            gLogger.Error(msg);    // all the details (IP, Path) go the the Error output, because if the Info level messages are ignored by the Logger totally, this will inform the user. We need all the info in the Error Log. Even though, if Info and Error levels both logged, it results duplicates
        }


        public static bool IsSendableToHealthMonitorForEmailing(Exception p_exception)
        {
            // anonymous people sometimes connect and we have SSL or authentication errors
            // also we are not interested in Kestrel Exception. Some of these exceptions are not bugs, but correctSSL or Authentication fails.
            // we only interested in our bugs our Controller C# code
            string fullExceptionStr = p_exception.ToString();   // You can simply print exception.ToString() -- that will also include the full text for all the nested InnerExceptions.
            bool isSendable = true;
            if (p_exception is Microsoft.AspNetCore.Server.Kestrel.Core.BadHttpRequestException)
            {
                // bad request data: "Request is missing Host header."
                // bad request data: "Invalid request line: ..."
                isSendable = false;
            }

            gLogger.Debug($"IsSendableToHealthMonitorForEmailing().IsSendable:{isSendable}, FullExceptionStr:'{fullExceptionStr}'");
            return isSendable;
        }

    }
}