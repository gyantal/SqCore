using System;
using System.Collections.Generic;
using System.Text;
using SqCommon;

namespace SqCoreWeb
{
    public enum WsMonTaskSettingAction : byte
    {
        Unknown = 0,         
        SpIndexChanges     // Check S&P500 for index changes, addition, deletion for SP500, SP100, SP600, SP1500
    }
    public class WebsitesMonitor
    {
        public static WebsitesMonitor gWebsitesMonitor = new WebsitesMonitor();

        public void Init()
        {
            Utils.Logger.Info("****WebsitesMonitor:Init()");
            var sqTask = new SqTask()
            {
                Name = "WebsitesMonitor",
                ExecutionFactory = WebsitesMonitorExecution.ExecutionFactoryCreate,
            };
            sqTask.Triggers.Add(new SqTrigger()
            {
                Name = "SpIndexChanges",
                SqTask = sqTask,
                TriggerType = TriggerType.DailyOnUsaMarketDay,
                StartTimeBase = StartTimeBase.BaseOnUsaMarketOpen,  // Market close is sometimes 16:00, sometimes 13:00. Better to be relative to market open.
                StartTimeOffset = TimeSpan.FromMinutes((17-9.5) * 60 + 20), // 5min after release time 17:15 ET, which is 1h15min after market close. Market open: 9:30 ET.
                TriggerSettings = new Dictionary<object, object>() { { TaskSetting.ActionType, WsMonTaskSettingAction.SpIndexChanges } }
            });
            SqTaskScheduler.gSqTasks.Add(sqTask);
        }

        public void Exit()
        {
            Utils.Logger.Info("****WebsitesMonitor:Exit()");
        }
    }

    public class WebsitesMonitorExecution : SqExecution
    {
        public static SqExecution ExecutionFactoryCreate()
        {
            return new WebsitesMonitorExecution();
        }

        public override void Run()  // try/catch is not necessary, because sqExecution.Run() is wrapped around a try/catch with HealthMonitor notification in SqTrigger.cs
        {
            Utils.Logger.Info($"WebsitesMonitorExecution.Run() BEGIN, Trigger: '{Trigger!.Name}'");

            WsMonTaskSettingAction action = WsMonTaskSettingAction.Unknown;
            if (Trigger!.TriggerSettings.TryGetValue(TaskSetting.ActionType, out object? actionObj))
                action = (WsMonTaskSettingAction)actionObj;

             if (action == WsMonTaskSettingAction.SpIndexChanges)
                CheckSpIndexChanges();
        }

        private void CheckSpIndexChanges() // try/catch is not necessary, because sqExecution.Run() is wrapped around a try/catch with HealthMonitor notification in SqTrigger.cs
        {
            string url = "https://www.spglobal.com/spdji/en/indices/equity/sp-500/#news-research";
            Utils.DownloadStringWithRetry(url, out string webpage);

            StrongAssert.True(!String.IsNullOrEmpty(webpage), Severity.ThrowException, "Error in Overmind.CheckSpIndexChanges().DownloadStringWithRetry()");
            
            // "<li class=\"meta-data-date\">Mar 24, 2021</li>\n                                       <li class=\"meta-data-date\">5:15 PM</li>\n"
            // Skip the first split and assume every second <li> is a Date, every second <li> is a time for that day.
            // It is enough to check the first entry.

            // inefficient code doing string allocations
            // string[] split = webpage.Split("<li class=\"meta-data-date\">", StringSplitOptions.RemoveEmptyEntries);
            // string firstDateTime = split[1].Split("</li>\n")[0].Trim() + " " + split[2].Split("</li>\n")[0].Trim();

            int pos1 = webpage.IndexOf("<li class=\"meta-data-date\">") + "<li class=\"meta-data-date\">".Length;
            int pos2 = webpage.IndexOf("</li>\n", pos1);
            int pos3 = webpage.IndexOf("<li class=\"meta-data-date\">", pos2 + "</li>\n".Length) + "<li class=\"meta-data-date\">".Length;
            int pos4 = webpage.IndexOf("</li>\n", pos3);
            ReadOnlySpan<char> pageSpan1 = webpage.AsSpan().Slice(pos1, pos2 - pos1);
            ReadOnlySpan<char> pageSpan2 = webpage.AsSpan().Slice(pos3, pos4 - pos3);
            string firstDateTime = String.Concat(pageSpan1, " ", pageSpan2);

            Utils.Logger.Info($"CheckSpIndexChanges() firstDateTime: '{firstDateTime}'");
            DateTime firstDateET = DateTime.ParseExact(firstDateTime, "MMM dd, yyyy h:mm tt", null);
            bool isLatestNewsTodayAfterClose = firstDateET.Date == DateTime.UtcNow.FromUtcToEt().Date && firstDateET.Hour >= 16;
            if (isLatestNewsTodayAfterClose)
            {
                string subject = "SqCore: Potential S&P500 index change is detected!";
                StringBuilder sb = new StringBuilder(Email.g_htmlEmailStart);
                sb.Append(@"<span class=""sqImportantOK"">Potential <strong>S&P500 index change</strong> is detected!</span><br/><br/>");
                sb.Append($"The official S&P website has a <strong>new post</strong> published today in the last 5-10 minutes.<br/>");
                sb.Append($"The news items are often PDF files, English sentences, which cannot be processed by a program.<br/>");
                sb.Append($"A human reader is needed to understand if it is relevant.<br/><br/>");
                sb.Append($"<strong>Action: </strong><br/> Go to <a href=\"{url}\">the site</a> and read the latest news.");
                sb.Append(Email.g_htmlEmailEnd);
                string emailHtmlBody = sb.ToString();
                new Email { ToAddresses = Utils.Configuration["Emails:Gyant"], Subject = subject, Body = emailHtmlBody, IsBodyHtml = true }.Send();
                new Email { ToAddresses = Utils.Configuration["Emails:Charm0"], Subject = subject, Body = emailHtmlBody, IsBodyHtml = true }.Send();
            }
        }
    }
}