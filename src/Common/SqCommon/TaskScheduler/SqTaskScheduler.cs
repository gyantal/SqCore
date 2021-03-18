using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SqCommon
{
    
    public class SqTaskScheduler
    {
        public static SqTaskScheduler gTaskScheduler = new SqTaskScheduler();    // this is the Boss of the Virtual Broker bees. It schedules them.
        public static List<SqTask> gSqTasks = new List<SqTask>();  // the worker bees, the Trading Agents
    
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
                Thread.Sleep(TimeSpan.FromSeconds(5));  // wait 5 seconds, so that IBGateways can connect at first

                // maybe loop is not required.
                // in the past we try to get UsaMarketOpenOrCloseTime() every 30 minutes. It was determined from YFinance intrady. "sleep 30 min for DetermineUsaMarketOpenOrCloseTime()"
                // however, it may be a good idea that the Scheduler periodically wakes up and check Tasks
                const int cVbSchedulerSleepMinutes = 30;
                while (true) 
                {
                    Utils.Logger.Info($"SchedulerThreadRun() loop BEGIN. Awake at every {cVbSchedulerSleepMinutes}min.");
                    bool isMarketTradingDay;
                    DateTime marketOpenTimeUtc, marketCloseTimeUtc;
                    //  Utils.DetermineUsaMarketTradingHours():  may throw an exception once per year, when Nasdaq page changes. BrokerScheduler.SchedulerThreadRun() catches it and HealthMonitor notified in VBroker.
                    bool isMarketHoursValid = Utils.DetermineUsaMarketTradingHours(DateTime.UtcNow, out isMarketTradingDay, out marketOpenTimeUtc, out marketCloseTimeUtc, TimeSpan.FromDays(3));
                    if (!isMarketHoursValid)
                        Utils.Logger.Error("DetermineUsaMarketTradingHours() was not ok.");  // but we should continue and schedule Daily tasks not related to MarketTradingHours

                    foreach (SqTask taskSchema in gSqTasks)
                    {
                        foreach (SqTrigger trigger in taskSchema.Triggers)
                        {
                            ScheduleTrigger(trigger, isMarketHoursValid, isMarketTradingDay, marketOpenTimeUtc, marketCloseTimeUtc);
                        }
                    }

                    Thread.Sleep(TimeSpan.FromMinutes(cVbSchedulerSleepMinutes));     // try reschedulement in 30 minutes
                }
            }
            catch (Exception e)
            {
                //  Utils.DetermineUsaMarketTradingHours():  may throw an exception once per year, when Nasdaq page changes. BrokerScheduler.SchedulerThreadRun() catches it and HealthMonitor notified in VBroker.               
                HealthMonitorMessage.SendAsync($"Exception in Scheduler.RecreateTasksAndLoopThread. Exception: '{ e.ToStringWithShortenedStackTrace(400)}'", HealthMonitorMessageID.ReportErrorFromVirtualBroker).TurnAsyncToSyncTask();
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
                    if (timeSpan.TotalMilliseconds < 1000.0)    // if the proposedTime is not significantly different that the scheduledTime
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
            //Console.WriteLine($"{DateTime.UtcNow.ToString("dd'T'HH':'mm':'ss")}: Task '" + p_trigger.TriggeredTaskSchema.Name + "' next time: " + ((p_trigger.NextScheduleTimeUtc != null) ? ((DateTime)p_trigger.NextScheduleTimeUtc).ToString("dd'T'HH':'mm':'ss") : "null"));
            Utils.Logger.Info("Task '" + p_trigger.SqTask?.Name ?? string.Empty + "' next time: " + ((p_trigger.NextScheduleTimeUtc != null) ? ((DateTime)p_trigger.NextScheduleTimeUtc).ToString("dd'T'HH':'mm':'ss") : "null"));
        }

        private DateTime? CalcNextTriggerTime(SqTrigger p_trigger, bool p_isMarketHoursValid, bool p_isMarketTradingDay, DateTime p_marketOpenTimeUtc, DateTime p_marketCloseTimeUtc)
        {
            // if it is scheduled 5 seconds from now, just forget it (1 seconds was not enough)
            // once the timer was set to ellapse at 20:30:00, but it ellapsed at 20:29:58sec.5, so the trade was scheduled again, because it was later than 1 sec
            DateTime tresholdNowTime = DateTime.UtcNow.AddSeconds(5);
            DateTime proposedTime = DateTime.MinValue;

            if (p_trigger.TriggerType == TriggerType.Daily)
            {
                if (p_trigger.StartTimeBase == StartTimeBase.BaseOnAbsoluteTimeMidnightUtc)
                    proposedTime = DateTime.UtcNow.Date + p_trigger.StartTimeOffset;

            }
            else if (p_trigger.TriggerType == TriggerType.DailyOnUsaMarketDay)
            {
                if (p_isMarketHoursValid && p_isMarketTradingDay)  // in this case market open and close times are not given
                {
                    if (p_trigger.StartTimeBase == StartTimeBase.BaseOnUsaMarketOpen)
                        proposedTime = p_marketOpenTimeUtc + p_trigger.StartTimeOffset;

                    if (p_trigger.StartTimeBase == StartTimeBase.BaseOnUsaMarketClose)
                        proposedTime = p_marketCloseTimeUtc + p_trigger.StartTimeOffset;
                }
            }

            if (proposedTime > tresholdNowTime)
                return proposedTime;
            return null;
        }

        public StringBuilder GetNextScheduleTimes(bool p_isHtml)
        {
            StringBuilder sb = new StringBuilder();
            DateTime utcNow = DateTime.UtcNow;
            foreach (var taskSchema in gSqTasks)
            {
                DateTime nextTimeUtc = DateTime.MaxValue;
                foreach (var trigger in taskSchema.Triggers)
                {
                    if ((trigger.NextScheduleTimeUtc != null) && (trigger.NextScheduleTimeUtc > utcNow) && (trigger.NextScheduleTimeUtc < nextTimeUtc))
                        nextTimeUtc = (DateTime)trigger.NextScheduleTimeUtc;
                }

                sb.AppendLine($"{taskSchema.Name}: {nextTimeUtc.ToString("MM-dd HH:mm:ss")}{((p_isHtml) ? "<br>" : string.Empty)}");
            }
            return sb;
        }

        public void TestElapseTrigger(string p_taskName, int p_triggerInd)
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

        public void Exit()
        {
        }
    }

}