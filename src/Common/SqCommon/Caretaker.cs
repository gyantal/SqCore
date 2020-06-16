using System;
using System.IO;
using System.Text;
using System.Threading;
using SqCommon;

namespace SqCommon
{
    // All apps need Caretaker services
    // Memory resident programs (WebServer, VirtualBroker) might need to monitor excess RAM usage, internet bandwidth slowage, monitor free disk space every day
    // Run-and-Exit programs (crawlers) that runs every day also need the Caretaker, to periodically decimate Log files.
    public class Caretaker
    {
        public static Caretaker gCaretaker = new Caretaker();

        string m_serviceSupervisorsEmail = String.Empty;
        bool m_needDailyMaintenance;
        TimeSpan m_dailyMaintenanceFromMidnightET = TimeSpan.MinValue;

        public bool IsInitialized { get; set; } = false;
        Timer? m_timer;

        // bigger daily maintenance tasks should run on the server at different times to ease resource usage
        // ManualTrader server:
        // SqCoreWeb: 2:00 ET
        // VBroker: 2:30 ET
        public void Init(string p_serviceSupervisorsEmail, bool p_needDailyMaintenance, TimeSpan p_dailyMaintenanceFromMidnightET)
        {
            m_serviceSupervisorsEmail = p_serviceSupervisorsEmail;
            m_needDailyMaintenance = p_needDailyMaintenance;
            m_dailyMaintenanceFromMidnightET = p_dailyMaintenanceFromMidnightET;
            ThreadPool.QueueUserWorkItem(Init_WT);
        }

        void Init_WT(object? p_state)    // WT : WorkThread
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
            DateTime etNow = Utils.ConvertTimeFromUtcToEt(DateTime.UtcNow);
            DateTime targetDateEt = etNow.Date.AddDays(1).Add(m_dailyMaintenanceFromMidnightET);  // e.g. run maintenance 2:00 ET, which is 7:00 GMT usually. Preopen market starts at 5:00ET.
            p_timer.Change(targetDateEt - etNow, TimeSpan.FromMilliseconds(-1.0));     // runs only once.


            // temporary change for testing purposes: set timer to 5 seconds to test maintenance
            // should be deleted after testing
            // p_timer.Change(TimeSpan.FromSeconds(5), TimeSpan.FromMilliseconds(-1.0));     // runs only once.
        }

        public void Timer_Elapsed(object? state)    // Timer is coming on a ThreadPool thread
        {
            DailyMaintenance();
            SetTimer(m_timer!);
        }

        public void DailyMaintenance()
        {


            // temporary change for testing purposes: commenting out test on sunday
            // should be commented back after testing



            DateTime etNow = Utils.ConvertTimeFromUtcToEt(DateTime.UtcNow);
            if (etNow.DayOfWeek == DayOfWeek.Sunday)
            {
                CheckFreeDiskSpace(null);
                CleanLogfiles(null);
            }
        }

        public bool CheckFreeDiskSpace(StringBuilder? p_noteToClient)
        {
            // TODO: CheckFreeDiskSpace: Both Windows and Linux: Check free disk space. If it is less than 2GB, inform archidata.servicesupervisors@gmail.com by email.
            string foundIssues = string.Empty;
            foreach (DriveInfo drive in DriveInfo.GetDrives())
            {
                Utils.Logger.Info("CheckFreeDiskSpace: drive " + drive.Name + " has free bytes " + drive.TotalFreeSpace);
                if (drive.TotalFreeSpace < 2e9)   // the free space is less than 2 GB
                    foundIssues += string.Format("drive {0} has less than 2GB free bytes, free disk space is {1}\r\n", drive.Name, drive.TotalFreeSpace);
            }
            if (string.IsNullOrEmpty(foundIssues) && (p_noteToClient != null))
            {
                p_noteToClient.AppendLine(foundIssues);
                new Email()
                {
                    Body = p_noteToClient.ToString(),
                    IsBodyHtml = false,
                    Subject = "CheckFreeDiskSpace found issues",
                    ToAddresses = m_serviceSupervisorsEmail
                }.Send();             // see SqCore.WebServer.SqCoreWeb.NoGitHub.json
                return true;
            }
            return false;
        }

        // NLog names the log files as "logs/SqCoreWeb.${date:format=yyyy-MM-dd}.sqlog". 
        // Even without restarting the app, a new file with a new date is created the first time any log happens after midnight. The yesterday log file is closed.
        // That assures that one log file is not too big and it contains only log for that day, no matter when was the app restarted the last time.
        public bool CleanLogfiles(StringBuilder? p_noteToClient)
        {
            Utils.Logger.Info("CleanLogfiles() BEGIN");

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

            Utils.Logger.Info("CleanLogfiles() END");
            return true;
        }

        public void Exit()
        {
        }

    }
}
