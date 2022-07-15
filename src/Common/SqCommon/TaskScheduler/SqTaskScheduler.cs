using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SqCommon;

public class SqTaskScheduler
{
    public static SqTaskScheduler gTaskScheduler = new();    // this is the Boss of the Virtual Broker bees. It schedules them.
    public static List<SqTask> gSqTasks = new();  // the worker bees, the Trading Agents
    const int cVbSchedulerSleepMinutes = 30;
    DateTime m_schedulerStartupTime = DateTime.MaxValue;

    public void Init()
    {
        Utils.Logger.Info("****Scheduler:Init()");
        Task schedulerTask = Task.Factory.StartNew(SchedulerThreadRun, TaskCreationOptions.LongRunning).LogUnobservedTaskExceptions("BrokerScheduler.SchedulerThreadRun");  // a separate thread. Not on ThreadPool
    }

    private void SchedulerThreadRun()
    {
        try
        {
            Thread.CurrentThread.Name = "VBroker scheduler";
            Thread.Sleep(TimeSpan.FromSeconds(1));  // wait 1 seconds, so that IBGateways can connect first. DetermineUsaMarketTradingHours() will be slow anyway as it downloads a webpage
            m_schedulerStartupTime = DateTime.UtcNow;

            // maybe loop is not required.
            // in the past we try to get UsaMarketOpenOrCloseTime() every 30 minutes. It was determined from YFinance intrady. "sleep 30 min for DetermineUsaMarketOpenOrCloseTime()"
            // however, it may be a good idea that the Scheduler periodically wakes up and check Tasks
            while (true)
            {
                Utils.Logger.Info($"SchedulerThreadRun() loop BEGIN. Awake at every {cVbSchedulerSleepMinutes}min.");
                // Utils.DetermineUsaMarketTradingHours():  may throw an exception once per year, when Nasdaq page changes. BrokerScheduler.SchedulerThreadRun() catches it and HealthMonitor notified in VBroker.
                bool isMarketHoursValid = Utils.DetermineUsaMarketTradingHours(DateTime.UtcNow, out bool isMarketTradingDay, out DateTime marketOpenTimeUtc, out DateTime marketCloseTimeUtc, TimeSpan.FromDays(3));
                if (!isMarketHoursValid)
                    Utils.Logger.Error("DetermineUsaMarketTradingHours() was not ok.");  // but we should continue and schedule Daily tasks not related to MarketTradingHours

                foreach (SqTask sqTask in gSqTasks)
                {
                    foreach (SqTrigger trigger in sqTask.Triggers)
                    {
                        ScheduleTrigger(trigger, isMarketHoursValid, isMarketTradingDay, marketOpenTimeUtc, marketCloseTimeUtc);
                    }
                }

                Thread.Sleep(TimeSpan.FromMinutes(cVbSchedulerSleepMinutes));     // try reschedulement in 30 minutes
            }
        }
        catch (Exception e)
        {
            // Utils.DetermineUsaMarketTradingHours():  may throw an exception once per year, when Nasdaq page changes. BrokerScheduler.SchedulerThreadRun() catches it and HealthMonitor notified in VBroker.
            HealthMonitorMessage.SendAsync($"Exception in Scheduler.RecreateTasksAndLoopThread. Exception: '{e.ToStringWithShortenedStackTrace(1600)}'", HealthMonitorMessageID.SqCoreWebCsError).TurnAsyncToSyncTask();
        }
    }

    public void ScheduleTrigger(SqTrigger p_trigger, bool p_isMarketHoursValid, bool p_isMarketTradingDay, DateTime p_marketOpenTimeUtc, DateTime p_marketCloseTimeUtc)
    {
        DateTime? proposedTime = CalcNextTriggerTime(p_trigger, p_isMarketHoursValid, p_isMarketTradingDay, p_marketOpenTimeUtc, p_marketCloseTimeUtc);
        if (proposedTime != null)
        {
            bool doSetTimer = true;
            if (p_trigger.NextScheduleTimeUtc != null)
            {
                TimeSpan timeSpan = ((DateTime)p_trigger.NextScheduleTimeUtc > (DateTime)proposedTime) ? (DateTime)p_trigger.NextScheduleTimeUtc - (DateTime)proposedTime : (DateTime)proposedTime - (DateTime)p_trigger.NextScheduleTimeUtc;
                if (timeSpan.TotalMilliseconds < 1000.0) // if the proposedTime is not significantly different that the scheduledTime
                    doSetTimer = false;
            }
            if (doSetTimer)
            {
                p_trigger.NextScheduleTimeUtc = proposedTime;

                StrongAssert.True((DateTime)p_trigger.NextScheduleTimeUtc > DateTime.UtcNow, Severity.ThrowException, "nextScheduleTimeUtc > DateTime.UtcNow");
                p_trigger.Timer.Change((DateTime)p_trigger.NextScheduleTimeUtc - DateTime.UtcNow, TimeSpan.FromMilliseconds(-1.0));
            }
        }
        // Warn() temporarily to show it on Console
        // Console.WriteLine($"{DateTime.UtcNow.ToString("dd'T'HH':'mm':'ss")}: Task '" + p_trigger.TriggeredTask.Name + "' next time: " + ((p_trigger.NextScheduleTimeUtc != null) ? ((DateTime)p_trigger.NextScheduleTimeUtc).ToString("dd'T'HH':'mm':'ss") : "null"));
        Utils.Logger.Info("Trigger '" + String.Concat(p_trigger.SqTask!.Name, ".", p_trigger.Name) + "' next time: " + ((p_trigger.NextScheduleTimeUtc != null) ? ((DateTime)p_trigger.NextScheduleTimeUtc).ToString("dd'T'HH':'mm':'ss") : "null"));
    }

