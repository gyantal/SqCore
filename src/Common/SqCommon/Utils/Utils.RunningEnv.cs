using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace SqCommon;

public static partial class Utils
{
    // public static bool IsLinux() // use OperatingSystem.IsLinux() and IsWindows from the System namespace

    public static readonly System.Globalization.CultureInfo InvCult = System.Globalization.CultureInfo.InvariantCulture;

    // each class can create its own logger with GetCurrentClassLogger(). That is great for unit testing. However, in production, many small util classes exist. Better to not let them allocate separate loggers, but they have one big common Logger.
    public static NLog.Logger Logger = NLog.LogManager.GetLogger("Sq");   // the name of the logger will be not the "Namespace.Class", but whatever you prefer: "App"
    public static IConfigurationRoot Configuration = new ConfigurationBuilder().Build();    // even small Tools have configs for sensitive data like passwords.
    public static ManualResetEventSlim? MainThreadIsExiting = null;  // broadcast main thread shutdown and give 2 seconds for long running background threads to quit. Some Tools, Apps do not require this, so don't initiate this for them automatically

    public static string RuntimeConfig()
    {
#if RELEASE
        return "RELEASE";
#elif DEBUG
        return "DEBUG";
#else
        return "UNKNOWN RUNTIME-CONFIG";
#endif
    }

    public static bool IsDebugRuntimeConfig()
    {
#if RELEASE
        return false;
#elif DEBUG
        return true;
#else
        return false;
#endif
    }

    public static bool DebuggingEnabled(this NLog.Logger p_logger) // QC integration needed it. There is not yet an Extension Property in C# in 2022, so we have to implement as an Extension method
    {
        return p_logger != null;    // return True always.
    }

    public static string SensitiveConfigFolderPath()
    {
        return Environment.OSVersion.Platform switch
        {
            // return "/home/ubuntu/SQ/Tools/BenchmarkDB/";  // on Linux, sometimes it is not the 'ubuntu' superuser, but something else.
            // GetCurrentDirectory() is the current working directory of the app. Most likely it is the folder of the '*.csproj'.
            // but deployment rm -rf everything until the src folder.
            // return Directory.GetCurrentDirectory() + "/../../.." + "/";
            // return "/home/sq-vnc-client/SQ/NonCommitedSensitiveData/";
            PlatformID.Unix => $"/home/{Environment.UserName}/SQ/NonCommitedSensitiveData/",
            PlatformID.Win32NT => Environment.UserName switch // Windows user name
            {
                // gyantal-PC
                "gyantal" => "h:/.shortcut-targets-by-id/0BzxkV1ug5ZxvVmtic1FsNTM5bHM/GDriveHedgeQuant/shared/GitHubRepos/NonCommitedSensitiveData/",
                // gyantal-Laptop
                "gyant" => "h:/.shortcut-targets-by-id/0BzxkV1ug5ZxvVmtic1FsNTM5bHM/GDriveHedgeQuant/shared/GitHubRepos/NonCommitedSensitiveData/",
                // Balazs-PC
                "Balazs" => "h:/.shortcut-targets-by-id/0BzxkV1ug5ZxvVmtic1FsNTM5bHM/GDriveHedgeQuant/shared/GitHubRepos/NonCommitedSensitiveData/",
                // Balazs laptop
                "Lukucz Balázs" => "g:/.shortcut-targets-by-id/0BzxkV1ug5ZxvVmtic1FsNTM5bHM/GDriveHedgeQuant/shared/GitHubRepos/NonCommitedSensitiveData/",
                "Laci" => "d:/ArchiData/GoogleDrive/GDriveHedgeQuant/shared/GitHubRepos/NonCommitedSensitiveData/",
                "vinci" => "g:/.shortcut-targets-by-id/0BzxkV1ug5ZxvVmtic1FsNTM5bHM/GDriveHedgeQuant/shared/GitHubRepos/NonCommitedSensitiveData/",
                // Daya-Desktop
                "Gigabyte" => "g:/.shortcut-targets-by-id/0BzxkV1ug5ZxvVmtic1FsNTM5bHM/GDriveHedgeQuant/shared/GitHubRepos/NonCommitedSensitiveData/",
                // Drcharmat-Laptop
                "drcharmat" => "c:/Agy/NonCommitedSensitiveData/",
                _ => throw new Exception("Windows user name is not recognized. Add your username and folder here!"),
            }, // find out which user from the team and determine it accordingly. Or just check whether folders exists (but that takes HDD read, which is slow)
            _ => throw new Exception("RunningPlatform() is not recognized"),
        };
    }

    // As Folder separator, try to use the Linux version '/' on both platforms. Even on Windows.
    // Windows has Path.DirectorySeparatorChar = '\\' and Path.AltDirectorySeparatorChar= '/'. So, '/' will work on Windows too.
    public static string FinDataFolderPath
    {
        get
        {
            if (OperatingSystem.IsWindows()) // GetFullPath() removes the unnecessary back marching ".."
                return Path.GetFullPath(AppDomain.CurrentDomain.BaseDirectory + @"../../../../../Fin/Data/");
            else
                return Path.GetFullPath(AppDomain.CurrentDomain.BaseDirectory + @"../FinData/");
        }
    }

    public static string TaskScheduler_UnobservedTaskExceptionMsg(object? p_sender, UnobservedTaskExceptionEventArgs p_e)
    {
        Task? senderTask = (p_sender != null) ? null : p_sender as Task;
        if (senderTask != null)
        {
            string msg = $"Sender is a task. TaskId: {senderTask.Id}, IsCompleted: {senderTask.IsCompleted}, IsCanceled: {senderTask.IsCanceled}, IsFaulted: {senderTask.IsFaulted}, TaskToString(): {senderTask}.";
            msg += (senderTask.Exception == null) ? " SenderTask.Exception is null" : $" SenderTask.Exception {senderTask.Exception.ToStringWithShortenedStackTrace(1600)}";
            return msg;
        }
        else
            return "Sender is not a task.";
    }
}