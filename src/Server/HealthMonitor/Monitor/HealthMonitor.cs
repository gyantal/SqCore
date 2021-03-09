using SqCommon;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace HealthMonitor
{
    public enum DataSource { HealthMonitor = 0, VBroker = 1, VBrokerCheckOKMessageArrived = 2, SqCoreWebsiteCore, SqCoreWebsiteApp1 }

    class DelayedMessageItem
    {
        public string EmailSubject { get; set; } = String.Empty;
        public string EmailBody { get; set; } = String.Empty;
        public string PhoneCallText { get; set; } = String.Empty;
    }
    class DelayedMessage
    {
        public Timer? Timer { get; set; } = null;
        public object Lock { get; set; } = new object();
        public DataSource DataSource { get; set; }
        public List<DelayedMessageItem> MessageItems = new List<DelayedMessageItem>();
    }

    [Flags]
    public enum InformSuperVisorsUrgency { Normal_UseTimer = 0, UrgentInfoSendEmail_OnlyUseIfCannotBeSpammedBySource = 1, UrgentInfoMakePhonecall_OnlyUseIfCannotBeSpammedBySource = 2 }


    public class SavedState : PersistedState   // data to persist between restarts of the crawler process
    {
        public bool IsDailyEmailReportEnabled { get; set; } = true;
        public bool IsRealtimePriceServiceTimerEnabled { get; set; } = true;
        public bool IsVBrokerTimerEnabled { get; set; } = true;
        public bool IsProcessingVBrokerMessagesEnabled { get; set; } = true;
        public bool IsProcessingSqCoreWebsiteMessagesEnabled { get; set; } = true;

        public bool IsSendErrorEmailAtGracefulShutdown { get; set; } = true;   // switch this off before deployment, and switch it on after deployment; make functionality on the WebSite

        public int nFailedDownload_YahooFinanceMain { get; set; } = 0;
    }

    public partial class HealthMonitor
    {
        static public HealthMonitor g_healthMonitor = new HealthMonitor();
        DateTime m_startTime;

        SavedState? m_persistedState = null;

        //Your timer object goes out of scope and gets erased by Garbage Collector after some time, which stops callbacks from firing. Save reference to it in a member of class.
        long m_nHeartbeat = 0;
        long m_nRtpsTimerCalled = 0;     // Real Time Price Service

        Timer? m_heartbeatTimer = null;
        Timer? m_checkWebsitesAndKeepAliveTimer = null;
        Timer? m_checkAmazonAwsInstancesTimer = null;
        Timer? m_rtpsTimer = null;

        Timer? m_dailyMarketOpenTimer = null;   // called at 9:30 ET, may be late or early by 1 hour, if there is a DayLightSaving day
        Timer? m_dailyReportTimer = null;   // called at the market close, because this is set by the MarketOpen Timer, it always use the current day proper DayLightSaving settings. Will be correct.

        const int cHeartbeatTimerFrequencyMinutes = 5;
        const int cRtpsTimerFrequencyMinutes = 15;  // changed from 10min to 15, to decrease strain on VBroker and to get less 'Requested market data is not subscribed' emails.
        const int cCheckWebsitesTimerFrequencyMinutes = 16;
        const int cCheckAmazonAwsTimerFrequencyMinutes = 60;
        Object m_lastHealthMonInformSupervisorLock = new Object();   // null value cannot be locked, so we have to create an object
        DateTime m_lastHealthMonErrorEmailTime = DateTime.MinValue;    // don't email if it was made in the last 10 minutes
        DateTime m_lastHealthMonErrorPhoneCallTime = DateTime.MinValue;    // don't call if it was made in the last 30 minutes

        ConcurrentDictionary<DataSource, DelayedMessage> m_delayedMessages = new ConcurrentDictionary<DataSource, DelayedMessage>();

        public SavedState? PersistedState
        {
            get
            {
                return m_persistedState;
            }

            set
            {
                m_persistedState = value;
            }
        }

        public void Init()
        {
            Utils.Logger.Info("****HealthMonitor:Init()");
            m_startTime = DateTime.UtcNow;

            // 1. Get the Current Parameter state from a persisted place (file, or AzureTable) (in case this HealthMonitor was unloaded and restarted)
            //PersistedState = new SavedState().CreateOrOpenEx();
            PersistedState = new SavedState();

            ScheduleTimers();
            InitVbScheduler();
            m_tcpListener = new ParallelTcpListener(ServerIp.LocalhostMetaAllPrivateIpWithIP, HealthMonitorMessage.DefaultHealthMonitorServerPort, ProcessTcpClient);
            m_tcpListener.StartTcpMessageListenerThreads();
        }

        public static bool IsRunningAsLocalDevelopment()
        {
            if (Utils.RunningPlatform() == Platform.Linux)    // assuming production environment on Linux, Other ways to customize: ifdef DEBUG/RELEASE  ifdef PRODUCTION/DEVELOPMENT, etc. this Linux/Windows is fine for now
            {
                return false;
            }
            else
            {
                // Windows: however, sometimes, when Running on Windows, we want to Run it as a proper Production environment. E.g.
                //      + Sometimes, for Debugging reasons, 
                //      + sometimes, because Linux server is down and running the Production locally on Windows
                return true;
            }
        }

        // at graceful shutdown, it is called
        public void Exit()      // in general exit should happen in the opposite order as Init()
        {
            //PersistedState.Save();
            if (m_tcpListener != null)
                m_tcpListener.StopTcpMessageListener();
            ExitVbScheduler();
        }

        private void ScheduleTimers()
        {
            try
            {
                Utils.Logger.Info("ScheduleDailyTimers() BEGIN");
                // "if I don't hit the site for 10-15 minutes, it goes to sleep"; "default configuration of an IIS Application pool that is set to have an idle-timeout of 20 minutes"
                m_checkWebsitesAndKeepAliveTimer = new System.Threading.Timer(new TimerCallback(CheckWebsitesAndKeepAliveTimer_Elapsed), null, TimeSpan.FromSeconds(30), TimeSpan.FromMinutes(cCheckWebsitesTimerFrequencyMinutes));

                m_checkAmazonAwsInstancesTimer = new System.Threading.Timer(new TimerCallback(CheckAmazonAwsInstances_Elapsed), null, TimeSpan.FromSeconds(40), TimeSpan.FromMinutes(cCheckAmazonAwsTimerFrequencyMinutes));

                m_rtpsTimer = new System.Threading.Timer(new TimerCallback(RtpsTimer_Elapsed), null, TimeSpan.FromSeconds(50), TimeSpan.FromMinutes(cRtpsTimerFrequencyMinutes));

                m_heartbeatTimer = new System.Threading.Timer((e) =>    // Heartbeat log is useful to find out when VM was shut down, or when the App crashed
                {
                    Utils.Logger.Info($"**m_nHeartbeat: {m_nHeartbeat} (at every {cHeartbeatTimerFrequencyMinutes} minutes)");
                    m_nHeartbeat++;
                }, null, TimeSpan.FromMinutes(0.5), TimeSpan.FromMinutes(cHeartbeatTimerFrequencyMinutes));

                m_dailyMarketOpenTimer = new System.Threading.Timer(new TimerCallback(DailyMarketOpenTimer_Elapsed), null, TimeSpan.FromMilliseconds(-1), TimeSpan.FromMilliseconds(-1));
                m_dailyReportTimer = new System.Threading.Timer(new TimerCallback(DailyReportTimer_Elapsed), null, TimeSpan.FromMilliseconds(-1), TimeSpan.FromMilliseconds(-1));
                DailyMarketOpenTimer_Elapsed(null);    // usually MarketOpenTimer re-set m_dailyReportTimer at market open, but if App is started intraday, this will set both timer
            }
            catch (Exception e)
            {
                Utils.Logger.Info(e, "ScheduleDailyTimers() Exception.");
                InformSupervisors(InformSuperVisorsUrgency.Normal_UseTimer, $"SQ HealthMonitor: ScheduleDailyTimers() Exception.", $"SQ HealthMonitor: ScheduleDailyTimers() Exception. Check log file.", $"HealthMonitor Schedule Daily Timers Exception. ... I repeat: HealthMonitor Schedule Daily Timers Exception.", ref m_lastHealthMonInformSupervisorLock, ref m_lastHealthMonErrorEmailTime, ref m_lastHealthMonErrorPhoneCallTime);
            }
            Utils.Logger.Info("ScheduleDailyTimers() END");
        }

        // Any solution that depends on System.Timers.Timer, System.Threading.Timer, or any of the other timers 
        // that currently exist in the.NET Framework will fail in the face of Daylight Saving time changes.  (if you want to time for Local time, but I want to time it for UtcTime)
        // If you use any of those timers, you will have to do some polling.
        // That will fail in the face of Daylight Saving time changes. It could execute an hour late or an hour early. 
        // but as we want to set up a Timer using UtcTime, it will not fail, because we exactly want utcTime timing. 
        // And the problem that time in USA local time, sometimes it is one hour early, sometimes one hour late, we don't care.
        private void SetupNotRepeatingDailyMarketOpenTimer()
        {
            DateTime utcNow = DateTime.UtcNow;
            DateTime openInET = new DateTime(utcNow.Year, utcNow.Month, utcNow.Day, 9, 30, 0);  // USA market always open at 9:30 ET
            DateTime requiredUtcTime = TimeZoneInfo.ConvertTime(openInET, SqCommon.Utils.FindSystemTimeZoneById(TimeZoneId.EST), TimeZoneInfo.Utc);

            // trigger the event at 15:00 in UTC; USA market opens at 14:30 in general or sometimes at 13:30 (only in 2 weeks)
            //DateTime requiredUtcTime = DateTime.UtcNow.Date.AddHours(15).AddMinutes(00); // that is today at 15:00
            if (utcNow > requiredUtcTime)
            {
                requiredUtcTime = requiredUtcTime.AddDays(1);
            }
            Utils.Logger.Info("SetupNotRepeatingDailyMarketOpenTimer for UTC time as: " + requiredUtcTime);

            // first parameter is the start time Interval and the second parameter is the interval
            // Timeout.Infinite means do not repeat the interval, only start the timer
            if (m_dailyMarketOpenTimer != null)
                m_dailyMarketOpenTimer.Change(requiredUtcTime - utcNow, Timeout.InfiniteTimeSpan);
        }

        private void SetupNotRepeatingDailyReportTimer()
        {
            bool isMarketTradingDay;
            DateTime openTimeUtc, closeTimeUtc;
            bool isTradingHoursOK = Utils.DetermineUsaMarketTradingHours(DateTime.UtcNow, out isMarketTradingDay, out openTimeUtc, out closeTimeUtc, TimeSpan.FromDays(3));
            if (!isTradingHoursOK)
            {
                Utils.Logger.Warn("DetermineUsaMarketTradingHours() was not ok.");
                // SetupNotRepeatingDailyReportTimer() happens once per day, if DetermineUsaMarketTradingHours() fails, send email only once per day.
                StrongAssert.Fail(Severity.NoException, $"DetermineUsaMarketTradingHours() failed. They probably changed their website. They do it annually. Daily HealthMonitor email at market close will not be sent.");
            }
            else
            {
                DateTime dailyReportStartTime = closeTimeUtc.AddMinutes(1.0);   // run it 1 minute after close, when VBrokers finished
                if (isMarketTradingDay && (DateTime.UtcNow.AddSeconds(10) < dailyReportStartTime)) // if it is only 10 seconds or less until close, don't start it.
                {
                    Utils.Logger.Info("SetupNotRepeatingDailyReportTimer for UTC time as: " + (DateTime)dailyReportStartTime);
                    if (m_dailyReportTimer != null)
                        m_dailyReportTimer.Change((DateTime)dailyReportStartTime - DateTime.UtcNow, Timeout.InfiniteTimeSpan);
                }
                else
                {
                    Utils.Logger.Info("SetupNotRepeatingDailyReportTimer not set. It is not a market day or market is closed already. ReportTimer will be set by MarketOpenTimer on the next trading day.");
                }
            }
        }

        private void DailyMarketOpenTimer_Elapsed(object? p_stateObj)   // this is called at 14:30 UTC every day; during that day DayLightSaving setting will not change, // Timer is coming on a ThreadPool thread
        {
            try
            {
                Utils.Logger.Info("DailyMarketOpenTimer_Elapsed() BEGIN");

                SetupNotRepeatingDailyReportTimer();
                SetupNotRepeatingDailyMarketOpenTimer();
            }
            catch (Exception e)
            {
                Utils.Logger.Error(e, "DailyMarketOpenTimer_Elapsed() exception.");
                //throw;
            }

            Utils.Logger.Info("DailyMarketOpenTimer_Elapsed() END");
        }

        // called at the market close, because this is set by the MarketOpen Timer, it always use the current day proper DayLightSaving settings. Will be correct.
        public void DailyReportTimer_Elapsed(object? p_stateObj) // Timer is coming on a ThreadPool thread
        {
            try
            {
                Utils.Logger.Info("DailyReportTimer_Elapsed() BEGIN");
                StringBuilder sb = DailySummaryReport(true);

                new Email
                {
                    ToAddresses = Utils.Configuration["Emails:Gyant"],
                    Subject = "HealthMonitor Daily Report",
                    Body = sb.ToString(),
                    IsBodyHtml = true
                }.Send();
            }
            catch (Exception e)
            {
                Utils.Logger.Error(e, "DailyReportTimer_Elapsed() exception.");
                //throw;
            }
            Utils.Logger.Info("DailyReportTimer_Elapsed() END");
        }

        string m_dailyReportEmailStr1 =
@"<!DOCTYPE html><html><head><style>
.sqNormalText {
    font-size: 125%;
}
.sqImportantOK {
    font-size: 140%;
    color: #10ff10;
    font-weight: bold;
}
.sqImportantError {
    font-size: 140%;
    color: #FF2020;
    font-weight: bold;
}
.sqDetail {
    font-size: 70%;
}
</style></head>
<body class=""sqNormalText"">
    <strong>Realtime Price</strong> Service: ";

        string m_dailyReportEmailStr2 =
@"</body>
</html>";

        // just summarize the whole today; don't go into too much detail. Result is sent by email
        public StringBuilder DailySummaryReport(bool p_isHtml)
        {
            Utils.Logger.Info("DailySummaryReport()");
            if (PersistedState == null || !PersistedState.IsDailyEmailReportEnabled)
                return new StringBuilder();

            StringBuilder sb = new StringBuilder((p_isHtml) ? m_dailyReportEmailStr1 : "Realtime Price:");

            bool? rtpsLastDownloadAccepted = null;
            var rtpsLastDownloadsSnapshot = m_rtpsLastDownloads.ToArray(); // we have to make a snapshot anyway, so the Timer thread can write it while we itarate
            if (rtpsLastDownloadsSnapshot.Length > 0)  // we need the last element
            {
                rtpsLastDownloadAccepted = rtpsLastDownloadsSnapshot[rtpsLastDownloadsSnapshot.Length - 1].Item2;
            }

            if (rtpsLastDownloadAccepted == null)
                sb.Append((p_isHtml) ? @"<span class=""sqImportantError""> ?</span>" : $" ?{Environment.NewLine}");
            else if ((bool)rtpsLastDownloadAccepted)
                sb.Append((p_isHtml) ? @"<span class=""sqImportantOK""> OK</span>" : $" OK{Environment.NewLine}");
            else
                sb.Append((p_isHtml) ? @"<span class=""sqImportantError""> ERROR</span>" : $" ERROR{Environment.NewLine}");
            Utils.Logger.Trace("DailySummaryReport(). rtps_1");

            sb.Append((p_isHtml) ? @"<br><strong>VBroker</strong>:" : "VBroker:"); // try to be concise in the email, so user has to spend only 1 second to evaluate: OK or ERROR. (don't put extra info, because it takes too long to evaluate)
            DateTime utcStartOfToday = DateTime.UtcNow.Date;
            bool wasAllOkToday = true;
            int nReportsToday = 0;

            Dictionary<string, string> lastDetailedVBrokerReports = new Dictionary<string, string>();
            StringBuilder sb2 = new StringBuilder();
            lock (m_VbReport)
            {
                for (int i = 0; i < m_VbReport.Count; i++)
                {
                    if (m_VbReport[i].Item1 > utcStartOfToday)
                    {
                        wasAllOkToday &= m_VbReport[i].Item2;
                        string strategyName = String.Empty;
                        int strategyNameInd1 = m_VbReport[i].Item3.IndexOf("BrokerTask ");  // "BrokerTask UberVXX was OK" or "had ERROR"
                        if (strategyNameInd1 != -1)
                        {
                            int strategyNameInd2 = strategyNameInd1 + "BrokerTask ".Length;
                            int strategyNameInd3 = m_VbReport[i].Item3.IndexOf(" ", strategyNameInd2);
                            if (strategyNameInd3 != -1)
                            {
                                strategyName = m_VbReport[i].Item3.Substring(strategyNameInd2, strategyNameInd3 - strategyNameInd2);
                            }
                        }

                        if (String.IsNullOrEmpty(strategyName))
                            strategyName = "Unknown strategy";

                        Utils.Logger.Trace($"DailySummaryReport(). Adding strategyName '{strategyName}' / '{ m_VbReport[i].Item4}' to lastDetailedVBrokerReports dictionary.");
                        lastDetailedVBrokerReports[strategyName] = m_VbReport[i].Item4;

                        nReportsToday++;
                        sb2.Append("    - " + m_VbReport[i].Item1.ToString("HH:mm:ss")
                            + (String.IsNullOrEmpty(strategyName) ? "" : " (" + strategyName + ")") +
                            ": " + ((m_VbReport[i].Item2) ? "OK" : "ERROR") +
                            ((p_isHtml) ? "<br>" : Environment.NewLine));


                    }
                }

                // we don't want to leak memory, and accumulate 200 daily records, so if the messages are 30 day or older remove them from the List
                DateTime day30DaysEarlier = utcStartOfToday.AddDays(-30);
                var v2 = m_VbReport.Where(r => r.Item1 > day30DaysEarlier).OrderBy(r => r.Item1).ToList();
                m_VbReport = v2;
            }
            Utils.Logger.Trace("DailySummaryReport(). vb_1");

            if (nReportsToday == 0)
                sb.Append((p_isHtml) ? @"<span class=""sqImportantError"">Potential ERROR!</span>: No report today from VBroker. Maybe it has crashed or VM is down." : "Potential ERROR! No report today from VBroker. Maybe it has crashed or VM is down.");
            else if (wasAllOkToday) // there was a report today from VBroker AND it was allOk
                sb.Append((p_isHtml) ? @"<span class=""sqImportantOK"">OK</span>" : " OK");
            else
                sb.Append((p_isHtml) ? @"<span class=""sqImportantError"">ERROR</span>: Errors occured today. See logs." : " ERROR");

            if (sb2.Length > 0)
            {
                sb.Append((p_isHtml) ? "<br>" : Environment.NewLine);
                sb.Append(sb2);
            }

            Utils.Logger.Trace($"DailySummaryReport(). lastDetailedVBrokerReports.Count: '{lastDetailedVBrokerReports.Count}'");
            if (lastDetailedVBrokerReports.Count > 0)
            {
                sb.AppendLine((p_isHtml) ? @"<br><hr><br><span class=""sqDetail""><strong>VBroker Detailed</strong>:<br>" : "VBroker Detailed:");
                foreach (var lastDetailedReport in lastDetailedVBrokerReports)
                {
                    Utils.Logger.Trace($"DailySummaryReport(). ItemKey: {lastDetailedReport.Key}");
                    Utils.Logger.Trace($"DailySummaryReport(). ItemValue: {lastDetailedReport.Value}");
                    string detailedRep = lastDetailedReport.Value.Replace("#10ff10", "green");      // on the website: #10ff10 is better, lighter, because of background. In email, background is white. "green" is darker. Better.
                    sb.Append((p_isHtml) ? $"{detailedRep}<br>" : detailedRep + Environment.NewLine + Environment.NewLine);        // fine tune later
                }
                sb.AppendLine((p_isHtml) ? @"</span>" : $"{Environment.NewLine}");
            }

            if (p_isHtml)
                sb.Append(m_dailyReportEmailStr2);

            Utils.Logger.Trace("DailySummaryReport() END.");
            return sb;
        }

        private void InformSupervisorsEx(DataSource p_dataSource, bool p_isUrgent, string p_emailSubject, string p_emailBody, string p_phonecallText, ref Object p_informSupervisorLock, ref DateTime p_lastCallTime)
        {
            // group messages by p_dataSource. 
            // 1. Always check p_lastEmailTime (even in urgent messages: don't send it every second if it is mistakenly spammed by source.)
            // If p_lastEmailTime > 10 min in Normal, and > 2 min in Urgent case => send email instantly
            // If p_lastEmailTime was too close, then don't spam email/phone call. 
            //      Add message data to that group; that timer callback can use.
            //      If timer does not exists for that source, create timer expiring in 10/2 min.
            // Timer callback: combine emails into one and deletes msgArray

            lock (p_informSupervisorLock)   // if InformSupervisors() can be called on two different threads at the same time, (if VBroker notified us twice very quickly) then one thread should wait, because we still want to inform user only once
            {
                TimeSpan timeFromLastInform = DateTime.UtcNow - p_lastCallTime;
                bool doInformSupervisorsNow = (p_isUrgent && (timeFromLastInform > TimeSpan.FromMinutes(2))) ||
                                                (!p_isUrgent && (timeFromLastInform > TimeSpan.FromMinutes(10)));
                p_lastCallTime = DateTime.UtcNow;
                if (doInformSupervisorsNow)
                {
                    SendEmailAndMakePhoneCall(p_emailSubject, p_emailBody, p_phonecallText);
                    return;
                }

                // Delay sending info. A timer callback should send it much later.
                var delayedMsg = m_delayedMessages.AddOrUpdate(p_dataSource,
                    k =>    // Add new
                    {
                        return new DelayedMessage()
                        {
                            Timer = new System.Threading.Timer(new TimerCallback(InformSupervisorsTimer_Elapsed), k, TimeSpan.FromMinutes(p_isUrgent ? 2 : 10), TimeSpan.FromMilliseconds(-1)),
                            DataSource = k,
                            MessageItems = new List<DelayedMessageItem>() { new DelayedMessageItem() { EmailSubject = p_emailSubject, EmailBody = p_emailBody, PhoneCallText = p_phonecallText } }
                        };
                    },
                    (k, v) =>   // Update existing
                    {
                        lock (v.Lock)
                        {
                            if (v.MessageItems.Count == 0 && v.Timer != null) // it means that InformSupervisorsTimer_Elapsed() already cleared it, we have to reset the timer again
                                v.Timer.Change(TimeSpan.FromMinutes(p_isUrgent ? 2 : 10), TimeSpan.FromMilliseconds(-1));
                            v.MessageItems.Add(new DelayedMessageItem() { EmailSubject = p_emailSubject, EmailBody = p_emailBody, PhoneCallText = p_phonecallText });
                        }
                        return v;
                    });

            }   // lock (p_informSupervisorLock)
        }



        public void InformSupervisorsTimer_Elapsed(object? p_stateObj) // Timer is coming on a ThreadPool thread
        {
            try
            {
                Utils.Logger.Info("InformSupervisorsTimer_Elapsed() BEGIN");
                if (p_stateObj == null)
                    throw new Exception("Expected a non-null parameter.");

                DataSource dataSource = (DataSource)p_stateObj;
                var delayedMsg = m_delayedMessages[dataSource];
                string emailSubject = "SQ HealthMonitor: Pooled messages"; 
                StringBuilder emailBody = new StringBuilder();
                lock (delayedMsg.Lock)
                {
                    foreach (var msg in delayedMsg.MessageItems)
                    {
                        emailBody.AppendLine($"Subject: {msg.EmailSubject}, Body: {msg.EmailBody}");
                    }
                    delayedMsg.MessageItems.Clear();
                }

                // phonecallText can be aggregated, but we intentionally leave it empty. This timer only happens if the first phonecall was already made. One phonecall per problem type should be enough.
                SendEmailAndMakePhoneCall(emailSubject, emailBody.ToString(), String.Empty);
            }
            catch (Exception e)
            {
                Utils.Logger.Error(e, "InformSupervisorsTimer_Elapsed() exception.");
                //throw;
            }
            Utils.Logger.Info("InformSupervisorsTimer_Elapsed() END");
        }


        private static void SendEmailAndMakePhoneCall(string p_emailSubject, string p_emailBody, string p_phonecallText)
        {
            Utils.Logger.Info("InformSupervisors(). Sending Warning email.");
            try
            {
                new Email
                {
                    ToAddresses = Utils.Configuration["Emails:Gyant"],
                    Subject = p_emailSubject,
                    Body = p_emailBody,
                    IsBodyHtml = true       // even though VBroker messages are not HTML, but text. But it works.
                    //IsBodyHtml = false        // has problems with exceptions : /bin/bash: -c: line 1: syntax error near unexpected token `('
                }.Send();
            }
            catch (Exception e)
            {
                Utils.Logger.Error(e, "InformSupervisors() email sending is crashed, but we still try to make the PhoneCall.");
            }

            if (!IsRunningAsLocalDevelopment() && !String.IsNullOrEmpty(p_phonecallText))
            {
                Utils.Logger.Info("InformSupervisors(). Making Phonecall.");

                var call = new PhoneCall
                {
                    FromNumber = Caller.Gyantal,
                    ToNumber = PhoneCall.PhoneNumbers[Caller.Gyantal],
                    Message = p_phonecallText,
                    NRepeatAll = 2
                };
                bool didTwilioAcceptedTheCommand = call.MakeTheCall();
                if (didTwilioAcceptedTheCommand)
                {
                    Utils.Logger.Debug("PhoneCall instruction was sent to Twilio.");
                }
                else
                    Utils.Logger.Error("PhoneCall instruction was NOT accepted by Twilio.");
            }
        }

        private void InformSupervisors(InformSuperVisorsUrgency p_urgency, string p_emailSubject, string p_emailBody, string p_phonecallText, ref Object p_informSupervisorLock, ref DateTime p_lastEmailTime, ref DateTime p_lastPhoneCallTime)
        {
            bool doInformSupervisors = false;
            if (p_urgency.HasFlag(InformSuperVisorsUrgency.UrgentInfoSendEmail_OnlyUseIfCannotBeSpammedBySource) || p_urgency.HasFlag(InformSuperVisorsUrgency.UrgentInfoMakePhonecall_OnlyUseIfCannotBeSpammedBySource))
                doInformSupervisors = true;
            else
            {
                lock (p_informSupervisorLock)   // if InformSupervisors is called on two different threads at the same time, (if VBroker notified us twice very quickly), we still want to inform user only once
                {
                    TimeSpan timeFromLastEmail = DateTime.UtcNow - p_lastEmailTime;
                    if (timeFromLastEmail > TimeSpan.FromMinutes(10))
                    {
                        doInformSupervisors = true;
                        p_lastEmailTime = DateTime.UtcNow;
                    }
                }
            }

            if (!doInformSupervisors)
                return;

            Utils.Logger.Info("InformSupervisors(). Sending Warning email.");
            try
            {
                new Email
                {
                    ToAddresses = Utils.Configuration["Emails:Gyant"],
                    Subject = p_emailSubject,
                    Body = p_emailBody,
                    IsBodyHtml = true
                    //IsBodyHtml = false        // has problems with exceptions : /bin/bash: -c: line 1: syntax error near unexpected token `('
                }.Send();
            }
            catch (Exception e)
            {
                Utils.Logger.Error(e, "InformSupervisors() email sending is crashed, but we still try to make the PhoneCall.");
            }


            if (!IsRunningAsLocalDevelopment() && !String.IsNullOrEmpty(p_phonecallText))
            {
                Utils.Logger.Info("InformSupervisors(). Making Phonecall.");

                TimeSpan timeFromLastCall = DateTime.UtcNow - p_lastPhoneCallTime;
                TimeSpan minTimeFromLastCall = p_urgency.HasFlag(InformSuperVisorsUrgency.UrgentInfoMakePhonecall_OnlyUseIfCannotBeSpammedBySource) ? TimeSpan.FromSeconds(3) : TimeSpan.FromMinutes(30);
                if (timeFromLastCall > minTimeFromLastCall)
                {
                    var call = new PhoneCall
                    {
                        FromNumber = Caller.Gyantal,
                        ToNumber = PhoneCall.PhoneNumbers[Caller.Gyantal],
                        Message = p_phonecallText,
                        NRepeatAll = 2
                    };
                    // skipped temporarily
                    bool didTwilioAcceptedTheCommand = call.MakeTheCall();
                    if (didTwilioAcceptedTheCommand)
                    {
                        Utils.Logger.Debug("PhoneCall instruction was sent to Twilio.");
                        p_lastPhoneCallTime = DateTime.UtcNow;
                    }
                    else
                        Utils.Logger.Error("PhoneCall instruction was NOT accepted by Twilio.");
                }
            }


        }

    }
}
