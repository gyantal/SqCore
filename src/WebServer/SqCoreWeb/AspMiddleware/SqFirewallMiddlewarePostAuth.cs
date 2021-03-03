
using Microsoft.AspNetCore.Http;
using SqCommon;
using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using static SqCoreWeb.WsUtils;

namespace SqCoreWeb
{
    internal class SqFirewallMiddlewarePostAuth
    {
        private static readonly NLog.Logger gLogger = NLog.LogManager.GetCurrentClassLogger();   // the name of the logger will be the "Namespace.Class"

        readonly RequestDelegate _next;

        string[] mainIndexHtmlCached = new string[0];   // faster if it is pre-split into parts. Pattern matching search doesn't take real-time at every query to the main index.html.

        public SqFirewallMiddlewarePostAuth(RequestDelegate next)
        {
            if (next == null)
                throw new ArgumentNullException(nameof(next));
            _next = next;

            var mainIndexHtml = System.IO.File.ReadAllText(Program.g_webAppGlobals.KestrelEnv?.WebRootPath + "/index.html");
            var mainIndexHtmlArray = mainIndexHtml.Split(@"<a href=""/UserAccount/login"">Login</a>", StringSplitOptions.RemoveEmptyEntries);  // has only 2 items. Searched string is not included.
            mainIndexHtmlCached = new string[mainIndexHtmlArray.Length + 1];
            mainIndexHtmlCached[0] = mainIndexHtmlArray[0];
            mainIndexHtmlCached[1] = @"<a href=""/UserAccount/login"">Login</a>";
            mainIndexHtmlCached[2] = mainIndexHtmlArray[1];
        }

