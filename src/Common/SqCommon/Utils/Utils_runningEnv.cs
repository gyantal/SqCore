using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace SqCommon
{
    public enum Platform
    {
        Windows,
        Linux,
        Mac
    }

    public static partial class Utils
    {
        public static readonly System.Globalization.CultureInfo InvCult = System.Globalization.CultureInfo.InvariantCulture;

        // each class can create its own logger with GetCurrentClassLogger(). That is great for unit testing. However, in production, many small util classes exist. Better to not let them allocate separate loggers, but they have one big common Logger.
        public static NLog.Logger Logger = NLog.LogManager.GetLogger("Sq");   // the name of the logger will be not the "Namespace.Class", but whatever you prefer: "App"
        public static IConfigurationRoot Configuration = new ConfigurationBuilder().Build();    // even small Tools have configs for sensitive data like passwords.
        public static ManualResetEventSlim? MainThreadIsExiting = null;  // broadcast main thread shutdown and give 2 seconds for long running background threads to quit. Some Tools, Apps do not require this, so don't initiate this for them automatically

        // see discussion here in CoreCLR (they are not ready) https://github.com/dotnet/corefx/issues/1017
        public static Platform RunningPlatform()
        {
            return Environment.NewLine switch
            {
                "\n" => Platform.Linux,
                "\r\n" => Platform.Windows,
                _ => throw new Exception("RunningPlatform() not recognized"),
            };
        }

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

        // public enum RunningEnvStrType
        // {
        //     Unknown,
        //     NonCommitedSensitiveDataFullPath,
        //     HttpsCertificateFullPath
        // }

        // static Dictionary<RunningEnvStrType, Dictionary<string, string>> RunningEnvStrDict = new Dictionary<RunningEnvStrType, Dictionary<string, string>>()
        // {
        //      { RunningEnvStrType.NonCommitedSensitiveDataFullPath,
        //         new Dictionary<string, string>()
        //         {
        //             { "Lin.*", "/home/ubuntu/SQ/WebServer/SQLab/SQLab.WebServer.SQLab.NoGitHub.json" },
        //             { "Win.gyantal", "c:/agy/Google Drive/GDriveHedgeQuant/shared/GitHubRepos/NonCommitedSensitiveData/" }
        //         }
        //     },
        //     { RunningEnvStrType.HttpsCertificateFullPath,
        //         new Dictionary<string, string>()
        //         {
        //             { "Lin.*", "/home/sq-vnc-client/SQ/WebServer/SqCoreWeb/merged_pubCert_privKey_pwd_haha.pfx" },
        //             { "Win.gyantal", @"g:\agy\myknowledge\programming\_ASP.NET\https cert\letsencrypt Folder from Ubuntu\letsencrypt\live\sqcore.net\merged_pubCert_privKey_pwd_haha.pfx" }
        //         }
        //     }
        // };
        // public static string RunningEnvStr(RunningEnvStrType p_runningEnvStrType)
        // {
        //     string os_username = (RunningPlatform() == Platform.Linux) ? "Lin.*" : "Win." + Environment.UserName);
        //     if (RunningEnvStrDict.TryGetValue(p_runningEnvStrType, out Dictionary<string, string> dictRe))
        //     {
        //         if (dictRe.TryGetValue(os_username, out string str))
        //         {
        //             return str;
        //         }
        //     }
        //     Utils.Logger.Error("Error in RunningEnvStr(). Couldn't find: " + os_username + ". Returning null.");
        //     return string.Empty;
        // }

        public static string SensitiveConfigFolderPath()
        {
            return RunningPlatform() switch
            {
                // return "/home/ubuntu/SQ/Tools/BenchmarkDB/";  // on Linux, sometimes it is not the 'ubuntu' superuser, but something else.
                // GetCurrentDirectory() is the current working directory of the app. Most likely it is the folder of the '*.csproj'.
                // but deployment rm -rf everything until the src folder.
                // return Directory.GetCurrentDirectory() + "/../../.." + "/";
                // return "/home/sq-vnc-client/SQ/NonCommitedSensitiveData/";
                Platform.Linux => $"/home/{Environment.UserName}/SQ/NonCommitedSensitiveData/",
                Platform.Windows => Environment.UserName switch // Windows user name
                {
                    // gyantal-PC
                    "gyantal" => "h:/.shortcut-targets-by-id/0BzxkV1ug5ZxvVmtic1FsNTM5bHM/GDriveHedgeQuant/shared/GitHubRepos/NonCommitedSensitiveData/",
                    // gyantal-Laptop
                    "gyant" => "h:/.shortcut-targets-by-id/0BzxkV1ug5ZxvVmtic1FsNTM5bHM/GDriveHedgeQuant/shared/GitHubRepos/NonCommitedSensitiveData/",
                    "Balazs" => "h:/.shortcut-targets-by-id/0BzxkV1ug5ZxvVmtic1FsNTM5bHM/GDriveHedgeQuant/shared/GitHubRepos/NonCommitedSensitiveData/",
                    "Laci" => "d:\\ArchiData\\GoogleDrive\\GDriveHedgeQuant\\shared\\GitHubRepos\\NonCommitedSensitiveData\\",
                    "vinci" => "g:\\.shortcut-targets-by-id\\0BzxkV1ug5ZxvVmtic1FsNTM5bHM\\GDriveHedgeQuant\\shared\\GitHubRepos\\NonCommitedSensitiveData\\",
                    _ => throw new Exception("Windows user name is not recognized. Add your username and folder here!"),
                }, // find out which user from the team and determine it accordingly. Or just check whether folders exists (but that takes HDD read, which is slow)
                _ => throw new Exception("RunningPlatform() is not recognized"),
            };
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
}