using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Rewrite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SqCommon;

namespace SqCoreWeb;

public class Startup
{
    public Startup(IConfiguration configuration)
    {
        Configuration = configuration;
    }

    public IConfiguration Configuration { get; }

#pragma warning disable CA1822 // "Mark members as static". Kestrel assumes this is an instance method, not static.
    // This method gets called by the runtime. Use this method to add services to the container.
    public void ConfigureServices(IServiceCollection services)
    {
#pragma warning restore CA1822
        // Asp.Net DependenciInjection (DI) of Kestrel policy for separating the creation of dependencies (IWebHostEnvironment, Options, Logger) from its actual usage in Controllers.
        // That way Controllers are light. And if there are 100 Controller classes, repeating the creation of Dependent objects (IWebHostEnvironment) is not in their source code. So, the source code of Controllers are light.
        // DI is not necessary. DotNet core bases classes doesn't use that for logging or anything. However, Kestrel uses it, which we can honour. It also helps in unit-test.
        // But it is perfectly fine to do the Creation of dependencies (Logger, like nLog) in the Controller.
        // Transient objects are always different; a new instance is provided to every controller and every service.
        // Scoped objects are the same within a request, but different across different requests
        // Singleton objects are the same for every object and every request(regardless of whether an instance is provided in ConfigureServices)
        services.AddSingleton(_ => Utils.Configuration);  // this is the proper DependenciInjection (DI) way of pushing it as a service to Controllers. So you don't have to manage the creation or disposal of instances.
        services.AddSingleton(_ => Program.WebAppGlobals);

        services.AddHttpsRedirection(options =>
        {
            options.RedirectStatusCode = StatusCodes.Status307TemporaryRedirect;
            options.HttpsPort = 5001;
        });

        // https://docs.microsoft.com/en-us/aspnet/core/performance/caching/response?view=aspnetcore-3.0
        services.AddResponseCaching(); // DI: these services could be used in MVC Control/Razor pages (either as [Attributes], or in code)
        services.AddMvc(options => // AddMvc() equals AddControllersWithViews() + AddRazorPages()
        {
            // For server-side caching that follows the HTTP 1.1 Caching specification, use this Response Caching Middleware.
            // Note that even though the browser loads data from (disk cache), the client ask the ServerSideMiddleware, so there is a 1msec time delay for these server-side cached queries.
            // But there is no server side processing. The Controllers are not called for any 900 msec processing. The AspMiddleware instructs the browser to use the disk cache.
            // https://docs.microsoft.com/en-us/aspnet/core/performance/caching/response?view=aspnetcore-6.0#responsecache-attribute
            // "There isn't a corresponding HTTP header (sent to client) for the VaryByQueryKeys property. (only the "cache-control: public,max-age=30" is sent in the header) The property is an HTTP feature handled by Response Caching Middleware.
            // VaryByQueryKeys: For the middleware to serve a cached response, the query string and query string value must match a previous request. "

            // These CashProfiles are given only once here, and if they change, we only have to change here, not in all Controllers.
            options.CacheProfiles.Add(
                "NoCache",
                new CacheProfile()
                {
                    Duration = 0,
                    Location = ResponseCacheLocation.None,
                    NoStore = true
                });
            // by default, without VaryByQueryKeys, the querystring is not used by the browser's Cache Policy, only the URL. The queryString https://sqcore.net/ContangoVisualizerData?commo=1 returned from cache the last received https://sqcore.net/ContangoVisualizerData?commo=3
            options.CacheProfiles.Add(
                "DefaultShortDuration",
                new CacheProfile()
                {
                    Duration = 60 * 1,   // 1 min for real-time price data
                    VaryByQueryKeys = new string[] { "*" } // a specific query string key or * can be used if all query string keys should be matched
                });
            options.CacheProfiles.Add(
                "DefaultMidDuration",
                new CacheProfile()
                {
                    // Duration = (int)TimeSpan.FromHours(12).TotalSeconds
                    Duration = 100000,   // 100,000 seconds = 27 hours
                    VaryByQueryKeys = new string[] { "*" } // a specific query string key or * can be used if all query string keys should be matched
                });
        });

        services.AddCompressedStaticFiles();

        // https://docs.microsoft.com/en-us/aspnet/core/performance/response-compression
        // this is on the fly, just-in-time (JIT) compression. CompressionLevel.Optimal takes 250ms, but CompressionLevel.Fastest takes 4ms time on CPU, but still worth it.
        // 2022-08: keep in code, but it was commented out when CompressedStaticFileMiddleware started to work. Ahead of time is better than on the fly
        // services.AddResponseCompression(options =>
        // {
        //     options.Providers.Add<BrotliCompressionProvider>();
        //     options.Providers.Add<GzipCompressionProvider>();
        //     // Default Mime types: application/javascript, application/json, application/xml, text/css, text/html, text/json, text/plain, text/xml
        //     options.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat( new[] { "image/svg+xml" });
        // });
        // services.Configure<BrotliCompressionProviderOptions>(options =>
        // {
        //     options.Level = CompressionLevel.Fastest;
        // });

        string? googleClientId = Utils.Configuration["Google:ClientId"];
        string? googleClientSecret = Utils.Configuration["Google:ClientSecret"];

        if (!String.IsNullOrEmpty(googleClientId) && !String.IsNullOrEmpty(googleClientSecret))
        {
            // The reason you have BOTH google and cookies Auth is because you're using Google for identity information but using cookies for storage of the identity for only asking Google once.
            // So AddIdentity() is not required, but Cookies Yes.
            services.AddAuthentication(options =>
            {
                // If you don't want the cookie to be automatically authenticated and assigned to HttpContext.User,
                // remove the CookieAuthenticationDefaults.AuthenticationScheme parameter passed to AddAuthentication.
                options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;  // For anything else (sign in, sign out, authenticate, forbid), use the cookies scheme
                options.DefaultChallengeScheme = GoogleDefaults.AuthenticationScheme;   // For challenges, use the google scheme. If not, "InvalidOperationException: No authenticationScheme was specified"

                options.DefaultAuthenticateScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                options.DefaultSignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
            })
            .AddCookie(o =>
            { // CookieAuth will be the default from the two, GoogleAuth is used only for Challenge
                o.LoginPath = "/UserAccount/login";
                o.LogoutPath = "/UserAccount/logout";

                // 2020-05-30: WARN|Microsoft.AspNetCore.Authentication.Google.GoogleHandler: '.AspNetCore.Correlation.Google.bzb7A4oxoS_pz_xQk0N4WngqgL0nyLUiT0k5QSPsD_M' cookie not found.
                // "Exception: Correlation failed.".
                // Maybe because SameSite cookies policy changed.
                // I suspect Bunny used an old Chrome or FFox or Edge.
                // "AspNetCore as a rule does not implement browser sniffing for you because User-Agents values are highly unstable"
                // However, if updating browser of the user to the latest Chrome doesn't solve it, we may implement these changes:
                // https://github.com/dotnet/aspnetcore/issues/14996
                // https://docs.microsoft.com/en-us/aspnet/core/security/samesite
                // "Cookies without SameSite header are treated as SameSite=Lax by default.
                // SameSite=None must be used to allow cross-site cookie use.
                // Cookies that assert SameSite=None must also be marked as Secure. (requires HTTPS)"
                // 2020-01: 'Correlation failed.' is a Browser Cache problem. 2020-06-03: JMC could log in. Error email 'correlation failed' arrived. When I used F12 in Chrome, disabled cache; then login went OK.

                // 2020-08: Chrome implements this default behavior as of version 84. (2020-08). Edge doesn't restrict that yet.
                // without any intervention, http://localhost/login returns this to the browser: ""Set-Cookie: .AspNetCore.Correlation.Google._AcFoUd0-sbBMoGfefWKA2WlqpVJwD2bGYTYs6axoBU=N; expires=Fri, 14 Aug 2020 14:45:30 GMT; path=/signin-google; samesite=none; httponly"
                // and Chrome throws an Error to JsConsole: "A cookie associated with a resource at http://localhost/ was set with `SameSite=None` but without `Secure`. It has been blocked"
                // disable this feature by going to "chrome://flags" and disabling "Cookies without SameSite must be secure", but it is good for development only
                // So, from now on, because we want to use Chrome84+, if we want login, we have to develop in HTTPS mode, not HTTP. We can completely forget HTTP. Just use HTTPS, even in DEV.

                // >GoogleAuth Login system uses cookie (.AspNetCore.Correlation.Google). From 2020-08, Chrome blocks a SameSite=None, which is not Secure.
                // But Secure means it is running on HTTPS. So, local development will also need to be done with HTTPS urls.
                // >Specify SameSite=None and Secure if the cookie should be sent in cross-site requests. This enables third-party use.
                // Specify SameSite=Strict or SameSite=Lax if the cookie should not be sent in cross-site requests.
                // But even in this case, if we use Both HTTP, HTTPS at development, Login problems arise on HTTP.
                // >Chrome debug: cookie HTTP://".AspNetCore.Cookies": "This set-cookie was blocked because it was not sent over a secure connection and would have overwritten a cookie with a secure attribute.",
                // but then that Secure HTTPS cookie with the same name is not sent to the non-secure HTTP request. (It is only sent to the HTTPS request).
                // Therefore, we should use only the HTTPS protocol, even in local development.  (except if AWS CloudFront cannot handle HTTPS to HTTPS conversions)
                // See cookies: Facebook and Google logins only work in HTTPS (even locally), and because we want in Local development the same experience as is production, we eliminate HTTP in local development

                // Cookies are shared between ports. So, https://localhost:5001/ and https://localhost:443 share the same cookie (login info), but http://localhost:5000/ cannot overwrite that cookie in Chrome

                o.Cookie.SameSite = SameSiteMode.Lax;    // sets the cookie ".AspNetCore.Cookies"
                o.Cookie.SecurePolicy = CookieSecurePolicy.Always;      // Note this will also require you to be running on HTTPS. Local development will also need to be done with HTTPS urls.
                // o.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;   // this is the default BTW, so no need to set.
                // problem: if Cookie storage works in https://localhost:5001/UserAccount/login  but not in HTTP: http://localhost:5000/UserAccount/login
                // "Note that the http page cannot set an insecure cookie of the same name as the secure cookie."
                // Solution: Manually delete the cookie from Chrome. see here.  https://bugs.chromium.org/p/chromium/issues/detail?id=843371
                // in Production, only HTTPS is allowed anyway, so it will work. Best is not mix development in both HTTP/HTTPS (just stick to one of them).
                // stick to HTTPS. Although Chrome browser-caching will not work in HTTPS (because of insecure cert), it is better to test HTTPS, because that will be the production.

                // Controls how much time the authentication ticket stored in the cookie will remain valid
                // This is separate from the value of Microsoft.AspNetCore.Http.CookieOptions.Expires, which specifies how long the browser will keep the cookie. We will set that in OnTicketReceived()
                o.ExpireTimeSpan = TimeSpan.FromDays(350);  // allow 1 year expiration.
            })
            .AddGoogle("Google", options =>
            {
                options.ClientId = googleClientId;
                options.ClientSecret = googleClientSecret;
                options.CorrelationCookie.SameSite = SameSiteMode.Lax; // sets the cookie ".AspNetCore.Correlation.Google.*"
                options.CorrelationCookie.SecurePolicy = CookieSecurePolicy.Always;
                // Note: Once logged in to Google Ecosystem (and once allowed Sqcore website), the Google login prompt (offering different users) does not even display.
                // Do we want it displayed? Probably NOT. Because this is good and fast:
                // "the Google login prompt does not even display. From the app I get redirected to Google,
                // and because I am already signed in with a user with that domain, Google immediately returns that as the authenticated user to your app."
                // If you really want to logout that Guser from SqCore: sign out of your Google account (in Gmail, GDrive or any G.app), or open an Incognito browser window.
                // >e.g. go do GoogleDrive: log-out as user. After that SqCore will ask the user login user only once. But that login will login to ALL Google services.
                // Which is actually fine. That is what I want. Once user logged in to his Gmail, he can enjoy SqCore without logging in again.
                // So, the same way, why GoogleDrive doesn't re-ask the password every time, the same applies here too.
                options.Events = new OAuthEvents
                {
                    // https://www.jerriepelser.com/blog/forcing-users-sign-in-gsuite-domain-account/
                    OnRedirectToAuthorizationEndpoint = context =>
                    {
                        Utils.Logger.Info("GoogleAuth.OnRedirectToAuthorizationEndpoint()");
                        // context.Response.Redirect(context.RedirectUri + "&hd=" + System.Net.WebUtility.UrlEncode("jerriepelser.com"));
                        context.Response.Redirect(context.RedirectUri, false); //  Temporary redirect response (HTTP 302)
                        return Task.CompletedTask;
                    },
                    OnCreatingTicket = context =>
                    {
                        Utils.Logger.Info("GoogleAuth.OnCreatingTicket(), User: " + context.User);
                        // Utils.Logger.Debug($"[Authorize] attribute forced Google auth. Email:'{email ?? "null"}', RedirectUri: '{context.Properties.RedirectUri ?? "null"}'");

                        // if (!Utils.IsAuthorizedGoogleUsers(Utils.Configuration, email))
                        //     throw new Exception($"Google Authorization Is Required. Your Google account: '{ email }' is not accepted. Logout this Google user and login with another one.");

                        // string domain = context.User.Value<string>("domain");
                        // if (domain != "jerriepelser.com")
                        //    throw new GoogleAuthenticationException("You must sign in with a jerriepelser.com email address");

                        return Task.CompletedTask;
                    },
                    OnTicketReceived = context =>
                    {
                        Utils.Logger.Info("GoogleAuth.OnTicketReceived()");
                        // if this is not set, then the cookie in the browser expires, even though the validation-info in the cookie is still valid. By default, cookies expire: "When the browsing session ends" Expires: 'session'
                        // https://www.jerriepelser.com/blog/managing-session-lifetime-aspnet-core-oauth-providers/
                        if (context.Properties != null)
                        {
                            context.Properties.IsPersistent = true;
                            context.Properties.ExpiresUtc = DateTimeOffset.UtcNow.AddDays(25);
                        }

                        return Task.FromResult(0);
                    },
                    OnRemoteFailure = remoteFailureContext =>
                    {
                        Utils.Logger.Error("GoogleAuth.OnRemoteFailure()");
                        Console.WriteLine("Error! GoogleAuth.OnRemoteFailure(");
                        // 2021-07-06: Daya had login problems on localhost:5001 only.
                        // "/signin-google?...<signin-token>" crashed in SqFirewallMiddlewarePreAuthLogger: _await _next(httpContext);
                        // "Microsoft.AspNetCore.Authentication.Google.GoogleHandler: Information: Error from RemoteAuthentication: A task was canceled.."
                        // StackTrace:
                        // at System.Net.Http.HttpConnectionPool.GetHttpConnectionAsync()
                        // ...
                        // at System.Net.Http.HttpClient.SendAsyncCore()
                        // ..
                        // at Microsoft.AspNetCore.Authentication.AuthenticationMiddleware.Invoke(HttpContext context)
                        // at SqCoreWeb.SqFirewallMiddlewarePreAuthLogger.Invoke(HttpContext httpContext)
                        // It seems that when Google returns our token in "/signin-google?...<signin-token>", ASP.Net core downloads something from GoogleServer with HttpClient
                        // That failed. We don't know the reason yet.
                        // It was quickly solved by that Daya changed his internet service provider to BackupInternet2 (maybe mobile-internet). With that internet connection this HttpClient download didn't fail.
                        // Some people in the forums complain about the same things and said that they solved it by setting up a Proxy.
                        // So, something is weird with Daya's Main internet service provider. He will have a new fiber optic internet in a week.
                        // https://github.com/googleapis/google-api-dotnet-client/issues/1394
                        // https://github.com/Clancey/SimpleAuth/issues/41  " if there is no [proper, secure] internet connection ... it results in a TaskCanceledException"
                        HealthMonitorMessage.SendAsync("GoogleAuth.OnRemoteFailure(). See comments in code.", HealthMonitorMessageID.SqCoreWebCsError).TurnAsyncToSyncTask();
                        return Task.FromResult(0);
                    }
                };
            });
        }
        else
        {
            Console.WriteLine("A_G_CId and A_G_CSe from Config has NOT been found. Cannot initialize GoogelAuthentication.");
            // Utils.Logger.Warn("A_G_CId and A_G_CSe from Config has NOT been found. Cannot initialize GoogelAuthentication.");
        }
    }

#pragma warning disable CA1822 // "Mark members as static". Kestrel assumes this is an instance method, not static.
    // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
    public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
    {
#pragma warning restore CA1822
        Program.WebAppGlobals.KestrelEnv = env;

        if (!env.IsDevelopment())
        {
            // In .NET 6, app.UseDeveloperExceptionPage(); is added by default when env.IsDevelopment(). But we also want it in Production for the first years of  development.
            app.UseDeveloperExceptionPage(); // returns a nice webpage that shows the stack trace and everything of the crash. Can be used even in Production to catch the error quicker.
            // app.UseExceptionHandler("/error.html"); // Usually used in Production. It hides the crash details totally from the user. There is no browser redirection. It returns 'error.html' with status: 200 (OK). Maybe 500 (Error) would be better to return, but then the Browser might not display that page to the user.

            // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
            app.UseHsts();
            app.UseHttpsRedirection();     // Chrome Caching warning! If you are developing using a self-signed certificate over https and there is an issue with the certificate then google will not cache the response
        }

        // TODO: experiment with this later. Currently default works fine, because 'ng serve proxy' redirect
        // The protections provided by CORS don't apply to WebSockets. Browsers do not: Perform CORS pre-flight requests. Respect the restrictions specified in Access-Control headers when making WebSocket requests.
        // var webSocketOptions = new WebSocketOptions()
        // {
        //     KeepAliveInterval = TimeSpan.FromSeconds(120),
        //     ReceiveBufferSize = 4 * 1024
        // };
        // webSocketOptions.AllowedOrigins.Add("https://localhost:5001");
        // webSocketOptions.AllowedOrigins.Add("https://localhost:4202");
        // app.UseWebSockets(webSocketOptions);

        // app.UseDefaultFiles();      // "UseDefaultFiles is a URL rewriter (default.htm, default.html, index.htm, index.html whichever first, 4 file queries to find the file) that doesn't actually serve the file. "
        app.UseRewriter(new RewriteOptions()
        .AddRewrite(@"^$", "index.html", skipRemainingRules: true) // empty string converted to index.html. Only 1 query to find the index.html file. Better than UseDefaultFiles()
        .AddRewrite(@"^(.*)/$", "$1/index.html", skipRemainingRules: true)); // converts "/" to "/index.html", e.g. .AddRewrite(@"^HealthMonitor/$", @"HealthMonitor/index.html" and all Angular projects.

        app.UseMiddleware<SqFirewallMiddlewarePreAuthLogger>();

        // place UseAuthentication() AFTER UseRouting(),  https://docs.microsoft.com/en-us/aspnet/core/migration/22-to-30?view=aspnetcore-2.2&tabs=visual-studio
        app.UseAuthentication();    // If execution reaches here and user is not logged in, this will redirect to Google-login. It is needed for filling httpContext?.User?.Claims. StaticFiles are served Before the user is login is assured. This is fast, but httpContext?.User?.Claims is null in this case.

        app.UseMiddleware<SqFirewallMiddlewarePostAuth>();  // For this to catch Exceptions, it should come after UseExceptionHadlers(), because those will swallow exceptions and generates nice ErrPage.

        // Request "dashboard.sqcore.net/index.html" should be converted to "sqcore.net/webapps/MarketDashboard/index.html"
        // But Authentication (and user check) should be done BEFORE that, because we will lose the subdomain 'dashboard' prefix from the host.
        // And the browser keeps separate cookies for the subdomain and main domain. dashboard.sqcore.net has different cookies than sqcore.net
        var options = new RewriteOptions();
        options.Rules.Add(new SubdomainRewriteOptionsRule());
        app.UseRewriter(options);

        app.Use(async (context, next) =>
        {
            Utils.Logger.Info($"Serving '{context.Request.Path.Value}'");
            await next();
        });

        // WebSocket should come After authentication, After SubdomainRewrite, but Caching is not necessary.
        var webSocketOptions = new WebSocketOptions()
        {
            KeepAliveInterval = TimeSpan.FromSeconds(120),  // default is 2 minutes
        };
        app.UseWebSockets(webSocketOptions);

        // Edge browser bug. Aug 30, 2019: "EdgeHTML not respecting nomodule attribute on script tag". It downloads both ES5 and ES6(2015) versions. https://developer.microsoft.com/en-us/microsoft-edge/platform/issues/23357397/
        // can be fixed to only emit ES2015: https://stackoverflow.com/questions/56495683/angular-cli-8-is-it-possible-to-build-only-on-es2015

        // Chrome Caching warning! If you are developing using a self-signed certificate over https and there is an issue with the certificate then google will not cache the response no matter what cache headers you use.
        // So, while developing browser caching on localhost: Either:
        // 1. Test HTTPS on port 5001 in Edge, https://localhost:5001/HealthMonitor/   OR
        // 2. Test HTTP on PORT 5000 in Chrome, http://localhost:5000/HealthMonitor/  (disable UseHttpsRedirection()) not HTTPS  (but note that Chrome can be slow on http://localhost)

        // because when we do Ctrl-R in Chrome, the Request header contains: "cache-control: no-cache". Then ResponseCaching will not use entry, and places this log:
        // dbug: Microsoft.AspNetCore.ResponseCaching.ResponseCachingMiddleware[9]
        //     The age of the entry is 00:05:23.2291902 and has exceeded the maximum age of 00:00:00 specified by the 'max-age' cache directive.
        // So, if we want to test responseCaching, open the same '/WeatherForecast' in a different tab.
        // GET '/WeatherForecast' from 127.0.0.1 (gyantal@gmail.com) in 63.35ms can decrease to
        // GET '/WeatherForecast' from 127.0.0.1 (gyantal@gmail.com) in 4.21ms
        app.UseResponseCaching();       // this fills up the Response header Cache-Control, but only for MVC Controllers (classes, methods), Razor Page handlers (classes)

        app.Use(async (context, next) => // this fills up the Response header Cache-Control for everything else, like static files.
        {
            // main Index.html cache is controlled in SqFirewallMiddlewarePostAuth(), because to differentiate based on Login/Logout
            if (((Program.WebAppGlobals.KestrelEnv?.EnvironmentName == "Development") || context.Request.Host.Host.StartsWith("sqcore.net"))
                && (context.Request.Path.Value?.Equals("/index.html", StringComparison.OrdinalIgnoreCase) ?? false))
            {
                await next();
                return;
            }

            if (!env.IsDevelopment()) // in development, don't use browser caching at all.
            {
                // we have to add header Before filling up the response with 'await next();', otherwise
                // if we try to add After StaticFiles(), we got exception: "System.InvalidOperationException: Headers are read-only, response has already started."
                TimeSpan maxBrowserCacheAge = TimeSpan.Zero;
                var path = context.Request.Path.Value;
                if (path != null &&
                    (path.Equals("/index.html", StringComparison.OrdinalIgnoreCase) // main index.html has Login/username on it. After Login, the page should be refreshed. So, ignore CacheControl for that
                    || path.StartsWith("/hub/") || path.StartsWith("/ws/"))) // WebSockets should not be cached
                {
                    maxBrowserCacheAge = TimeSpan.Zero;
                }
                else
                {
                    string ext = Path.GetExtension(context.Request.Path.Value) ?? string.Empty;
                    if (ext != string.Empty) // If has any extension, then it is not a Controller (but probably a StaticFile()). If it is "/", then it is already converted to "index.htmL". Controllers will handle its own cacheAge with attributes.
                    {
                        // UseResponseCaching() will fill up headers, if MVC controllers or Razor pages, we don't want to use this caching, because the Controller will specify it in an attribute.
                        // probably no cache for API calls like "https://localhost:5001/WeatherForecast"  (they probably get RT data), Controllers will handle it.
                        maxBrowserCacheAge = ext switch
                        {
                            ".html" => TimeSpan.FromDays(8),
                            var xt when xt == ".html" || xt == ".htm" => TimeSpan.FromHours(8),    // short cache time for html files (like index.html or when no  that contains the URL links for other JS, CSS files)
                            var xt when xt == ".css" => TimeSpan.FromDays(7),   // median time frames for CSS and JS files. Angular only changes HTML files.
                            var xt when xt == ".js" => TimeSpan.FromDays(7),
                            var xt when xt == ".jpg" || xt == ".jpeg" || xt == ".ico" || xt == ".webp" || xt == ".jxl" || xt == ".avif" => TimeSpan.FromDays(300),      // images files are very long term, long cache time for *.jpg files. assume a year, 31536000 seconds, typically used. They will never change
                            _ => TimeSpan.FromDays(350)
                        };
                    }
                }
                if (maxBrowserCacheAge.TotalSeconds > 0) // if Duration = 0, it will raise exception of "The relative expiration value must be positive. (Parameter 'AbsoluteExpirationRelativeToNow')"
                {
                    Console.WriteLine($"Adding Cache-control to header '{context.Request.Host} {context.Request.Path}'");
                    Utils.Logger.Info($"Adding Cache-control to header '{context.Request.Host} {context.Request.Path}'");
                    context.Response.GetTypedHeaders().CacheControl =
                        new Microsoft.Net.Http.Headers.CacheControlHeaderValue()
                        {
                            Public = true,
                            MaxAge = maxBrowserCacheAge
                        };
                }
            }
            // Vary: User-Agent or Vary: Accept-Encoding is used by intermediate CDN caches (if used, we don't.) It is not necessary to set in direct server to client connection.
            // so the CDN caches differentiate by user-agents or gzip/brotli/noCompressions.
            // StaticFiles(): index.html sets vary: Accept-Encoding, because html can be compressed. Ico/jpeg files are not compressed, so 'vary' is not set in HTTP header.
            // https://blog.stackpath.com/accept-encoding-vary-important/
            // context.Response.Headers[Microsoft.Net.Http.Headers.HeaderNames.Vary] = new string[] { "Accept-Encoding" };

            await next();
        });

        // this is on the fly, just-in-time (JIT) compression. CompressionLevel.Optimal takes 250ms, but CompressionLevel.Fastest takes 4ms time on CPU, but still worth it.
        // app.UseResponseCompression(); // 2022-08: keep in code, but it was commented out when CompressedStaticFileMiddleware started to work. Ahead of time is better than on the fly

        app.UseRouting();

        // UseAuthorization: If execution reaches here and user is not logged in, this will redirect to Google-login (only for [Authorize] attribute Controllers.
        // For normal static files, images, the SqFirewallMiddlewarePostAuth let it pass to the end, because we want to give the user Jpeg files even though it is not logged in.)
        // It is needed for [Authorize] attributes protection, "If the app uses authentication/authorization features such as AuthorizePage or [Authorize],
        // place the call to UseAuthentication and UseAuthorization after UseRouting"
        app.UseAuthorization();

        app.UseEndpoints(endpoints =>
        {
            endpoints.MapControllerRoute(
                name: "default",
                pattern: "{controller}/{action=Index}/{id?}");  // controllers should listen on "/api/" so SubdomainRewriteOptionsRule() can differentiate what to leave as from root and what path to extend
        });

        app.UseMiddleware<SqWebsocketMiddleware>();

        app.UseSqStaticFiles(env); // Enable static files to be served. This would allow html, images, etc. in wwwroot directory to be served.

        app.Use(async (context, next) =>
        {
            Console.WriteLine($"Problem. End of the serving line. Request.Path: '{context.Request.Path.Value}'");
            await next();
        });
    }
}