using System;
using System.Net;
using System.Threading;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NLog.Web;
using SqCommon;
using System.Diagnostics;

// Kestrel is a cross-platform web server for ASP.NET Core. Kestrel is the web server that's included and enabled by default in ASP.NET Core
namespace SqCoreWeb;

public partial class Program
{
static readonly CancellationTokenSource gKestrelCancelTs = new();
public static void KestrelWebServer_Init()
{
}

public static void KestrelWebServer_Run(string[] args)
{
    // ASP .NET 6 new minimal hosting model: merging Program.cs and Startup.cs : NO, We don't have to use it.
    // https://stackoverflow.com/questions/71895364/why-migrate-to-the-asp-net-core-6-minimal-hosting-model "Why migrate to the ASP.NET Core 6 minimal hosting model?"
    // https://docs.microsoft.com/en-us/aspnet/core/migration/50-to-60-samples?view=aspnetcore-6.0
    // "Unifies Startup.cs and Program.cs into a single Program.cs file., that's a disadvantage. "
    // In SqCore the server Program.cs should not be unified with the Kestrel code. SqCoreServer does many things. HealthMonitor, VBroker, Database functionalities.
    // The WebServer functionality is only 1 function. We should not unify it.
    // Maybe we will migrate in the future, but in 2022 we keep the .NET 5 version and let's see which will be more popular by the community
    try
    {
        IHost host = CreateHostBuilder(args).Build();
        // in VsCode F5 Debug: launching a web browser works by finding a pattern in the DebugConsole (not in the real external console), but EXE is launched in separate "console": "externalTerminal"
        Debug.WriteLine("Now listening on: https://127.0.0.1:5001");
        host.RunAsync(gKestrelCancelTs.Token);     // without await, it returns instantly, running in threadpool
    }
    catch (Exception e)
    {
        Utils.Logger.Error(e, "Exception in Kestrel webserver thread.");
    }
    finally
    {
    }
}

public static void KestrelWebServer_Stop()
{
    gKestrelCancelTs.Cancel();
}


public static void KestrelWebServer_Exit()
{
}


public static IHostBuilder CreateHostBuilder(string[] args) =>
    Host.CreateDefaultBuilder(args)
    .ConfigureWebHostDefaults(webBuilder =>
    {
        webBuilder.ConfigureKestrel(serverOptions =>
        {
            // Because of Google Auth cookie usage and Chrome SameSite policy, use only the HTTPS protocol (no HTTP), even in local development. See explanation at Google Auth code. Also HTTP/3 requires only HTTPS
            // Safe to leave ports 5000, 5001 on IPAddress.Loopback (localhost), because localhost can be accessed only from local machine. From the public web, the ports 5000, 5001 is not accessable.
            // See cookies: Facebook and Google logins only work in HTTPS (even locally), and because we want in Local development the same experience (user email info) as is production, we eliminate HTTP in local development
            // serverOptions.Listen(IPAddress.Loopback, 5000); // 2020-10: HTTP still works for basic things // In IPv4, 127.0.0.1 is the most commonly used loopback address, in IP6, it is [::1],  "localhost" means either 127.0.0.1 or  [::1]

            string pfxFullPath = Utils.SensitiveConfigFolderPath() + $"sqcore.net.merged_pubCert_privKey.pfx";
            Utils.Logger.Info($"Pfx file: " + pfxFullPath);
            serverOptions.Listen(IPAddress.Loopback /* '127.0.0.1' (it is not 'localhost') */, 5001, listenOptions => // On Linux server: only 'localhost:5001' is opened, but '<PublicIP>:5001>' is not. We would need PublicAny for that. But for security, it is fine.
            {
                    // On Linux, "default developer certificate could not be found or is out of date. ". Uncommenting this solved the problem temporarily.
                    // Exception: 'System.InvalidOperationException: Unable to configure HTTPS endpoint. No server certificate was specified, and the default developer certificate could not be found or is out of date.
                    // To generate a developer certificate run 'dotnet dev-certs https'. To trust the certificate (Windows and macOS only) run 'dotnet dev-certs https --trust'.
                    // For more information on configuring HTTPS see https://go.microsoft.com/fwlink/?linkid=848054.
                    //    at Microsoft.AspNetCore.Hosting.ListenOptionsHttpsExtensions.UseHttps(ListenOptions listenOptions, Action`1 configureOptions)

                    // https://go.microsoft.com/fwlink/?linkid=848054
                    // https://stackoverflow.com/questions/53300480/unable-to-configure-https-endpoint-no-server-certificate-was-specified-and-the
                    // The .NET Core SDK includes an HTTPS development certificate. The certificate is installed as part of the first-run experience.
                    // But that cert expires after about 6 month. Its expiration can be followed in certmgr.msc/Trusted Root Certification Authorities/Certificates/localhost/Expiration Date.
                    // Cleaning (dotnet dev-certs https --clean) and recreating (dotnet dev-certs https -t) it both on Win/Linux would work, but it has to be done every 6 months.
                    // Better once and for all solution to use the live certificate of SqCore.net even in localhost in Development.

                    // listenOptions.UseHttps();   // Configure Kestrel to use HTTPS with the default certificate for the domain (certmgr.msc/Trusted Root Certification Authorities/Certificates). This is usually the .NET Core SDK includes an HTTPS development certificate, which expires in every 6 months. Throws an exception if no default certificate is configured.
                    listenOptions.UseHttps(pfxFullPath, @"haha");
            });

            // from the public web only port 443 is accessable. However, on that port, both HTTP and HTTPS traffic is allowed. Although we redirect HTTP to HTTPS later.
            serverOptions.ListenAnyIP(443, listenOptions => // Both 'localhost:443' and '<PublicIP>:443>' is listened on Linux server.
            {
                listenOptions.UseHttps(pfxFullPath, @"haha");
                // Future: One Kestrel server can support many domains (sqcore.net, www.snifferquant.net, xyz.com). In that case, different certs are needed based on connectionContext
                // We don't actually need all of these. Because the wildcart cert is both root and subdomain 'checked by 'certbot certificates''. So, don't need branching here based on context.
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
            if (Utils.IsDebugRuntimeConfig())
            {
                // logging.AddConsole();   // in VsCode at F5: ASP.NET Core logs appears in normal console.
                logging.AddDebug();  // in VsCode at F5: ASP.NET Core logs appears in Debug console.
                logging.SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Trace);
            }
            else
            {
                // in production, logging slows down.
                logging.SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Warning);
            }
        })
        .UseNLog();  // NLog: Setup NLog for Dependency injection; LoggerProvider under the ASP.NET Core platform.
    });
}