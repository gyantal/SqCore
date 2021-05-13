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
                //  Utils.DetermineUsaMarketTradingHours():  may throw an exception once per year, when Nasdaq page changes. BrokerScheduler.SchedulerThreadRun() catches it and HealthMonitor notified in VBroker.               
                HealthMonitorMessage.SendAsync($"Exception in Scheduler.RecreateTasksAndLoopThread. Exception: '{ e.ToStringWithShortenedStackTrace(1600)}'", HealthMonitorMessageID.SqCoreWebCsError).TurnAsyncToSyncTask();
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
            //Console.WriteLine($"{DateTime.UtcNow.ToString("dd'T'HH':'mm':'ss")}: Task '" + p_trigger.TriggeredTask.Name + "' next time: " + ((p_trigger.NextScheduleTimeUtc != null) ? ((DateTime)p_trigger.NextScheduleTimeUtc).ToString("dd'T'HH':'mm':'ss") : "null"));
            Utils.Logger.Info("Trigger '" + String.Concat(p_trigger.SqTask!.Name, ".", p_trigger.Name) + "' next time: " + ((p_trigger.NextScheduleTimeUtc != null) ? ((DateTime)p_trigger.NextScheduleTimeUtc).ToString("dd'T'HH':'mm':'ss") : "null"));
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

        public StringBuilder PrintNextScheduleTimes(bool p_isHtml)  // Get is better word for getting DateTimes[], Print shows it receives a string
        {
            List<(DateTime NextTimeUtc, string Name)> nextTimes = new List<(DateTime NextTimeUtc, string Name)>(); // named tuples
            foreach (var sqTask in gSqTasks)
            {
                foreach (var trigger in sqTask.Triggers)
                {
                    nextTimes.Add((trigger.NextScheduleTimeUtc ?? DateTime.MaxValue, String.Concat(sqTask.Name, ".", trigger.Name)));
                }
            }
            nextTimes.Sort(); // in-place. Tuple<T1, T2> documented to sort by Item1 and then Item2

            DateTime utcNow = DateTime.UtcNow;
            StringBuilder sb = new StringBuilder();
            foreach (var nextTime in nextTimes)
            {
                string nextTimeUtcStr = (nextTime.NextTimeUtc != DateTime.MaxValue && nextTime.NextTimeUtc > utcNow) ? nextTime.NextTimeUtc.TohMMDDHHMMSS() + " (UTC)": "---";
                sb.AppendLine($"{nextTimeUtcStr}: {nextTime.Name}{((p_isHtml) ? "<br>" : string.Empty)}");
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