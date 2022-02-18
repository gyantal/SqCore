using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using SqCommon;

namespace SqCommon
{
    // All apps need Caretaker services (e.g. Checking free disk space for log files)
    // Memory resident programs (WebServer, VirtualBroker) might need to monitor excess RAM usage, internet bandwidth slowage, monitor free disk space every day
    // Run-and-Exit programs (crawlers) that runs every day also need the Caretaker, to periodically decimate Log files.
    public sealed class Caretaker : IDisposable
    {
        public static Caretaker g_caretaker = new ();

        string m_appName = string.Empty;
        string m_serviceSupervisorsEmail = string.Empty;
        bool m_needDailyMaintenance;
        TimeSpan m_dailyMaintenanceFromMidnightET = TimeSpan.MinValue;

        public bool IsInitialized { get; set; } = false;
        Timer? m_timer = null;

        // bigger daily maintenance tasks should run on the server at different times to ease resource usage
        // ManualTrader server:
        // SqCoreWeb: 2:00 ET
        // VBroker: 2:30 ET
        public void Init(string p_appName, string p_serviceSupervisorsEmail, bool p_needDailyMaintenance, TimeSpan p_dailyMaintenanceFromMidnightET)
        {
            m_appName = p_appName;
            m_serviceSupervisorsEmail = p_serviceSupervisorsEmail;
            m_needDailyMaintenance = p_needDailyMaintenance;
            m_dailyMaintenanceFromMidnightET = p_dailyMaintenanceFromMidnightET;
            Utils.RunInNewThread(Init_WT);
        }

        void Init_WT(object? p_state) // WT : WorkThread
        {
            Thread.CurrentThread.Name = "Caretaker.Init_WT Thread";

            if (m_needDailyMaintenance)
            {
                m_timer = new System.Threading.Timer(new TimerCallback(Timer_Elapsed), this, TimeSpan.FromMilliseconds(-1.0), TimeSpan.FromMilliseconds(-1.0));
                SetTimer(m_timer);
            }

            IsInitialized = true;
        }

        public void SetTimer(Timer p_timer)
        {
            DateTime etNow = DateTime.UtcNow.FromUtcToEt();
            DateTime targetDateEt = etNow.Date.AddDays(1).Add(m_dailyMaintenanceFromMidnightET);  // e.g. run maintenance 2:00 ET, which is 7:00 GMT usually. Preopen market starts at 5:00ET.
            p_timer.Change(targetDateEt - etNow, TimeSpan.FromMilliseconds(-1.0));     // runs only once.
        }

        public void Timer_Elapsed(object? state) // Timer is coming on a ThreadPool thread
        {
            Utils.Logger.Info($"Caretaker.Timer_Elapsed() START");
            try
            {
                DailyMaintenance();
                SetTimer(m_timer!);
            }
            catch (System.Exception e)
            {
                // 2021-06-02: HealthMonitor app: unexplained exception coupled with 'not enough free memory' message on Linux VNC desktop: "
                // "Unhandled exception. System.IO.FileNotFoundException: Unable to find the specified file.
                //    at Interop.Sys.GetCwdHelper(Byte* ptr, Int32 bufferSize)
                //    at Interop.Sys.GetCwd()   // Directory.GetCurrentDirectory() was called
                //    at SqCommon.Caretaker.CheckFreeDiskSpace(StringBuilder p_noteToClient)

                Utils.Logger.Error(e, "Exception in Caretaker.Timer_Elapsed().");
                string emailBody = e.ToStringWithShortenedStackTrace(1000);
                new Email()
                {
                    Body = emailBody,
                    IsBodyHtml = false,
                    Subject = $"{m_appName} Caretaker Error! App crashed.",    // 'error' or 'warning' should be in the subject line to trigger attention of users
                    ToAddresses = m_serviceSupervisorsEmail
                }.Send();             // see SqCore.WebServer.SqCoreWeb.NoGitHub.json
                throw;    // we can choose to swallow the exception or crash the app. If we swallow it, we might risk that error will go undetected forever.
            }

            Utils.Logger.Info($"Caretaker.Timer_Elapsed() END");
        }

        public void DailyMaintenance()
        {
            DateTime etNow = Utils.ConvertTimeFromUtcToEt(DateTime.UtcNow);
            if (etNow.DayOfWeek == DayOfWeek.Sunday)
            {
                CheckFreeDiskSpace(null);
                CleanLogfiles(null);
            }
        }

