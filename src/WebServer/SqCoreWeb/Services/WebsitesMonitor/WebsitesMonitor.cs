using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using SqCommon;

namespace SqCoreWeb;

public enum WsMonTaskSettingAction : byte
{
    Unknown = 0,
    SpIndexChanges // Check S&P500 for index changes, addition, deletion for SP500, SP100, SP600, SP1500
}
public class WebsitesMonitor
{
    public static readonly WebsitesMonitor gWebsitesMonitor = new();

    public static void Init()
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
            // Market close is sometimes 16:00, sometimes 13:00. Better to be relative to market open.
            // 5min after release time 17:15 ET, which is 1h15min after market close. Market open: 9:30 ET.
            Start = new RelativeTime() { Base = RelativeTimeBase.BaseOnUsaMarketOpen, TimeOffset = TimeSpan.FromMinutes((17 - 9.5) * 60 + 20) },
            TriggerSettings = new Dictionary<object, object>() { { TaskSetting.ActionType, WsMonTaskSettingAction.SpIndexChanges } }
        });
        SqTaskScheduler.gSqTasks.Add(sqTask);
    }

    public static void Exit()
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

    public override void Run() // try/catch is only necessary if there is a non-awaited async that continues later in a different tPool thread. See comment in SqExecution.cs
    {
        Utils.Logger.Info($"WebsitesMonitorExecution.Run() BEGIN, Trigger: '{Trigger!.Name}'");

        WsMonTaskSettingAction action = WsMonTaskSettingAction.Unknown;
        if (Trigger!.TriggerSettings.TryGetValue(TaskSetting.ActionType, out object? actionObj))
            action = (WsMonTaskSettingAction)actionObj;

        if (action == WsMonTaskSettingAction.SpIndexChanges)
            CheckSpIndexChanges();
    }

    private static void CheckSpIndexChanges()
    {
        string url = "https://www.spglobal.com/spdji/en/indices/equity/sp-500/#news-research";
        string? webpage = Utils.DownloadStringWithRetryAsync(url).TurnAsyncToSyncTask();

        StrongAssert.True(!String.IsNullOrEmpty(webpage), Severity.ThrowException, "Error in Overmind.CheckSpIndexChanges(). DownloadStringWithRetry()");
        if (webpage!.Length < 20000)
        { // usually, it is 270K. If it is less than 50K, maybe an error message: "504 ERROR...The request could not be satisfied...CloudFront attempted to establish a connection with the origin"
            // once per month rarely we receive "<head><title>502 Bad Gateway</title></head>"
            // they have to restart their server so for 5-10 minutes, it is not available even in Chrome clients.
            // in this case, sleep for 10 min, then retry
            Utils.Logger.Warn($"CheckSpIndexChanges(). Page size is unexpectedly small: '{webpage}'");
            Thread.Sleep(TimeSpan.FromMinutes(10));
            webpage = Utils.DownloadStringWithRetryAsync(url).TurnAsyncToSyncTask();
            StrongAssert.True(!String.IsNullOrEmpty(webpage), Severity.ThrowException, "Error in Overmind.CheckSpIndexChanges(). 2x DownloadStringWithRetry()");
        }
        // "<li class=\"meta-data-date\">Mar 24, 2021</li>\n                                       <li class=\"meta-data-date\">5:15 PM</li>\n"
        // Skip the first split and assume every second <li> is a Date, every second <li> is a time for that day.
        // It is enough to check the first entry.

        // inefficient code doing string allocations
        // string[] split = webpage.Split("<li class=\"meta-data-date\">", StringSplitOptions.RemoveEmptyEntries);
        // string firstDateTime = split[1].Split("</li>\n")[0].Trim() + " " + split[2].Split("</li>\n")[0].Trim();

        int pos1 = webpage!.IndexOf("<li class=\"meta-data-date\">") + "<li class=\"meta-data-date\">".Length;
        int pos2 = webpage.IndexOf("</li>\n", pos1);
        int pos3 = webpage.IndexOf("<li class=\"meta-data-date\">", pos2 + "</li>\n".Length) + "<li class=\"meta-data-date\">".Length;
        int pos4 = webpage.IndexOf("</li>\n", pos3);
        ReadOnlySpan<char> pageSpan1 = webpage.AsSpan()[pos1..pos2]; // 'ReadOnlySpan<char>' cannot be declared in async methods or lambda expressions.
        ReadOnlySpan<char> pageSpan2 = webpage.AsSpan()[pos3..pos4];
        string firstDateTimeStr = String.Concat(pageSpan1, " ", pageSpan2);

        Utils.Logger.Info($"CheckSpIndexChanges() firstDateTime: '{firstDateTimeStr}'");
        DateTime firstDateET = ParseSpglobalDateStr(firstDateTimeStr);
        bool isLatestNewsTodayAfterClose = firstDateET.Date == DateTime.UtcNow.FromUtcToEt().Date && firstDateET.Hour >= 16;
        if (isLatestNewsTodayAfterClose)
        {
            string subject = "SqCore: Potential S&P500 index change is detected!";
            StringBuilder sb = new(Email.g_htmlEmailStart);
            sb.Append(@"<span class=""sqImportantOK"">Potential <strong>S&P500 index change</strong> is detected!</span><br/><br/>");
            sb.Append($"The official S&P website has a <strong>new post</strong> published today in the last 5-10 minutes.<br/>");
            sb.Append($"The news items are often PDF files, English sentences, which cannot be processed by a program.<br/>");
            sb.Append($"A human reader is needed to understand if it is relevant.<br/><br/>");
            sb.Append($"<strong>Action: </strong><br/> Go to <a href=\"{url}\">the site</a> and read the latest news.");
            sb.Append(Email.g_htmlEmailEnd);
            string emailHtmlBody = sb.ToString();
            new Email { ToAddresses = String.Concat(Utils.Configuration["Emails:Gyant"], ";", Utils.Configuration["Emails:Charm0"]), Subject = subject, Body = emailHtmlBody, IsBodyHtml = true }.Send();
        }
    }

    // xUnit test Examples: "Apr 5, 2021 5:15 PM"
    public static DateTime ParseSpglobalDateStr(string p_firstDateTime)
    {
        return DateTime.ParseExact(p_firstDateTime, "MMM d, yyyy h:mm tt", null);
    }
}