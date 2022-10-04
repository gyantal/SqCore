// Use CompressedStaticMiddleWare.cs that replaces StaticFiles. Because precompiled brotli is much smaller (can be half size) than on-the-fly brotli (app.UseResponseCompression()) done by the current Kestrel pipeline.
// SqStudiesList.html. Uncompressed: 80KB, Kestrel's on-the-fly brotli compression (Fastest): 21KB, Precompiled brotli (served by ompressedStaticMiddleWare): 12KB
// And it would also save disk-reading time and CPU-compression time if we use the preCompiled brotli files.

// https://github.com/AnderssonPeter/CompressedStaticFiles
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace SqCoreWeb;

public interface IFileAlternative
{
    long Size { get; }
    /// <summary>
    /// Used to give some files a higher priority
    /// </summary>
    float Cost { get; }
    void Apply(HttpContext context);
    void Prepare(IContentTypeProvider contentTypeProvider, StaticFileResponseContext staticFileResponseContext);
}

public interface IAlternativeFileProvider
{
    void Initialize(FileExtensionContentTypeProvider fileExtensionContentTypeProvider);
    IFileAlternative? GetAlternative(HttpContext context, IFileProvider fileSystem, IFileInfo originalFile);
}

internal static class LoggerExtensions
{
    private static readonly Action<ILogger, string, string, long, long, Exception?> _logFileServed;

    static LoggerExtensions()
    {
        _logFileServed = LoggerMessage.Define<string, string, long, long>(
           logLevel: LogLevel.Information,
           eventId: 1,
           formatString: "Sending file. Request file: '{RequestedPath}'. Served file: '{ServedPath}'. Original file size: {OriginalFileSize}. Served file size: {ServedFileSize}");
    }

    public static void LogFileServed(this ILogger logger, string requestedPath, string servedPath, long originalFileSize, long servedFileSize)
    {
        if (string.IsNullOrEmpty(requestedPath))
        {
            throw new ArgumentNullException(nameof(requestedPath));
        }
        if (string.IsNullOrEmpty(servedPath))
        {
            throw new ArgumentNullException(nameof(servedPath));
        }
        _logFileServed(logger, requestedPath, servedPath, originalFileSize, servedFileSize, null);
    }
}

public class CompressedAlternativeFile : IFileAlternative
{
    private readonly ILogger logger;
    private readonly IFileInfo originalFile;
    private readonly IFileInfo alternativeFile;

    public CompressedAlternativeFile(ILogger logger, IFileInfo originalFile, IFileInfo alternativeFile)
    {
        this.logger = logger;
        this.originalFile = originalFile;
        this.alternativeFile = alternativeFile;
    }

    public long Size => alternativeFile.Length;

    public float Cost => Size;

    public void Apply(HttpContext context)
    {
        var matchedPath = context.Request.Path.Value + Path.GetExtension(alternativeFile.Name);
        logger.LogFileServed(context.Request.Path.Value ?? "UnknownRequestPath", matchedPath, originalFile.Length, alternativeFile.Length);
        context.Request.Path = new PathString(matchedPath);
    }

    public void Prepare(IContentTypeProvider contentTypeProvider, StaticFileResponseContext staticFileResponseContext)
    {
        foreach (var compressionType in CompressedAlternativeFileProvider.CompressionTypes.Keys)
        {
            var fileExtension = CompressedAlternativeFileProvider.CompressionTypes[compressionType];
            if (staticFileResponseContext.File.Name.EndsWith(fileExtension, StringComparison.OrdinalIgnoreCase))
            {
                // we need to restore the original content type, otherwise it would be based on the compression type
                // (for example "application/brotli" instead of "text/html")
                if (contentTypeProvider.TryGetContentType(staticFileResponseContext.File.PhysicalPath.Remove(
                    staticFileResponseContext.File.PhysicalPath.Length - fileExtension.Length, fileExtension.Length), out var contentType))
                    staticFileResponseContext.Context.Response.ContentType = contentType;
                staticFileResponseContext.Context.Response.Headers.Add("Content-Encoding", new[] { compressionType });
            }
        }
    }
}

public class CompressedAlternativeFileProvider : IAlternativeFileProvider
{
    internal static Dictionary<string, string> CompressionTypes =
        new()
        {
                { "gzip", ".gz" },
                { "br", ".br" }
        };