        public bool CheckFreeDiskSpace(StringBuilder? p_noteToClient)
        {
            Utils.Logger.Info($"Caretaker.CheckFreeDiskSpace() START");
            string currentWorkingDir = Directory.GetCurrentDirectory();
            Utils.Logger.Info($"Caretaker.GetCurrentDirectory(): '{currentWorkingDir}'");   // this can throw System.IO.FileNotFoundException if we redeploy the App, and overwrite (delete) folders under the app, without stopping/restarting the app.
            string foundIssues = string.Empty;
            int requiredFreeSpaceGb = 2;
            if (p_noteToClient != null)
                p_noteToClient.AppendLine($"Checking required safe free disk space of {requiredFreeSpaceGb}GB on drive containing the working dir {currentWorkingDir}");
            foreach (DriveInfo drive in DriveInfo.GetDrives().OrderBy(r => r.DriveType))
            {
                // Type: Fixed (C:\ on Windows, '/', '/sys/fs/pstore' (persistent storage of Kernel errors) on Linux)
                // Type: Ram, Unknown, Network (many different virtual drives on Linux)
                string noteToClient = $"Drive Name:'{drive.Name}', Type:{drive.DriveType}, RoodDirName:{drive.RootDirectory.Name}";    // name such as 'C:\'
                Utils.Logger.Info(noteToClient);
                if (p_noteToClient != null)
                    p_noteToClient.AppendLine(noteToClient);

                // We have to be selective and check only the drive from which the app runs, because drive.TotalFreeSpace raises exceptions for specific drives.
                // On Windows: DVD drives has no free space. Exception:'The device is not ready.
                // On Linux: System.IO.IOException: Permission denied if ask for the system drives, like '/sys', '/proc', '/dev', '/dev/pts'
                if (!currentWorkingDir.StartsWith(drive.Name))
                    continue;

                // In theory AvailableFreeSpace should be used, because that gives the proper space for this user, considering user-quota limits set by the admin.
                long freeDiskSpaceMB = drive.TotalFreeSpace / 1024 / 1024;
                long availableUserQuotaSpaceMB = drive.AvailableFreeSpace / 1024 / 1024;

                noteToClient = $"    Drive '{drive.Name}'. Free disk space:{freeDiskSpaceMB}MB, Available user quota:{availableUserQuotaSpaceMB}MB";
                Utils.Logger.Info(noteToClient);
                if (p_noteToClient != null)
                    p_noteToClient.AppendLine(noteToClient);
                if (availableUserQuotaSpaceMB < requiredFreeSpaceGb * 1024) // the free space is less than 2 GB
                    foundIssues += $"! Low free space (<{requiredFreeSpaceGb}GB): " + noteToClient + Environment.NewLine;
            }
            bool isLowDiskSpace = !string.IsNullOrEmpty(foundIssues);
            if (isLowDiskSpace && (p_noteToClient != null))
            {
                p_noteToClient.Append("Warning:" + Environment.NewLine + foundIssues);
                new Email()
                {
                    Body = p_noteToClient.ToString(),
                    IsBodyHtml = false,
                    Subject = $"{m_appName} Caretaker Warning! CheckFreeDiskSpace found low free space",    // 'error' or 'warning' should be in the subject line to trigger attention of users
                    ToAddresses = m_serviceSupervisorsEmail
                }.Send();             // see SqCore.WebServer.SqCoreWeb.NoGitHub.json
            }
            Utils.Logger.Info($"Caretaker.CheckFreeDiskSpace() END. isLowDiskSpace: {isLowDiskSpace}");
            return !isLowDiskSpace;
        }

        // NLog names the log files as "logs/SqCoreWeb.${date:format=yyyy-MM-dd}.sqlog".
        // Even without restarting the app, a new file with a new date is created the first time any log happens after midnight. The yesterday log file is closed.
        // That assures that one log file is not too big and it contains only log for that day, no matter when was the app restarted the last time.
        public bool CleanLogfiles(StringBuilder? p_noteToClient)
        {
            Utils.Logger.Info("Caretaker.CleanLogfiles() BEGIN");

            string currentWorkingDir = Directory.GetCurrentDirectory();

            if (p_noteToClient != null)
                p_noteToClient.AppendLine($"Current working directory of the app: {currentWorkingDir}");

            // TODO: probably you need not the WorkingDir, but the directory of the running application (EXE), although Caretaker.cs is in the SqCommon.dll. Which would be in the same folder as the EXE.
            // see and extend Utils_runningEnv.cs with utility functions if needed
            // the 'logs' folder is relative to the EXE folder, but its relativity can be different in Windows, Linux
            // Windows: See Nlog.config "fileName="${basedir}/../../../../../../logs/SqCoreWeb.${date:format=yyyy-MM-dd}.sqlog""
            // Linux: preparation in BuildAllProd.py: "line.replace("{basedir}/../../../../../../logs", "{basedir}/../logs")"

            // TODO: Tidy old log files. If the log file is more than 10 days old, then convert TXT file to 7zip
            // (keeping the filename: SqCoreWeb.2020-01-24.sqlog becomes SqCoreWeb.2020-01-24.sqlog.7zip) Delete TXT file.
            // If the 7zip file is more than 6 month old, then delete it. It is too old, it will not be needed.
            Utils.Logger.Info("Not implemented yet! TODO: Tidy old log files.");
            Utils.Logger.Info("Caretaker.CleanLogfiles() END");
            return true;
        }

        public void Exit()
        {
            Dispose();
        }

        public void Dispose()
        {
            if (m_timer != null)
                m_timer.Dispose();
        }
    }
}