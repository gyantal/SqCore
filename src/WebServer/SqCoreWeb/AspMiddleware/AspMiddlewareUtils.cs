using System;
using System.IO;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.Hosting;

namespace SqCoreWeb;

public static class AspMiddlewareUtils
{
    // https://stackoverflow.com/questions/50096995/make-asp-net-core-server-kestrel-case-sensitive-on-windows
    // ASP.NET Core apps running in Linux containers use a case sensitive file system, which means that the CSS and JS file references must be case-correct.
    // However, Windows file system is not case sensitive.
    // We recommend a convention for all filenames ("all lowercase" usually works best). We already do have standards to always use lower-case. So, we check that standard.
    // This has to be switched on only on Windows (which is development)

    public static IApplicationBuilder UseSqStaticFiles(this IApplicationBuilder app, IWebHostEnvironment env)
    {
        // Enable static files to be served. This would allow html, images, etc. in wwwroot directory to be served.
        // The URLs of files exposed using UseDirectoryBrowser and UseStaticFiles are case sensitive and character constrained, subject to the underlying file system.
        // For example, Windows is not case sensitive, but MacOS and Linux are case sensitive.
        // for jpeg files, place UseStaticFiles BEFORE UseRouting
        // Angular apps: in Production they are static files in wwwroot/webapps, served by a brotli capable StaticFiles middleware.
        // In Development Angular apps are 'ng serve'-d in a separate process on a separate port. For 'watch' style Hot Reload development.
        // if (env.IsDevelopment())
        //     app.UseStaticFilesCaseSensitive();  // Force case sensitivity even on Windows
        // else
        //     app.UseStaticFiles(); // on Linux StaticFiles serving is case sensitive, which is good. But not case sensitive on Windows.
        // CompressedStaticFileMiddleware replaces StaticFilesMiddleware. Can serve "*.html" from "*.html.br"

        StaticFileOptions sfOptions = new StaticFileOptions();
        // sfOptions.ServeUnknownFileTypes = true; // Not enough. The Response Header will have missing "Content-Type: image/avif", so browser interprets it as binary file and put *.avif into Download folder instead of using it as an image.
        sfOptions.ContentTypeProvider = new FileExtensionContentTypeProvider // FileExtension => Mime Type mapping. Good that these mappings are additional on the top of the default. So, the [".webp"] = "image/webp" and other defaults still work.
        {
            Mappings =
            {
                [".avif"] = "image/avif"
            }
        };

        if (env.IsDevelopment()) // Force case sensitivity on Windows only. Avoid the CPU overhead on Linux.
        { // on Linux StaticFiles serving is case sensitive by default, which is good. No need for specific path-checking code overhead.
            sfOptions.OnPrepareResponse = x =>
            {
                if (!File.Exists(x.File.PhysicalPath))
                    return;
                var requested = x.Context.Request.Path.Value;
                if (String.IsNullOrEmpty(requested))
                    return;

                var onDisk = GetExactFullName(new FileInfo(x.File.PhysicalPath)).Replace("\\", "/");

                // var onDisk = x.File.PhysicalPath.AsFile().GetExactFullName().Replace("\\", "/");
                if (!onDisk.EndsWith(requested)) // case sensitive match both on Windows and Linux
                {
                    throw new Exception("The requested file has incorrect casing and will fail on Linux servers." +
                        Environment.NewLine + "Requested:" + requested + Environment.NewLine +
                        "On disk: " + onDisk[^requested.Length..]);
                }
            };
        }

        // return app.UseStaticFiles(sfOptions);
        return app.UseCompressedStaticFiles(sfOptions);
    }

    public static string GetExactFullName(this FileSystemInfo p_fsi)
    {
        var path = p_fsi.FullName;
        if (!File.Exists(path) && !Directory.Exists(path))
            return path;

        var asDirectory = new DirectoryInfo(path);
        var parent = asDirectory.Parent;

        if (parent == null) // Drive:
            return asDirectory.Name.ToUpper();

        return Path.Combine(parent.GetExactFullName(), parent.GetFileSystemInfos(asDirectory.Name)[0].Name);
    }
}