    private readonly ILogger logger;
    private readonly IOptions<CompressedStaticFileOptions> options;

    public CompressedAlternativeFileProvider(ILogger<CompressedAlternativeFileProvider> logger, IOptions<CompressedStaticFileOptions> options)
    {
        this.logger = logger;
        this.options = options;
    }

    public void Initialize(FileExtensionContentTypeProvider fileExtensionContentTypeProvider)
    {
        // the StaticFileProvider would not serve the file if it does not know the content-type
        fileExtensionContentTypeProvider.Mappings[".br"] = "application/brotli";
    }

    /// <summary>
    /// Find the encodings that are supported by the browser and by this middleware
    /// </summary>
    private static IEnumerable<string> GetSupportedEncodings(HttpContext context)
    {
        var browserSupportedCompressionTypes = context.Request.Headers.GetCommaSeparatedValues("Accept-Encoding");
        var validCompressionTypes = CompressionTypes.Keys.Intersect(browserSupportedCompressionTypes, StringComparer.OrdinalIgnoreCase);
        return validCompressionTypes;
    }

    public IFileAlternative? GetAlternative(HttpContext context, IFileProvider fileSystem, IFileInfo originalFile)
    {
        if (!options.Value.EnablePrecompressedFiles)
        {
            return null;
        }
        var supportedEncodings = GetSupportedEncodings(context);
        IFileInfo matchedFile = originalFile;
        foreach (var compressionType in supportedEncodings)
        {
            var fileExtension = CompressionTypes[compressionType];
            var file = fileSystem.GetFileInfo(context.Request.Path + fileExtension);
            if (file.Exists && file.Length < matchedFile.Length)
            {
                matchedFile = file;
            }
        }

        if (matchedFile != originalFile)
        {
            // a compressed version exists and is smaller, change the path to serve the compressed file
            // var matchedPath = context.Request.Path.Value + Path.GetExtension(matchedFile.Name); // was not used
            return new CompressedAlternativeFile(logger, originalFile, matchedFile);
        }
        return null;
    }
}

public class CompressedStaticFileOptions
{
    public bool EnablePrecompressedFiles { get; set; } = true;
    public bool EnableImageSubstitution { get; set; } = true;

    /// <summary>
    /// Used to prioritize image formats that contain higher quality per byte, if only size should be considered remove all entries.
    /// </summary>
    public Dictionary<string, float> ImageSubstitutionCostRatio { get; set; } = new Dictionary<string, float>()
        {
            { "image/bmp", 2 },
            { "image/tiff", 1 },
            { "image/gif", 1 },
            { "image/apng", 0.9f },
            { "image/png", 0.9f },
            { "image/webp", 0.9f },
            { "image/avif", 0.8f }
        };
}

public class CompressedStaticFileMiddleware
{
    private readonly AsyncLocal<IFileAlternative> alternativeFile = new();
    private readonly IOptions<StaticFileOptions> _staticFileOptions;
    private readonly IEnumerable<IAlternativeFileProvider> alternativeFileProviders;
    private readonly StaticFileMiddleware _base;
    // private readonly ILogger logger;

    public CompressedStaticFileMiddleware(
        RequestDelegate next,
        IWebHostEnvironment hostingEnv,
        IOptions<StaticFileOptions> staticFileOptions, IOptions<CompressedStaticFileOptions> compressedStaticFileOptions, ILoggerFactory loggerFactory, IEnumerable<IAlternativeFileProvider> alternativeFileProviders)
    {
        if (next == null)
        {
            throw new ArgumentNullException(nameof(next));
        }

        if (loggerFactory == null)
        {
            throw new ArgumentNullException(nameof(loggerFactory));
        }

        if (compressedStaticFileOptions == null) // to remove Warning of: "Remove unused parameter 'compressedStaticFileOptions'
        {
            throw new ArgumentNullException(nameof(compressedStaticFileOptions));
        }
        if (hostingEnv == null)
        {
            throw new ArgumentNullException(nameof(hostingEnv));
        }
        if (!alternativeFileProviders.Any())
        {
            throw new Exception("No IAlternativeFileProviders where found, did you forget to add AddCompressedStaticFiles() in ConfigureServices?");
        }
        // logger = loggerFactory.CreateLogger<CompressedStaticFileMiddleware>();


        this._staticFileOptions = staticFileOptions ?? throw new ArgumentNullException(nameof(staticFileOptions));
        this.alternativeFileProviders = alternativeFileProviders;
        InitializeStaticFileOptions(hostingEnv, staticFileOptions);

        _base = new StaticFileMiddleware(next, hostingEnv, staticFileOptions, loggerFactory);
    }

