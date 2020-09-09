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
        public static ManualResetEventSlim? MainThreadIsExiting = null;  // some Tools, Apps do not require this, so don't initiate this for them automatically


        // see discussion here in CoreCLR (they are not ready) https://github.com/dotnet/corefx/issues/1017
        public static Platform RunningPlatform()
        {
            switch (Environment.NewLine)
            {
                case "\n":
                    return Platform.Linux;

                case "\r\n":
                    return Platform.Windows;

                default:
                    throw new Exception("RunningPlatform() not recognized");
            }
        }

        public static string RuntimeConfig()
        {
#if RELEASE
            return "RELEASE";
#elif DEBUG
            return "DEBUG";
#elif DEBUG
            return "UNKNOWN RUNTIME-CONFIG";
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
        //             { "Win.gyantal", "g:/agy/Google Drive/GDriveHedgeQuant/shared/GitHubRepos/NonCommitedSensitiveData/" }
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
        //     return String.Empty;
        // }

        public static string SensitiveConfigFolderPath()
        {
            switch (RunningPlatform())
            {
                case Platform.Linux:
                    //return "/home/ubuntu/SQ/Tools/BenchmarkDB/";  // on Linux, sometimes it is not the 'ubuntu' superuser, but something else.
                    // GetCurrentDirectory() is the current working directory of the app. Most likely it is the folder of the '*.csproj'.
                    // but deployment rm -rf everything until the src folder.
                    //return Directory.GetCurrentDirectory() + "/../../.." + "/";
                    return "/home/sq-vnc-client/SQ/NonCommitedSensitiveData/";

               case Platform.Windows:
                    // find out which user from the team and determine it accordingly. Or just check whether folders exists (but that takes HDD read, which is slow)
                    switch (Environment.UserName)   // Windows user name
                    {
                        case "gyantal":
                            return "g:/agy/Google Drive/GDriveHedgeQuant/shared/GitHubRepos/NonCommitedSensitiveData/";
                        case "Balazs":
                            return "d:/GDrive/GDriveHedgeQuant/shared/GitHubRepos/NonCommitedSensitiveData/";
                        case "Laci":
                            return "d:\\ArchiData\\GoogleDrive\\GDriveHedgeQuant\\shared\\GitHubRepos\\NonCommitedSensitiveData\\";
                        default:
                            throw new Exception("Windows user name is not recognized. Add your username and folder here!");
                    }
                default:
                    throw new Exception("RunningPlatform() is not recognized");
            }
        }

        // https://stackoverflow.com/questions/22629951/suppressing-warning-cs4014-because-this-call-is-not-awaited-execution-of-the
        public static void FireParallelAndForgetAndLogErrorTask(this Task task)
        {
            // task is called without await, so it doesn't wait; it will run parallel. "await task.ContinueWith()" would wait the task
            task.ContinueWith(
                t => { Utils.Logger.Error(t.Exception.ToString()); },
                TaskContinuationOptions.OnlyOnFaulted);
        }

        public static void TurnAsyncToSyncTask(this Task task)
        {   // RunSynchronously may not be called on a task not bound to a delegate, such as the task returned from an asynchronous method.
            // So for asynch Methods, use Wait(), or use ConfigureAwait() + GetResult() which is Explicit wait too.
            // https://stackoverflow.com/questions/14485115/synchronously-waiting-for-an-async-operation-and-why-does-wait-freeze-the-pro
            task.ConfigureAwait(continueOnCapturedContext: false).GetAwaiter().GetResult();
        }

    }
}