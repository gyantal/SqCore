
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SqCommon;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using static SqCoreWeb.WsUtils;

namespace SqCoreWeb
{
    public class HttpRequestLog
    {
        public DateTime StartTime;
        public bool IsHttps;  // HTTP or HTTPS
        public string Method = string.Empty; // GET, PUT
        public HostString Host = new(string.Empty);
        public string Path = string.Empty;
        public string QueryString = string.Empty;  // it is not part of the path
        public string ClientIP = string.Empty;
        public string ClientUserEmail = string.Empty;
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
            _next = next ?? throw new ArgumentNullException(nameof(next));

            InitializeWhitelist();
        }

        public async Task Invoke(HttpContext httpContext)
        {
            // 0. This is the first entry point of ASP middleware. It is the first in the chain. Before that, there is only settings, like Rewrite() and Https redirection.
            if (httpContext == null)
                throw new ArgumentNullException(nameof(httpContext));

            // 1. Do whitelist check first. That will sort out the most number of bad requests. Always do that filter First that results the most refusing. Try to consume less resources, so don't log it to file.
            if (!IsHttpRequestOnWhitelist(httpContext))
            {
                // Console.WriteLine($"SqFirewall: request '{httpContext.Request.Host}' '{httpContext.Request.Path}' is not on whitelist.");
                // return Unauthorized();  // can only be used in Controller. https://github.com/aspnet/Mvc/blob/rel/1.1.1/src/Microsoft.AspNetCore.Mvc.Core/ControllerBase.cs
                httpContext.Response.StatusCode = StatusCodes.Status410Gone;  // '410 Gone' is better than '404 Not Found'. Client will not request it later. See https://en.wikipedia.org/wiki/List_of_HTTP_status_codes
                await httpContext.Response.WriteAsync($"SqFirewall: request '{httpContext.Request.Host}' '{httpContext.Request.Path}' is not on whitelist.", Encoding.UTF8);
                return;
            }

            // 2. Do blacklists too, because whitelist might check only for prefixes. Don't push it to the next Middleware if the path or IP is on the blacklist. In the future, implement a whitelist too, and only allow  requests explicitely on the whitelist.
            if (IsClientIpOrPathOnBlacklist(httpContext))
            {
                // silently log it and stop processing
                // string msg = String.Format($"{DateTime.UtcNow.ToString("HH':'mm':'ss.f")}#Blacklisted request is terminated: {httpContext.Request.Method} '{httpContext.Request.Path}' from {WsUtils.GetRequestIPv6(httpContext)}");
                // Console.WriteLine(msg);
                // gLogger.Info(msg);
                httpContext.Response.StatusCode = StatusCodes.Status410Gone;  // '410 Gone' is better than '404 Not Found'. Client will not request it later. See https://en.wikipedia.org/wiki/List_of_HTTP_status_codes
                await httpContext.Response.WriteAsync($"SqFirewall: request '{httpContext.Request.Path}' is on blacklist.", Encoding.UTF8);
                return;
            }

            Exception? exception = null;
            DateTime startTime = DateTime.UtcNow;
            var sw = Stopwatch.StartNew();
            try
            {
                // If crashing in query "/signin-google", then see comment in Startup.cs:OnRemoteFailure
                // if (httpContext.Request.Path.ToString().StartsWith("/signin-google"))
                //     Utils.Logger.Info("SqFirewallMiddlewarePreAuthLogger._next() will be called to check Google Authentication.");
                await _next(httpContext);   // continue in middleware app.UseAuthentication();
            }
            catch (Exception e)
            {
                // when NullReference exception was raised in TestHealthMonitorEmailByRaisingException(), The exception didn't fall to here. if 
                // It was handled already and I got a nice Error page to the client. So, here, we don't have the exceptions and exception messages and the stack trace.
                exception = e;

                Utils.Logger.Error(e, "SqFirewallMiddlewarePreAuthLogger._next() middleware");
                if (e.InnerException != null)
                    Utils.Logger.Error(e, "SqFirewallMiddlewarePreAuthLogger._next() middleware. InnerException.");
                throw;
            }
            finally
            {
                sw.Stop();  // Kestrel measures about 50ms more overhead than this measurement. Add 50ms more to estimate reaction time.

                var statusCode = httpContext.Response?.StatusCode;      // it may be null if there was an Exception
                var level = statusCode > 499 ? Microsoft.Extensions.Logging.LogLevel.Error : Microsoft.Extensions.Logging.LogLevel.Information;
                var clientIP = WsUtils.GetRequestIPv6(httpContext);
                var clientUserEmail = WsUtils.GetRequestUser(httpContext);

                var requestLog = new HttpRequestLog() { 
                    StartTime = DateTime.UtcNow, 
                    IsHttps = httpContext.Request.IsHttps, 
                    Method = httpContext.Request.Method,
                    Host = httpContext.Request.Host,       // "sqcore.net" for main, "dashboard.sqcore.net" for sub-domain queries
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
                string msg = String.Format("PreAuth.Postprocess: Returning {0}#{1}{2} {3} '{4} {5}' from {6} (u: {7}) ret: {8} in {9:0.00}ms", requestLog.StartTime.ToString("HH':'mm':'ss.f"), requestLog.IsError ? "ERROR in " : string.Empty, requestLog.IsHttps ? "HTTPS" : "HTTP", requestLog.Method, requestLog.Host, requestLog.Path, requestLog.ClientIP, requestLog.ClientUserEmail, requestLog.StatusCode, requestLog.TotalMilliseconds);
                // string shortMsg = String.Format("{0}#{1} {2} '{3} {4}' from {5} ({6}) in {7:0.00}ms", requestLog.StartTime.ToString("HH':'mm':'ss.f"), requestLog.IsError ? "ERROR in " : string.Empty, requestLog.Method, requestLog.Host, requestLog.Path, requestLog.ClientIP, requestLog.ClientUserEmail, requestLog.TotalMilliseconds);
                // Console.WriteLine(shortMsg);
                gLogger.Info(msg);

                if (requestLog.IsError)
                    LogDetailedContextForError(httpContext, requestLog);

                // at the moment, send only raised Exceptions to HealthMonitor, not general IsErrors, like wrong statusCodes
                if (requestLog.Exception != null && IsSendableToHealthMonitorForEmailing(requestLog.Exception))
                {
                    StringBuilder sb = new("Exception in SqCore.Website.C#.SqFirewallMiddlewarePreAuthLogger. \r\n");
                    var requestLogStr = String.Format("{0}#{1}{2} {3} '{4}' from {5} (u: {6}) ret: {7} in {8:0.00}ms", requestLog.StartTime.ToString("HH':'mm':'ss.f"), requestLog.IsError ? "ERROR in " : string.Empty, requestLog.IsHttps ? "HTTPS" : "HTTP", requestLog.Method, requestLog.Path + (String.IsNullOrEmpty(requestLog.QueryString) ? string.Empty : requestLog.QueryString), requestLog.ClientIP, requestLog.ClientUserEmail, requestLog.StatusCode, requestLog.TotalMilliseconds);
                    sb.Append("Request: " + requestLogStr + "\r\n");
                    sb.Append("Exception: '" + requestLog.Exception.ToStringWithShortenedStackTrace(1600) + "'\r\n");
                    HealthMonitorMessage.SendAsync(sb.ToString(), HealthMonitorMessageID.SqCoreWebCsError).TurnAsyncToSyncTask();
                }

            }

        }

        static string[] m_whitelistExact = {"index.html"};    // just examples. Will be overriden. Don't store the initial "/" because that is an extra character check 100x times.
        static string[] m_whitelistPrefix = {"ws/", "signin-google"};   //  just examples. Will be overriden.

        void InitializeWhitelist()
        {
            // There are 140 files (96 non-brotli) in wwwroot in 2020, there will be 1000 files in it in the future. We don't want a whitelist that performs 1000 string-comparisions, but binary search can help
            List<string> whitelistExact = new(10);
            List<string> whitelistPrefix = new(10);
            whitelistPrefix.AddRange(new string[] { "ws/", "signin-google" });   // Add WebSocket prefixes; and "/signin-google"

            DirectoryInfo di = new(Program.g_webAppGlobals.KestrelEnv!.WebRootPath);
            AddFileToListRecursive(di, string.Empty, ref whitelistExact);

            // https://stackoverflow.com/questions/21583278/getting-all-controllers-and-actions-names-in-c-sharp
            // we can also get the name of all the methods inside the Controllers, but we don't want to string-compare 200x times for each http request. So, just get the Controller names.
            Assembly asm = Assembly.GetExecutingAssembly();
            var controllersList = asm.GetTypes()
                .Where(type => typeof(Microsoft.AspNetCore.Mvc.ControllerBase).IsAssignableFrom(type))
                .Select(type => type.Name.Replace("Controller", string.Empty)).ToList();  // Controllers sometimes don't use "/" at the end. (like request "/ContangoVisualizerData", /JsLog") Other times they use: "/WebServer/Ping"
            whitelistPrefix.AddRange(controllersList);

            whitelistExact.Sort(StringComparer.OrdinalIgnoreCase);  // suspicion: by default Sort() uses IgnoreCase on Windows, but CaseSensitive on Linux. They both use the default ICU, which is set on the op.system. https://github.com/dotnet/runtime/issues/20109
            m_whitelistExact = whitelistExact.ToArray();
            whitelistPrefix.Sort(StringComparer.OrdinalIgnoreCase);
            m_whitelistPrefix = whitelistPrefix.ToArray();

            // Console.WriteLine($"m_whitelistExact ({m_whitelistExact.Length}): " + String.Join(", ", m_whitelistExact));
            gLogger.Info($"m_whitelistExact ({m_whitelistExact.Length}): " + String.Join(", ", m_whitelistExact));
        }

        void AddFileToListRecursive(DirectoryInfo p_di, string p_relPath, ref List<string> p_whitelistExact)
        {
            // https://stackoverflow.com/questions/5181405/best-way-to-iterate-folders-and-subfolders
            // string[] fileEntries = Directory.GetFiles(Program.g_webAppGlobals.KestrelEnv!.WebRootPath); // "/wwwroot"
            // System.IO.DirectoryInfo.EnumerateFiles is faster, because you can start enumerating the collection of FileInfo objects before the whole collection is returned.
            foreach (var fi in p_di.EnumerateFileSystemInfos())
            {
                if (!fi.Attributes.HasFlag(FileAttributes.Directory))
                { // File
#if DEBUG
                    if (fi.Name.EndsWith(".BR"))
                        throw new Exception("SqDev warning. Fix it.: The wwwroot folder should contain only lowercase extensions for Linux conformity.");
#endif
                    if (!fi.Name.EndsWith(".br"))   // don't add Brotli files to the list.
                        p_whitelistExact.Add(p_relPath + fi.Name);
                } else
                { // Folder
                    AddFileToListRecursive((DirectoryInfo)fi, p_relPath + fi.Name + "/", ref p_whitelistExact);
                }

            }
        }
        
        static bool IsHttpRequestOnWhitelist(HttpContext p_httpContext)
        {
            // "/" was already rewritten by app.UseRewriter() to '/index.html', so empty string check is not required.

            // 1. start with the most frequently good filter, which is the Prefix, not the Exact match. ("/hub/", "/ws/" or for Controller queries)
            // currently, the m_whitelistPrefix.Length is around 5. For that sequential search is fine. 
            // If number increases, to speed things up, we can do BinarySearch(), which will return an index near. Then we can check +1, -1 around that candidate. It might work for Prefixes.
            string prefixSearch = p_httpContext.Request.Path;
            if (prefixSearch[0] == '/')
                prefixSearch = prefixSearch[1..]; // remove initial '/' for comparisions
            foreach (var whitelistStr in m_whitelistPrefix)
            {
                if (prefixSearch.StartsWith(whitelistStr, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            // 2. Check exact matches. These are file names in the wwwroot folder
            // serving https://dashboard.sqcore.net/styles.d3e388801d8b7263f11a.css
            // if host == "dashboard.sqcore.net" then translate  https://dashboard.sqcore.net/styles.d3e388801d8b7263f11a.css to "webapps\MarketDashboard\styles.d3e388801d8b7263f11a.css"
            // but for Prefix checks, for "/hub/", "/ws/" or for Controller queries don't append this prefix to filename
            string exactSearch = p_httpContext.Request.Path;
            if (String.Equals(p_httpContext.Request.Host.ToString(), "dashboard.sqcore.net", StringComparison.OrdinalIgnoreCase))
            {
                exactSearch = "/webapps/MarketDashboard" + exactSearch;
            }
            else if (String.Equals(p_httpContext.Request.Host.ToString(), "healthmonitor.sqcore.net", StringComparison.OrdinalIgnoreCase))
            {
                exactSearch = "/webapps/HealthMonitor" + exactSearch;
            }
            // 2020-10: m_whitelistExact has 96 non-brotli out of 140 files both in the WindowsDev/wwwroot folder (Angular projects added) and on Linux
            // 2^7=128, so that is about 7 string comparisons with BinarySearch. Excellent.
            if (exactSearch[0] == '/')
                exactSearch = exactSearch[1..]; // remove initial '/' for comparisions
            int iExact = Array.BinarySearch(m_whitelistExact, exactSearch, StringComparer.OrdinalIgnoreCase);
            // Console.WriteLine($"Array.BinarySearch() for '{exactSearch}', result is {iExact}.'");
            if (iExact >= 0)
                return true;    // found
            // Otherwise, the object to search for is not found. The next larger object is at index '~iExact'.

            return false;
        }

        // "/robots.txt", "/ads.txt": just don't want to handle search engines. Consume resources.
        // static string[] m_blacklistPrefix = {"/private/", "/local/","/git/", "/app/", "/core/", "/rest/", "/.env","/robots.txt", "/ads.txt", "//", "/index.php", "/user/register", "/latest/dynamic", "/ws/stats", "/corporate/", "/imeges", "/remote"};
        static string[] m_blacklistPrefix = {"/ws/stats"};  // is whitelist is operational with Exact matches, that filters most of the bad. However, we accept '/ws' as a prefix for many, but we can ban this specific one.
        // hackers always try to break the server by typical vulnerability queries. It is pointless to process them. Most of the time it raises an exception.
        static bool IsClientIpOrPathOnBlacklist(HttpContext p_httpContext)
        {
            // 1. check that request path is allowed. 
            // Leave this active, because whitelist checks may only checks the folder part of the path. With the blacklist, we can ban specific files inside an allowed folder. 
            foreach (var blacklistStr in m_blacklistPrefix)
            {
                if (p_httpContext.Request.Path.StartsWithSegments(blacklistStr, StringComparison.OrdinalIgnoreCase))   
                    return true;
            }

            // 2. check client IP is banned or not
            return false;
        }

        static void LogDetailedContextForError(HttpContext httpContext, HttpRequestLog requestLog)
        {
            var request = httpContext.Request;
            string headers = string.Empty;
            foreach (var key in request.Headers.Keys)
                headers += key + "=" + request.Headers[key] + Environment.NewLine;

            string msg = String.Format("{0}{1} {2} '{3}' from {4} (user: {5}) responded {6} in {7:0.00} ms. RequestHeaders: {8}", requestLog.IsError ? "ERROR in " : string.Empty, requestLog.IsHttps ? "HTTPS" : "HTTP", requestLog.Method, requestLog.Path + (String.IsNullOrEmpty(requestLog.QueryString) ? string.Empty : requestLog.QueryString), requestLog.ClientIP, requestLog.ClientUserEmail, requestLog.StatusCode, requestLog.TotalMilliseconds, headers);
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
            if (p_exception is Microsoft.AspNetCore.Http.BadHttpRequestException)
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