    private void InitializeStaticFileOptions(IWebHostEnvironment hostingEnv, IOptions<StaticFileOptions> staticFileOptions)
    {
        staticFileOptions.Value.FileProvider = staticFileOptions.Value.FileProvider ?? hostingEnv.WebRootFileProvider;
        var contentTypeProvider = staticFileOptions.Value.ContentTypeProvider ?? new FileExtensionContentTypeProvider();
        if (contentTypeProvider is FileExtensionContentTypeProvider fileExtensionContentTypeProvider)
        {
            foreach (var alternativeFileProvider in alternativeFileProviders)
            {
                alternativeFileProvider.Initialize(fileExtensionContentTypeProvider);
            }

        }
        staticFileOptions.Value.ContentTypeProvider = contentTypeProvider;

        var originalPrepareResponse = staticFileOptions.Value.OnPrepareResponse;
        staticFileOptions.Value.OnPrepareResponse = context =>
        {
            originalPrepareResponse(context);
            var alternativeFile = this.alternativeFile.Value;
            if (alternativeFile != null)
            {
                alternativeFile.Prepare(contentTypeProvider, context);
            }

        };
    }

    public Task Invoke(HttpContext context)
    {
        if (context.Request.Path.HasValue)
        {
            ProcessRequest(context);
        }
        return _base.Invoke(context);
    }

    private void ProcessRequest(HttpContext context)
    {
        var fileSystem = _staticFileOptions.Value.FileProvider;
        if (fileSystem == null)
            return;
        var originalFile = fileSystem.GetFileInfo(context.Request.Path);

        if (!originalFile.Exists || originalFile.IsDirectory)
        {
            return;
        }

        // Find the smallest file from all our alternative file providers
        var smallestAlternativeFile = alternativeFileProviders.Select(alternativeFileProvider => alternativeFileProvider.GetAlternative(context, fileSystem, originalFile))
                                                              .Where(af => af != null)
                                                              .OrderBy(alternativeFile => alternativeFile?.Cost)
                                                              .FirstOrDefault();
        if (smallestAlternativeFile != null)
        {
            smallestAlternativeFile.Apply(context);
            alternativeFile.Value = smallestAlternativeFile;
        }
    }
}

public static class CompressedStaticFileExtensions
{
    public static CompressedStaticFileOptions RemoveImageSubstitutionCostRatio(this CompressedStaticFileOptions compressedStaticFileOptions)
    {
        compressedStaticFileOptions.ImageSubstitutionCostRatio.Clear();
        return compressedStaticFileOptions;
    }

    public static IServiceCollection AddCompressedStaticFiles(this IServiceCollection services)
    {
        services.AddSingleton<IAlternativeFileProvider, CompressedAlternativeFileProvider>();
        // services.AddSingleton<IAlternativeFileProvider, AlternativeImageFileProvider>(); // 2022-08: decided to not use their image file swapping solution
        return services;
    }

    public static IServiceCollection AddCompressedStaticFiles(this IServiceCollection services, Action<CompressedStaticFileOptions> configureOptions)
    {
        services.Configure(configureOptions);
        services.AddSingleton<IAlternativeFileProvider, CompressedAlternativeFileProvider>();
        // services.AddSingleton<IAlternativeFileProvider, AlternativeImageFileProvider>(); // 2022-08: decided to not use their image file swapping solution
        return services;
    }

    public static IApplicationBuilder UseCompressedStaticFiles(this IApplicationBuilder app)
    {
        if (app == null)
        {
            throw new ArgumentNullException(nameof(app));
        }

        return app.UseMiddleware<CompressedStaticFileMiddleware>();
    }

    public static IApplicationBuilder UseCompressedStaticFiles(this IApplicationBuilder app, StaticFileOptions staticFileOptions) // staticFileOptions specify if it is case sensitive or not
    {
        if (app == null)
        {
            throw new ArgumentNullException(nameof(app));
        }

        return app.UseMiddleware<CompressedStaticFileMiddleware>(Options.Create(staticFileOptions));
    }
}