    private DateTime? CalcNextTriggerTime(SqTrigger p_trigger, bool p_isMarketHoursValid, bool p_isMarketTradingDay, DateTime p_marketOpenTimeUtc, DateTime p_marketCloseTimeUtc)
    {
        // if it is scheduled 3 seconds from now, just forget it (1 seconds was not enough)
        // once the timer was set to ellapse at 20:30:00, but it ellapsed at 20:29:58sec.5, so the trade was scheduled again, because it was later than 1 sec
        DateTime tresholdNowTime = DateTime.UtcNow.AddSeconds(3);
        DateTime proposedTime = DateTime.MinValue;

        if (p_trigger.TriggerType == TriggerType.Daily)
        {
            if (p_trigger.Start.Base == RelativeTimeBase.BaseOnAbsoluteTimeMidnightUtc)
                proposedTime = DateTime.UtcNow.Date + p_trigger.Start.TimeOffset;
        }
        else if (p_trigger.TriggerType == TriggerType.DailyOnUsaMarketDay)
        {
            if (p_isMarketHoursValid && p_isMarketTradingDay) // in this case market open and close times are not given
            {
                if (p_trigger.Start.Base == RelativeTimeBase.BaseOnUsaMarketOpen)
                    proposedTime = p_marketOpenTimeUtc + p_trigger.Start.TimeOffset;

                if (p_trigger.Start.Base == RelativeTimeBase.BaseOnUsaMarketClose)
                    proposedTime = p_marketCloseTimeUtc + p_trigger.Start.TimeOffset;
            }
        }
        else if (p_trigger.TriggerType == TriggerType.AtApplicationStartup)
        {
            proposedTime = m_schedulerStartupTime + p_trigger.Start.TimeOffset;
        }
        else if (p_trigger.TriggerType == TriggerType.Periodic)
        {
            // ScheduleTrigger() runs in every 30min.
            // if (p_trigger.NextScheduleTimeUtc) is already set in the Future, then don't change it. Otherwise DateTime.UtcNow.AddHours(1) would always push that more into the future and it never runs.
            DateTime utcNow = DateTime.UtcNow;
            bool isAlreadySetInTheFuture = false;
            if (p_trigger.NextScheduleTimeUtc != null)
                isAlreadySetInTheFuture = p_trigger.NextScheduleTimeUtc - utcNow > TimeSpan.Zero;

            if (p_trigger.Start.Base == RelativeTimeBase.BaseOnAbsoluteTimeAtEveryHourUtc)
            {
                if (!isAlreadySetInTheFuture)
                {
                    DateTime pastWholeHour = new(utcNow.Year, utcNow.Month, utcNow.Day, utcNow.Hour, 0, 0); // if utcNow is 20:10 and TimeOffset = 40 min of every whole hours, then we schedule 20:40
                    proposedTime = pastWholeHour + p_trigger.Start.TimeOffset;
                    if (proposedTime <= tresholdNowTime)
                        proposedTime = proposedTime.AddHours(1);
                }
            }
        }
        else
        {
            throw new NotImplementedException();
        }

        if (proposedTime > tresholdNowTime)
            return proposedTime;
        return null;
    }

    public static StringBuilder PrintNextScheduleTimes(bool p_isHtml) // Get is better word for getting DateTimes[], Print shows it receives a string
    {
        List<(DateTime NextTimeUtc, string Name)> nextTimes = new(); // named tuples
        foreach (var sqTask in gSqTasks)
        {
            foreach (var trigger in sqTask.Triggers)
            {
                nextTimes.Add((trigger.NextScheduleTimeUtc ?? DateTime.MaxValue, String.Concat(sqTask.Name, ".", trigger.Name)));
            }
        }
        nextTimes.Sort(); // in-place. Tuple<T1, T2> documented to sort by Item1 and then Item2

        DateTime utcNow = DateTime.UtcNow;
        StringBuilder sb = new();
        foreach (var (nextTimeUtc, name) in nextTimes)
        {
            string nextTimeUtcStr = (nextTimeUtc != DateTime.MaxValue && nextTimeUtc > utcNow) ? nextTimeUtc.TohMMDDHHMMSS() + " (UTC)" : "---";
            sb.AppendLine($"{nextTimeUtcStr}: {name}{(p_isHtml ? "<br>" : string.Empty)}");
        }
        return sb;
    }

    public static void TestElapseTrigger(string p_taskName, int p_triggerInd)
    {
        var sqTask = gSqTasks.Find(r => r.Name == p_taskName);
        if (sqTask == null)
        {
            Console.WriteLine("No such SqTask.");
            return;
        }
        if (p_triggerInd >= 0 && p_triggerInd < sqTask.Triggers.Count)
            sqTask.Triggers[p_triggerInd].Timer_Elapsed(null);
    }

    public static void Exit()
    {
    }
}