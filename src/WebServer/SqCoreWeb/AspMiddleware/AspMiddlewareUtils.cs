using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace SqCoreWeb
{
    public static class AspMiddlewareUtils
    {

        // https://stackoverflow.com/questions/50096995/make-asp-net-core-server-kestrel-case-sensitive-on-windows
        // ASP.NET Core apps running in Linux containers use a case sensitive file system, which means that the CSS and JS file references must be case-correct.
        // However, Windows file system is not case sensitive.
        // We recommend a convention for all filenames ("all lowercase" usually works best). We already do have standards to always use lower-case. So, we check that standard.
        // This has to be switched on only on Windows (which is development) 
        public static IApplicationBuilder UseStaticFilesCaseSensitive(this IApplicationBuilder app)
        {
            var fileOptions = new StaticFileOptions
            {
                OnPrepareResponse = x =>
                {
                    if (!File.Exists(x.File.PhysicalPath))
                        return;
                    var requested = x.Context.Request.Path.Value;
                    if (String.IsNullOrEmpty(requested))
                        return;

                    var onDisk = GetExactFullName(new FileInfo(x.File.PhysicalPath)).Replace("\\", "/");

                    //var onDisk = x.File.PhysicalPath.AsFile().GetExactFullName().Replace("\\", "/");
                    if (!onDisk.EndsWith(requested))
                    {
                        throw new Exception("The requested file has incorrect casing and will fail on Linux servers." +
                            Environment.NewLine + "Requested:" + requested + Environment.NewLine +
                            "On disk: " + onDisk.Substring(onDisk.Length - requested.Length));
                    }
                }
            };

            return app.UseStaticFiles(fileOptions);
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
}