        public async Task Invoke(HttpContext httpContext)
        {
            if (httpContext == null)
                throw new ArgumentNullException(nameof(httpContext));

            // 1. checks user auth for some staticFiles (like HTMLs, Controller APIs), but not for everything (not jpg, CSS, JS)
            var userAuthCheck = WsUtils.CheckAuthorizedGoogleEmail(httpContext);
            if (userAuthCheck != UserAuthCheckResult.UserKnownAuthOK)
            {
                // It would be impossible task if subdomains are converted to path BEFORE this user auth check.
                // if "https://dashboard.sqcore.net" rewriten to  "https://sqcore.net/webapps/MarketDashboard/index.html" then login is //dashboard.sqcore.net/UserAccount/login
                // if "https://healthmonitor.sqcore.net" rewriten to.....
                // Otherwise, we redirect user to https://sqcore.net/UserAccount/login

                // if user is unknown or not allowed: log it but allow some files (jpeg) through, but not html or APIs
                

                string ext = Path.GetExtension(httpContext.Request.Path.Value) ?? String.Empty;
                bool isAllowedRequest = false;
                
                if (ext.Equals(".html", StringComparison.OrdinalIgnoreCase) || ext.Equals(".htm", StringComparison.OrdinalIgnoreCase))   // 1. HTML requests
                {
                    // Allow without user login only for the main domain's index.html ("sqcore.net/index.html"),  
                    // For subdomains, like "dashboard.sqcore.net/index.html" require UserLogin
                    if (((Program.g_webAppGlobals.KestrelEnv?.EnvironmentName == "Development") || httpContext.Request.Host.Host.StartsWith("sqcore.net")) &&
                        (httpContext.Request.Path.Value?.Equals("/index.html", StringComparison.OrdinalIgnoreCase) ?? false))
                    { // if it is HTML only allow '/index.html' through
                        isAllowedRequest = true;    // don't replace raw main index.html file by in-memory. Let it through. A brotli version will be delivered, which is better then in-memory non-compressed.
                        
                        // Problem: after Logout/Login Chrome takes index(Logout version).html from disk-cache, instead of reload.
                        // Because when it is read from 'index.html.br' brottli, it adds etag, and last-modified headers.
                        // So, the index(Logout version).html should NOT be cached, while the index(Login version).html should be cached.
                        Console.WriteLine($"Adding CacheControl NoCache to header '{httpContext.Request.Host} {httpContext.Request.Path}'");
                        Utils.Logger.Info($"Adding CacheControl NoCache to header '{httpContext.Request.Host} {httpContext.Request.Path}'");
                        httpContext.Response.GetTypedHeaders().CacheControl =
                            new Microsoft.Net.Http.Headers.CacheControlHeaderValue()
                            {
                                NoCache = true,
                                NoStore = true,
                                MustRevalidate = true
                            };
                    }
                }
                else if (String.IsNullOrEmpty(ext))  // 2. API requests
                {
                    if (httpContext.Request.Path.Value?.Equals("/UserAccount/login", StringComparison.OrdinalIgnoreCase) ?? false)   // if it is an API call only allow '/UserAccount/login' through. 
                        isAllowedRequest = true;
                    if ((Program.g_webAppGlobals.KestrelEnv?.EnvironmentName == "Development") && (httpContext.Request.Path.Value?.StartsWith("/hub/", StringComparison.OrdinalIgnoreCase) ?? false))
                        isAllowedRequest = true;    // in Development, when 'ng served'-d with proxy redirection from http://localhost:4202 to https://localhost:5001 , Don't force Google Auth, because 
                    if ((Program.g_webAppGlobals.KestrelEnv?.EnvironmentName == "Development") && (httpContext.Request.Path.Value?.StartsWith("/ws/", StringComparison.OrdinalIgnoreCase) ?? false))
                        isAllowedRequest = true;
                }
                else 
                    isAllowedRequest = true;    // 3. allow jpeg files and other resources, like favicon.ico

                if ((Program.g_webAppGlobals.KestrelEnv?.EnvironmentName == "Development") && httpContext.Request.Host.Host.StartsWith("127.0.0.1"))
                    isAllowedRequest = true;    // vscode-chrome-debug runs Chrome with --remote-debugging-port=9222. On that Gmail login is not possible. Result "This browser or app may not be secure.". So, don't require user logins if Chrome-Debug is used

                if (isAllowedRequest)
                {
                    // allow the requests. Let it through to the other handlers in the pipeline.
                    string msg = String.Format($"PostAuth.PreProcess: {DateTime.UtcNow.ToString("HH':'mm':'ss.f")}#Uknown or not allowed user request: {httpContext.Request.Method} '{httpContext.Request.Host} {httpContext.Request.Path}' from {WsUtils.GetRequestIPv6(httpContext)}. Falling through to further Kestrel middleware without redirecting to '/UserAccount/login'.");
                    Console.WriteLine(msg);
                    gLogger.Info(msg);
                }
                else
                {
                    string msg = String.Format($"PostAuth.PreProcess: {DateTime.UtcNow.ToString("HH':'mm':'ss.f")}#Uknown or not allowed user request: {httpContext.Request.Method} '{httpContext.Request.Host} {httpContext.Request.Path}' from {WsUtils.GetRequestIPv6(httpContext)}. Redirecting to '/UserAccount/login'.");
                    Console.WriteLine(msg);
                    gLogger.Info(msg);

                    // https://stackoverflow.com/questions/9130422/how-long-do-browsers-cache-http-301s
                    // 302 Found; Redirection; temporarily located on a different URL. Web clients must keep using the original URL.
                    // 301 Moved Permanently: Browsers will cache a 301 redirect with no expiry date. 
                    // "The browsers still honor the Cache-Control and Expires headers like with any other response, if they are specified. You could even add Cache-Control: no-cache so it won't be cached permanently."
                    // 301 resulted that https://healthmonitor.sqcore.net/ was permanently redirected to https://healthmonitor.sqcore.net/UserAccount/login 
                    // This redirection was fine Before Google authentication, but after that Browser never asked the index.html main page, but always redirected to /UserAccount/login
                    // That resulted a recursion in GoogleAuth, and after 3 recursions, Google realized it and redirected to https://healthmonitor.sqcore.net/signin-google without any ".AspNetCore.Correlation.Google." cookie 
                    // And that resulted 'System.Exception: Correlation failed.'

                    // httpContext.Response.Redirect("/UserAccount/login", true);  // forced login. Even for main /index.html
                    httpContext.Response.Redirect("/UserAccount/login", false); //  Temporary redirect response (HTTP 302). Otherwise, browser will cache it forever.
                    // raw Return in Kestrel chain would give client a response header: status: 200 (OK), Data size: 0. Browser will present a blank page. Which is fine now.
                    // httpContext.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
                    // await httpContext.Response.WriteAsync("Unauthorized request! Login on the main page with an authorized user."); // text response is quick and doesn't consume too much resource
                    return;
                }
            }
            else
            {
                // if user is accepted, index.html should be rewritten to change 'Login' link to username/logout link
                // in Development, Host = "127.0.0.1"
                if (((Program.g_webAppGlobals.KestrelEnv?.EnvironmentName == "Development") || httpContext.Request.Host.Host.StartsWith("sqcore.net")) 
                    && (httpContext.Request.Path.Value?.Equals("/index.html", StringComparison.OrdinalIgnoreCase) ?? false))
                {
                    //await _next(httpContext);
                    //await context.Response.WriteAsync($"Hello {CultureInfo.CurrentCulture.DisplayName}");
                    //return Content(mainIndexHtmlCached, "text/html");

                    // This solution has some Non-refresh problems after Logout, which happens almost never. 
                    // After UserAccount/logout server redirect goes to => Index.html. Reloads, and it comes from the cach (shows userName), which is bad.
                    // (But one simple manual Browser.Refresh() by the user solves it).
                    // >write to the user in a tooltip: "After Logout, Refresh the browser. That is the price of quick page load, when the user is logged in (99% of the time)"
                    Console.WriteLine($"Adding CacheControl MaxAge to header '{httpContext.Request.Host} {httpContext.Request.Path}'");
                    Utils.Logger.Info($"Adding CacheControl MaxAge to header '{httpContext.Request.Host} {httpContext.Request.Path}'");
                    httpContext.Response.GetTypedHeaders().CacheControl =
                        new Microsoft.Net.Http.Headers.CacheControlHeaderValue()
                        {
                            Public = true,
                            MaxAge = TimeSpan.FromDays(8)
                        };

                    var mainIndexHtmlCachedReplaced = mainIndexHtmlCached[0] + WsUtils.GetRequestUser(httpContext) +
                        @"&nbsp; <a href=""/UserAccount/logout"" title=""After Logout, Ctrl-Refresh the browser. That is the price of quick page load, when the user is logged in (99% of the time)"">Logout</a>"
                        + mainIndexHtmlCached[2];
                    await httpContext.Response.WriteAsync(mainIndexHtmlCachedReplaced);
                    return;
                }
            }

            await _next(httpContext);
        }

    }
}