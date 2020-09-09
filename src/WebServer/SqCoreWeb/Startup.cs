using System;
using System.IO;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.AspNetCore.SpaServices.AngularCli;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.FileProviders;
using Microsoft.AspNetCore.Rewrite;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.ResponseCompression;
using System.Threading.Tasks;
using System.IO.Compression;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using SqCommon;

namespace SqCoreWeb
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            // Asp.Net DependenciInjection (DI) of Kestrel policy for separating the creation of dependencies (IWebHostEnvironment, Options, Logger) from its actual usage in Controllers.
            // That way Controllers are light. And if there is 100 Controller classes, repeating the creation of Dependent objects (IWebHostEnvironment) is not in their source code. So, the source code of Controllers are light.
            // DI is not necessary. DotNet core bases classes doesn't use that for logging or anything. However, Kestrel uses it, which we can honour. It also helps in unit-test. 
            // But it is perfectly fine to do the Creation of dependencies (Logger, like nLog) to do it in the Controller.
            // Transient objects are always different; a new instance is provided to every controller and every service.
            // Scoped objects are the same within a request, but different across different requests
            // Singleton objects are the same for every object and every request(regardless of whether an instance is provided in ConfigureServices)
            services.AddSingleton(_ => Utils.Configuration);  // this is the proper DependenciInjection (DI) way of pushing it as a service to Controllers. So you don't have to manage the creation or disposal of instances.
            services.AddSingleton(_ => Program.g_webAppGlobals);

            services.AddHttpsRedirection(options =>
            {
                options.RedirectStatusCode = StatusCodes.Status307TemporaryRedirect;
                options.HttpsPort = 5001;
            });

            services.AddSignalR();  //  enables bi-directional communication between the browser and server. Based on WebSocket.

            // https://docs.microsoft.com/en-us/aspnet/core/performance/caching/response?view=aspnetcore-3.0
            services.AddResponseCaching(); // DI: these services could be used in MVC control/Razor pages (either as [Attributes], or in code)
            services.AddMvc(options =>     // AddMvc() equals AddControllersWithViews() + AddRazorPages()
            { // this CashProfile is given once here, and if it changes, we only have to change here, not in all Controllers.
                options.CacheProfiles.Add("NoCache",
                    new CacheProfile()
                    {
                        Duration = 0,
                        Location = ResponseCacheLocation.None,
                        NoStore = true
                    });
                options.CacheProfiles.Add("DefaultShortDuration",
                    new CacheProfile()
                    {
                        Duration = 60 * 1   // 1 min for real-time price data
                    });
                options.CacheProfiles.Add("DefaultMidDuration",
                    new CacheProfile()
                    {
                        //Duration = (int)TimeSpan.FromHours(12).TotalSeconds
                        Duration = 100000   // 100,000 seconds = 27 hours
                    });
            }).SetCompatibilityVersion(CompatibilityVersion.Version_3_0);
            //services.AddControllersWithViews();        // AddMvc() equals AddControllersWithViews() + AddRazorPages(), so we don't use Razor pages now.
            // In production, the Angular files (index.html) will be served from this directory, but actually we don't use UseSpaStaticFiles(), so we don't need this here.
            services.AddSpaStaticFiles(configuration =>
            {
                configuration.RootPath = "Angular/dist";
            });

            // https://docs.microsoft.com/en-us/aspnet/core/performance/response-compression
            services.AddResponseCompression(options =>
            {
                options.Providers.Add<BrotliCompressionProvider>();
                options.Providers.Add<GzipCompressionProvider>();
                // Default Mime types: application/javascript, application/json, application/xml, text/css, text/html, text/json, text/plain, text/xml
                options.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat( new[] { "image/svg+xml" });
            });
            services.Configure<BrotliCompressionProviderOptions>(options =>
            {
                options.Level = CompressionLevel.Fastest;
            });

            
            string googleClientId = Utils.Configuration["Google:ClientId"];
            string googleClientSecret = Utils.Configuration["Google:ClientSecret"];
            
            if (!String.IsNullOrEmpty(googleClientId) && !String.IsNullOrEmpty(googleClientSecret))
            {
                // The reason you have BOTH google and cookies Auth is because you're using google for identity information but using cookies for storage of the identity for only asking Google once.
                //So AddIdentity() is not required, but Cookies Yes.
                services.AddAuthentication(options =>
                {
                    // If you don't want the cookie to be automatically authenticated and assigned to HttpContext.User, 
                    // remove the CookieAuthenticationDefaults.AuthenticationScheme parameter passed to AddAuthentication.
                    options.DefaultScheme =  CookieAuthenticationDefaults.AuthenticationScheme;  // For anything else (sign in, sign out, authenticate, forbid), use the cookies scheme
                    options.DefaultChallengeScheme = GoogleDefaults.AuthenticationScheme;   // For challenges, use the google scheme. If not, "InvalidOperationException: No authenticationScheme was specified"

                    options.DefaultAuthenticateScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                    options.DefaultSignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                })
                .AddCookie(o => {  // CookieAuth will be the default from the two, GoogleAuth is used only for Challenge
                    o.LoginPath = "/UserAccount/login";
                    o.LogoutPath = "/UserAccount/logout";

                    // 2020-05-30: WARN|Microsoft.AspNetCore.Authentication.Google.GoogleHandler: '.AspNetCore.Correlation.Google.bzb7A4oxoS_pz_xQk0N4WngqgL0nyLUiT0k5QSPsD_M' cookie not found.
                    // "Exception: Correlation failed.".
                    // Maybe because SameSite cookies policy changed.
                    // I suspect Bunny used an old Chrome or FFox or Edge.
                    // "AspNetCore as a rule does not implement browser sniffing for you because User-Agents values are highly unstable"
                    // However, if updating browser of the user to the latest Chrome doesn't solve it, we may implement these changes:
                    // https://github.com/dotnet/aspnetcore/issues/14996
                    // https://docs.microsoft.com/en-us/aspnet/core/security/samesite?view=aspnetcore-3.1
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

                    // https://docs.microsoft.com/en-us/aspnet/core/security/samesite?view=aspnetcore-3.1
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
                            //context.Response.Redirect(context.RedirectUri + "&hd=" + System.Net.WebUtility.UrlEncode("jerriepelser.com"));
                            context.Response.Redirect(context.RedirectUri, false); //  Temporary redirect response (HTTP 302)
                            return Task.CompletedTask;
                        },
                        OnCreatingTicket = context =>
                        {
                            Utils.Logger.Info("GoogleAuth.OnCreatingTicket(), User: " + context.User);
                            // string email = context.User.Value<Newtonsoft.Json.Linq.JArray>("emails")[0]["value"].ToString();
                            // Utils.Logger.Debug($"[Authorize] attribute forced Google auth. Email:'{email ?? "null"}', RedirectUri: '{context.Properties.RedirectUri ?? "null"}'");

                            // if (!Utils.IsAuthorizedGoogleUsers(Utils.Configuration, email))
                            //     throw new Exception($"Google Authorization Is Required. Your Google account: '{ email }' is not accepted. Logout this Google user and login with another one.");

                            //string domain = context.User.Value<string>("domain");
                            //if (domain != "jerriepelser.com")
                            //    throw new GoogleAuthenticationException("You must sign in with a jerriepelser.com email address");

                            return Task.CompletedTask;
                        },
                        OnTicketReceived = context =>
                        {
                            Utils.Logger.Info("GoogleAuth.OnTicketReceived()");
                            // if this is not set, then the cookie in the browser expires, even though the validation-info in the cookie is still valid. By default, cookies expire: "When the browsing session ends" Expires: 'session'
                            // https://www.jerriepelser.com/blog/managing-session-lifetime-aspnet-core-oauth-providers/
                            context.Properties.IsPersistent = true;
                            context.Properties.ExpiresUtc = DateTimeOffset.UtcNow.AddDays(25);

                            return Task.FromResult(0);
                        },
                        OnRemoteFailure = context =>
                        {
                            Utils.Logger.Info("GoogleAuth.OnRemoteFailure()");
                            return Task.FromResult(0);
                        }
                    };
                });
            }
            else
            {
                Console.WriteLine("A_G_CId and A_G_CSe from Config has NOT been found. Cannot initialize GoogelAuthentication.");
                //Utils.Logger.Warn("A_G_CId and A_G_CSe from Config has NOT been found. Cannot initialize GoogelAuthentication.");
            }

            services.AddHostedService<DashboardPushHubKestrelBckgrndSrv>();

        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            Program.g_webAppGlobals.KestrelEnv = env;

            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();        // in DEBUG it returns a nice webpage that shows the stack trace and everything of the crash.
            }
            else
            {
                app.UseDeveloperExceptionPage();     // it is more useful in the first years of development. A very detailed Exception page can be used even in Production to catch the error quicker.
                //app.UseExceptionHandler("/error.html"); // it hides the crash totally. There is no browser redirection. It returns 'error.html' with status: 200 (OK). Maybe 500 (Error) would be better to return, but then the Browser might not display that page to the user.
                
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

            //app.UseDefaultFiles();      // "UseDefaultFiles is a URL rewriter (default.htm, default.html, index.htm, index.html whichever first, 4 file queries to find the file) that doesn't actually serve the file. "
            app.UseRewriter(new RewriteOptions()
            .AddRewrite(@"^$", "index.html", skipRemainingRules: true)              // empty string converted to index.html. Only 1 query to find the index.html file. Better than UseDefaultFiles()
            .AddRewrite(@"^(.*)/$", "$1/index.html", skipRemainingRules: true)      // converts "/" to "/index.html", e.g. .AddRewrite(@"^HealthMonitor/$", @"HealthMonitor/index.html" and all Angular projects.     
            );

            app.UseMiddleware<SqFirewallMiddlewarePreAuthLogger>();

            // place UseAuthentication() AFTER UseRouting(),  https://docs.microsoft.com/en-us/aspnet/core/migration/22-to-30?view=aspnetcore-2.2&tabs=visual-studio
            app.UseAuthentication();    // // If execution reaches here and user is not logged in, this will redirect to Google-login. It is needed for filling httpContext?.User?.Claims. StaticFiles are served Before the user is login is assured. This is fast, but httpContext?.User?.Claims is null in this case.

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
                ReceiveBufferSize = 4 * 1024                    // default is 4KB.
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
            
            app.Use(async (context, next) =>    // this fills up the Response header Cache-Control for everything else, like static files.
            {
                // main Index.html cache is controlled in SqFirewallMiddlewarePostAuth(), because to differentiate based on Login/Logout
                if (((Program.g_webAppGlobals.KestrelEnv?.EnvironmentName == "Development") || context.Request.Host.Host.StartsWith("sqcore.net")) 
                    && context.Request.Path.Value.Equals("/index.html", StringComparison.OrdinalIgnoreCase))
                {
                    await next();
                    return;
                }

                if (!env.IsDevelopment())   // in development, don't use browser caching at all.
                {
                    // we have to add header Before filling up the response with 'await next();', otherwise 
                    // if we try to add After StaticFiles(), we got exception: "System.InvalidOperationException: Headers are read-only, response has already started."
                    TimeSpan maxBrowserCacheAge = TimeSpan.Zero;
                    if (context.Request.Path.Value.Equals("/index.html", StringComparison.OrdinalIgnoreCase)   // main index.html has Login/username on it. After Login, the page should be refreshed. So, ignore CacheControl for that
                        || context.Request.Path.Value.StartsWith("/hub/") || context.Request.Path.Value.StartsWith("/ws/")   // WebSockets should not be cached
                    )  
                    {
                        maxBrowserCacheAge = TimeSpan.Zero;
                    }
                    else
                    {
                        string ext = Path.GetExtension(context.Request.Path.Value) ?? String.Empty;
                        if (ext != String.Empty)    // If has any extension, then it is not a Controller (but probably a StaticFile()). If it is "/", then it is already converted to "index.htmL". Controllers will handle its own cacheAge with attributes. 
                        {
                            // UseResponseCaching() will fill up headers, if MVC controllers or Razor pages, we don't want to use this caching, because the Controller will specify it in an attribute.
                            // probably no cache for API calls like "https://localhost:5001/WeatherForecast"  (they probably get RT data), Controllers will handle it.
                            maxBrowserCacheAge = (ext) switch
                            {
                                ".html" => TimeSpan.FromDays(8),
                                var xt when xt == ".html" || xt == ".htm" => TimeSpan.FromHours(8),    // short cache time for html files (like index.html or when no  that contains the URL links for other JS, CSS files)                            
                                var xt when xt == ".css" => TimeSpan.FromDays(7),   // median time frames for CSS and JS files. Angular only changes HTML files.
                                var xt when xt == ".js" => TimeSpan.FromDays(7),
                                var xt when xt == ".jpg" || xt == ".jpeg" || xt == ".ico" => TimeSpan.FromDays(300),      // images files are very long term, long cache time for *.jpg files. assume a year, 31536000 seconds, typically used. They will never change
                                _ => TimeSpan.FromDays(350)
                            };
                        }
                    }
                    if (maxBrowserCacheAge.TotalSeconds > 0)    // if Duration = 0, it will raise exception of "The relative expiration value must be positive. (Parameter 'AbsoluteExpirationRelativeToNow')"
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
                //context.Response.Headers[Microsoft.Net.Http.Headers.HeaderNames.Vary] = new string[] { "Accept-Encoding" };

                await next();
            });


            // AddAngularSpaHandlingMiddleware(app, env, "HealthMonitor");     // it doesn't do anything useful now, but might do in the future
            // AddAngularSpaHandlingMiddleware(app, env, "MarketDashboard");   // it doesn't do anything useful now, but might do in the future

            app.UseResponseCompression();       // this is on the fly, just-in-time (JIT) compression. CompressionLevel.Optimal takes 250ms, but CompressionLevel.Fastest takes 4ms time on CPU, but still worth it.


            // var rwOptions = new RewriteOptions();
            // if (env.IsDevelopment())
            // {
            //     rwOptions
            //     .AddRedirect(@"^HealthMonitor", @"dev/DeveloperWarningForServingAngularSPA.html")  // Redirect() forces client to query again.
            //     .AddRedirect(@"^MarketDashboard", @"dev/DeveloperWarningForServingAngularSPA.html");  // Redirect() forces client to query again.
            // }
            // else
            // {
            //     rwOptions
            //     .AddRedirect(@"^HealthMonitor$", @"HealthMonitor/")  // Redirect() forces client to query again (needed for base URL to be the SPA folder)
            //     .AddRewrite(@"^HealthMonitor/$", @"HealthMonitor/index.html", skipRemainingRules: true)   // Rewrite() is hidden from the Client. Helps to find it by UseStaticFiles()
            //     .AddRedirect(@"^MarketDashboard$", @"MarketDashboard/")  // Redirect() forces client to query again (needed for base URL to be the SPA folder)
            //     .AddRewrite(@"^MarketDashboard/$", @"MarketDashboard/index.html", skipRemainingRules: true);   // Rewrite() is hidden from the Client. Helps to find it by UseStaticFiles()
            // }

            // app.UseRewriter(rwOptions);

            // string angularSpaStr = String.Empty;

            // app.Use(async (context, next) =>
            // {
            //     Console.WriteLine($"After UseRewriter():  Request.Path: '{context.Request.Path.Value}'");

            //     if (context.Request.Path.Value.StartsWith("/HealthMonitor/", StringComparison.OrdinalIgnoreCase))
            //         angularSpaStr = "HealthMonitor";
            //     else if (context.Request.Path.Value.StartsWith("/MarketDashboard/", StringComparison.OrdinalIgnoreCase))
            //         angularSpaStr = "MarketDashboard";

            //     if (!String.IsNullOrEmpty(angularSpaStr)) {
            //     Problem, this app.UseStaticFiles() never worked here. It didn't file the files, because it is not in the middleware chain.
            //         context.Request.Path = context.Request.Path.Value.Replace("/" + angularSpaStr, "");
            //         // "Serving UseStaticFiles():  Request.Path: '/HealthMonitor/index.html' in folder:'Angular\dist\HealthMonitor'"
            //         Console.WriteLine($"Serving UseStaticFiles():  Request.Path: '{context.Request.Path.Value}' in folder:'{@"Angular\dist\" + angularSpaStr}'");
            //         app.UseStaticFiles(new StaticFileOptions() { FileProvider = new PhysicalFileProvider(Path.Combine(Directory.GetCurrentDirectory(), @"Angular\dist\" + angularSpaStr))});
            //     }

            //     await next();
            // });


            // app.Map(new PathString("/HealthMonitor"), client =>
            // {
            //     // In Development, when we do 'ng serve' the 'index.hml' should be in the root folder, and all other files (main, pollyfill) should be in the root too. So, only <base href=""> is possible
            //     // Therefor, in Production, index.html should be in the HealthMonitor folder, so browser should ask 'https://localhost:5001/HealthMonitor/' and NOT 'https://localhost:5001/HealthMonitor'
            //     // If browser asks 'https://localhost:5001/HealthMonitor', we should redirect to 'https://localhost:5001/HealthMonitor/'
            //     client.Use(async (context, next) =>
            //     {
            //         Console.WriteLine($"Map.Use: context.Request.Path.Value: '{context.Request.Path.Value}'");

            //         // if (String.IsNullOrEmpty(context.Request.Path.Value))    // turn https://localhost:5001/HealthMonitor   to // turn https://localhost:5001/HealthMonitor/index.html
            //         // {
            //         //     context.Request.Path = "/index.html";
            //         // }
            //         // else if (String.Equals(context.Request.Path.Value, "/index.html", StringComparison.OrdinalIgnoreCase))
            //         //     context.Request.Path = "";

            //         await next();
            //     });

            //     StaticFileOptions clientAppDist = new StaticFileOptions()
            //     {
            //         FileProvider = new PhysicalFileProvider(Path.Combine(Directory.GetCurrentDirectory(), @"Angular\dist\HealthMonitor"))
            //     };
            //     client.UseStaticFiles(clientAppDist);
            // });

            // app.Map(new PathString("/MarketDashboard"), client =>
            // {
            //     StaticFileOptions clientAppDist = new StaticFileOptions()
            //     {
            //         FileProvider = new PhysicalFileProvider(Path.Combine(Directory.GetCurrentDirectory(), @"Angular\dist\MarketDashboard"))
            //     };
            //     client.UseStaticFiles(clientAppDist);
            // });

            // if (!env.IsDevelopment())   // this is not used now.
            // {
            //     app.UseSpaStaticFiles();    // RootPath = "Angular/dist";, this serves https://localhost:5001/HealthMonitor/styles.3ff695c00d717f2d2a11.css , however, it doesn't serve https://localhost:5001/HealthMonitor/index.html
            // }


            // Serve the files Default.htm, default.html, Index.htm, Index.html
            // by default (in that order), i.e., without having to explicitly qualify the URL.
            // For example, if your endpoint is http://localhost:3012/ and wwwroot directory
            // has Index.html, then Index.html will be served when someone hits
            // http://localhost:3012/


            // Enable static files to be served. This would allow html, images, etc. in wwwroot directory to be served. 
            // The URLs of files exposed using UseDirectoryBrowser and UseStaticFiles are case sensitive and character constrained, subject to the underlying file system.
            // For example, Windows is not case sensitive, but MACOS and Linux are case sensitive.
            // for jpeg files, place UseStaticFiles BEFORE UseRouting
            if (env.IsDevelopment())
                app.UseStaticFilesCaseSensitive();
            else 
                app.UseStaticFiles();

            app.UseRouting();

            app.UseAuthorization();     // If execution reaches here and user is not logged in, this will redirect to Google-login. It is needed for [Authorize] attributes protection, "If the app uses authentication/authorization features such as AuthorizePage or [Authorize], place the call to UseAuthentication and UseAuthorization after UseRouting"

            app.UseMiddleware<SqWebsocketMiddleware>();
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapHub<ExSvPushHub>("/hub/exsvpush");
                endpoints.MapHub<DashboardPushHub>("/hub/dashboardpush");
                endpoints.MapControllerRoute(
                    name: "default",
                    pattern: "{controller}/{action=Index}/{id?}");  // controllers should listen on "/api/" so SubdomainRewriteOptionsRule() can differentiate what to leave as from root and what path to extend
            });

            app.Use(async (context, next) =>
            {
                Console.WriteLine($"Problem. End of the serving line. Request.Path: '{context.Request.Path.Value}'");
                await next();
            });

            // https://stackoverflow.com/questions/54582037/asp-net-core-iapplicationbuilder-map-spa-and-static-files
            // the query is: "https://localhost:5001/HealthMonitor/styles.3ff695c00d717f2d2a11.css"
            // this should serve only  https://localhost:5001/HealthMonitor/index.html when client ask  https://localhost:5001/HealthMonitor
            // app.Map(new PathString("/HealthMonitor"), client =>
            // {
            //     Console.WriteLine("app.Map('/HealthMonitor'");

            //     client.Use(async (context, next) =>
            //     {
            //         Console.WriteLine($"Map.Use: context.Request.Path.Value: {context.Request.Path.Value}");

            //         if (context.Request.Path.Value.Equals("/HealthMonitor", StringComparison.OrdinalIgnoreCase))
            //         {
            //             context.Request.Path = "/HealthMonitor/index.html";

            //         }

            //         await next();

            //         // if (context.Response.StatusCode == 404 &&
            //         //     !Path.HasExtension(context.Request.Path.Value) &&
            //         //     !context.Request.Path.Value.StartsWith("/api/"))
            //         // {
            //         //     context.Request.Path = "/angulardev.html";

            //         //     await next();
            //         // }
            //     });

            //     // Each map gets its own physical path for it to map the static files to. 
            //     // StaticFileOptions clientAppDist = new StaticFileOptions()
            //     // {
            //     //     //FileProvider = new PhysicalFileProvider(Path.Combine(Directory.GetCurrentDirectory(), @"Angular\dist\HealthMonitor"))
            //     //     FileProvider = new PhysicalFileProvider(Path.Combine(Directory.GetCurrentDirectory(), @"Angular\dist"))
            //     // };

            //     DefaultFilesOptions clientAppDist2 = new DefaultFilesOptions()
            //     {
            //         //FileProvider = new PhysicalFileProvider(Path.Combine(Directory.GetCurrentDirectory(), @"Angular\dist\HealthMonitor"))
            //         FileProvider = new PhysicalFileProvider(Path.Combine(Directory.GetCurrentDirectory(), @"Angular\dist"))
            //     };


            //     app.UseDefaultFiles(clientAppDist2);        // this is doing Redirects. We don't like the extra query.



            //     //app.UseStaticFiles(clientAppDist);

            //     // Each map its own static files otherwise, it will only ever serve index.html no matter the filename 
            //     //client.UseSpaStaticFiles(clientAppDist); // allowFallbackOnServingWebRootFiles: false //  if only index.html served, check in index.html the relative path: <base href="MarketDashboard/">

            //     // // Each map will call its own UseSpa where we give its own sourcepath
            //     // client.UseSpa(spa =>   // allowFallbackOnServingWebRootFiles: true     // Strange. Without this, it doesn't work !!!.
            //     // {
            //     //     spa.Options.SourcePath = @"Angular\projects\HealthMonitor";
            //     //     spa.Options.DefaultPageStaticFileOptions = clientAppDist;
            //     // });
            // });



            // // https://stackoverflow.com/questions/54582037/asp-net-core-iapplicationbuilder-map-spa-and-static-files
            // // the query is: "https://localhost:5001/HealthMonitor/styles.3ff695c00d717f2d2a11.css"
            // app.Map(new PathString("/HealthMonitor"), client =>
            // {
            //     Console.WriteLine("app.Map('/HealthMonitor'");
            //     // Each map gets its own physical path for it to map the static files to. 
            //     StaticFileOptions clientAppDist = new StaticFileOptions()
            //     {
            //         //FileProvider = new PhysicalFileProvider(Path.Combine(Directory.GetCurrentDirectory(), @"Angular\dist\HealthMonitor"))
            //         FileProvider = new PhysicalFileProvider(Path.Combine(Directory.GetCurrentDirectory(), @"Angular\dist"))
            //     };

            //     // Each map its own static files otherwise, it will only ever serve index.html no matter the filename 
            //     client.UseSpaStaticFiles(clientAppDist); // allowFallbackOnServingWebRootFiles: false //  if only index.html served, check in index.html the relative path: <base href="MarketDashboard/">

            //     // // Each map will call its own UseSpa where we give its own sourcepath
            //     // client.UseSpa(spa =>   // allowFallbackOnServingWebRootFiles: true     // Strange. Without this, it doesn't work !!!.
            //     // {
            //     //     spa.Options.SourcePath = @"Angular\projects\HealthMonitor";
            //     //     spa.Options.DefaultPageStaticFileOptions = clientAppDist;
            //     // });
            // });

            // app.Map("/HealthMonitor", app1 =>
            // {
            //     Console.WriteLine("app.Map('/HealthMonitor', app1 ");
            //     app1.UseSpaStaticFiles(new StaticFileOptions
            //     {
            //         RequestPath = "/HealthMonitor"
            //     });
                

            //     app1.UseSpa(spa =>
            //     {
            //         // To learn more about options for serving an Angular SPA from ASP.NET Core,
            //         // see https://go.microsoft.com/fwlink/?linkid=864501

            //         spa.Options.SourcePath = "Angular";     // "Gets or sets the path, relative to the application working directory, of the directory that contains the SPA source files during development. The directory may not exist in published applications."

            //     });
            // });

            // app.UseSpa(spa =>
            // {
            //     // To learn more about options for serving an Angular SPA from ASP.NET Core,
            //     // see https://go.microsoft.com/fwlink/?linkid=864501

            //     spa.Options.SourcePath = "Angular";     // "Gets or sets the path, relative to the application working directory, of the directory that contains the SPA source files during development. The directory may not exist in published applications."

            //     if (env.IsDevelopment())
            //     {
            //         spa.UseAngularCliServer(npmScript: "start");
            //     }
            // });
        }


        // it doesn't do anything useful now, but might do in the future
        // private void AddAngularSpaHandlingMiddleware(IApplicationBuilder app, IWebHostEnvironment env, string spaName)
        // {
        //     var rwOptions = new RewriteOptions();
        //     if (env.IsDevelopment())
        //     {
        //         // rwOptions
        //         // .AddRedirect(@"^webapps/"+spaName, @"dev/DeveloperWarningForServingAngularSPA.html")  // Redirect() forces client to query again.
        //         // .AddRewrite(@"^webapps/forced/"+spaName, "webapps/" + spaName + @"/index.html", skipRemainingRules: true);  // just in case Developer in Debug wants to access the Prod version of the Angular app
        //     }
        //     else
        //     {
        //         rwOptions
        //         .AddRedirect(@"^" + spaName +"$", "webapps/" + spaName + @"/");  // Redirect("HealthMonitor" to "webapps/HealthMonitor/") forces client to query again (needed for base URL to be the SPA folder)
        //         //.AddRewrite(@"^" + spaName + "/$", spaName + @"/index.html", skipRemainingRules: true);   // Rewrite("HealthMonitor/" to "HealthMonitor/index.html") is hidden from the Client. Helps to find it by UseStaticFiles()
        //     }
        //     app.UseRewriter(rwOptions);

        //     // app.Map(new PathString("/" + spaName), client =>
        //     // {
        //     //     // In Development, when we do 'ng serve' the 'index.hml' should be in the root folder, and all other files (main, pollyfill) should be in the root too. So, only <base href=""> is possible
        //     //     // Therefor, in Production, index.html should be in the HealthMonitor folder, so browser should ask 'https://localhost:5001/HealthMonitor/' and NOT 'https://localhost:5001/HealthMonitor'
        //     //     // If browser asks 'https://localhost:5001/HealthMonitor', we should redirect to 'https://localhost:5001/HealthMonitor/'
        //     //     client.Use(async (context, next) =>
        //     //     {
        //     //         Console.WriteLine($"Map.Use({"/" + spaName}): context.Request.Path.Value: '{context.Request.Path.Value}'");

        //     //         // if (String.IsNullOrEmpty(context.Request.Path.Value))    // turn https://localhost:5001/HealthMonitor   to // turn https://localhost:5001/HealthMonitor/index.html
        //     //         // {
        //     //         //     context.Request.Path = "/index.html";
        //     //         // }
        //     //         // else if (String.Equals(context.Request.Path.Value, "/index.html", StringComparison.OrdinalIgnoreCase))
        //     //         //     context.Request.Path = "";

        //     //         await next();
        //     //     });

        //     //     StaticFileOptions clientAppDist = new StaticFileOptions()
        //     //     {
        //     //         FileProvider = new PhysicalFileProvider(Path.Combine(Directory.GetCurrentDirectory(), @"Angular/dist/" + spaName))   // "Angular\dist\" is Windows like, not good. Use forward slash for Linux.
        //     //     };
        //     //     //client.UseStaticFiles(clientAppDist);
        //     //     client.UseCompressedStaticFiles(clientAppDist);
        //     // });
        // }
    }
}
