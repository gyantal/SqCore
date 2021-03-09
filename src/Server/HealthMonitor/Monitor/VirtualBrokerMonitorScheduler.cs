using SqCommon;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace HealthMonitor
{
    public class HmVbTrigger : TriggerBase
    {
        public HmVbTrigger() : base()
        {
        }

        public override void Timer_Elapsed(object? state)    // Timer is coming on a ThreadPool thread, Check that the UberVXX or HarryLong Task sent us the OK with CheckOKMessageArrived() 
        {
            Utils.Logger.Info("HmVbTrigger.Timer_Elapsed() 1");
            NextScheduleTimeUtc = null;

            bool isMarketTradingDay;
            DateTime marketOpenTimeUtc, marketCloseTimeUtc;
            bool isTradingHoursOK = Utils.DetermineUsaMarketTradingHours(DateTime.UtcNow, out isMarketTradingDay, out marketOpenTimeUtc, out marketCloseTimeUtc, TimeSpan.FromDays(3));
            if (!isTradingHoursOK)
            {
                Utils.Logger.Error("DetermineUsaMarketTradingHours() was not ok.");
            }
            else
            {
                HealthMonitor.ScheduleHmVbTrigger(this, isMarketTradingDay, marketOpenTimeUtc, marketCloseTimeUtc);
            }

            // Do something short in this Threadpool thread
            // Check that VBroker OK message arrived properly from the Expected Strategy. Different Tasks may take more time to execute
            Utils.Logger.Info("HmVbTrigger.Timer_Elapsed() 2");
            if (TriggeredTaskSchema != null && (TriggeredTaskSchema.Name == "UberVXX" || TriggeredTaskSchema.Name == "HarryLong"))
            {
                Thread.Sleep(TimeSpan.FromMinutes(5));

                DateTime utcStart = DateTime.UtcNow.AddMinutes(-6);
                HealthMonitor.g_healthMonitor.CheckOKMessageArrived(utcStart, TriggeredTaskSchema.Name);
            }
        }
    }

    // Another feauture that was not implemented. Maybe not needed ever:
    //Periodically check that VirtualBroker is alive and ready(every 60 minutes, if no answer for second time, make phonecall). Skip this one. Reasons:
    //- not easy to implement.VBroker should be happy to get incoming Tcp messages
    //- that is unusual, and it means I have to play with security, allowing another IP to access the VBroker server. But when IPs change, that is a lot of hassle
    //- currently, there are 2 extra simulations intraday. If VbServer is down, the simulation that is 30min before marketClose would be spotted by HealthMon.
    //- this feature can be mimicked: buy doing UberVXX task Simulation every hour.In that case VBrokerMonitorScheduler will notice that OK message didn't come in the last hour. 
    //- Sum: this feauture is not necessary, and takes time to implement.Don't do it now.
    public partial class HealthMonitor
    {

        internal void InitVbScheduler()
        {
            Utils.Logger.Info("VbScheduler:Init()");
            Task schedulerTask = Task.Factory.StartNew(VbSchedulerThreadRun, TaskCreationOptions.LongRunning).LogUnobservedTaskExceptions("HealthMonitor.VbSchedulerThreadRun");  // a separate thread. Not on ThreadPool
        }

        private void VbSchedulerThreadRun()
        {
            try
            {
                Thread.CurrentThread.Name = "VBroker scheduler";
                //Thread.Sleep(TimeSpan.FromSeconds(5));  // wait 5 seconds, so that IBGateways can connect at first

                List<TriggeredTaskSchema> taskSchemas = new List<TriggeredTaskSchema>();
                var uberVxxTaskSchema = new TriggeredTaskSchema()
                {
                    Name = "UberVXX",
                    NameForTextToSpeech = "Uber V X X "
                };
                uberVxxTaskSchema.Triggers.Add(new HmVbTrigger()
                {
                    TriggeredTaskSchema = uberVxxTaskSchema,
                    TriggerType = TriggerType.DailyOnUsaMarketDay,
                    StartTimeBase = StartTimeBase.BaseOnUsaMarketOpen,
                    StartTimeOffset = TimeSpan.FromMinutes(25),
                });
                uberVxxTaskSchema.Triggers.Add(new HmVbTrigger()
                {
                    TriggeredTaskSchema = uberVxxTaskSchema,
                    TriggerType = TriggerType.DailyOnUsaMarketDay,
                    StartTimeBase = StartTimeBase.BaseOnUsaMarketClose,
                    StartTimeOffset = TimeSpan.FromMinutes(-35),
                });
                uberVxxTaskSchema.Triggers.Add(new HmVbTrigger()
                {
                    TriggeredTaskSchema = uberVxxTaskSchema,
                    TriggerType = TriggerType.DailyOnUsaMarketDay,
                    StartTimeBase = StartTimeBase.BaseOnUsaMarketClose,
                    StartTimeOffset = TimeSpan.FromSeconds(-15),    // from -20sec to -15sec. From start, the trade executes in 2seconds
                });
                taskSchemas.Add(uberVxxTaskSchema);


                var harryLongTaskSchema = new TriggeredTaskSchema()
                {
                    Name = "HarryLong",
                    NameForTextToSpeech = "Harry Long "
                };
                harryLongTaskSchema.Triggers.Add(new HmVbTrigger()
                {
                    TriggeredTaskSchema = harryLongTaskSchema,
                    TriggerType = TriggerType.DailyOnUsaMarketDay,
                    StartTimeBase = StartTimeBase.BaseOnUsaMarketOpen,
                    StartTimeOffset = TimeSpan.FromMinutes(30) 
                });
                harryLongTaskSchema.Triggers.Add(new HmVbTrigger()
                {
                    TriggeredTaskSchema = harryLongTaskSchema,
                    TriggerType = TriggerType.DailyOnUsaMarketDay,
                    StartTimeBase = StartTimeBase.BaseOnUsaMarketClose,
                    StartTimeOffset = TimeSpan.FromMinutes(-31)
                });
                harryLongTaskSchema.Triggers.Add(new HmVbTrigger()
                {
                    TriggeredTaskSchema = harryLongTaskSchema,
                    TriggerType = TriggerType.DailyOnUsaMarketDay,
                    StartTimeBase = StartTimeBase.BaseOnUsaMarketClose,
                    StartTimeOffset = TimeSpan.FromSeconds(-11)    // Give UberVXX priority (executing at -15sec). That is more important because that can change from full 100% long to -200% short. This Harry Long strategy just slowly modifies weights, so if one trade is missed, it is not a problem.
                });
                taskSchemas.Add(harryLongTaskSchema);

                // maybe loop is not required.
                // in the past we try to get UsaMarketOpenOrCloseTime() every 30 minutes. It was determined from YFinance intrady. "sleep 30 min for DetermineUsaMarketOpenOrCloseTime()"
                // however, it may be a good idea that the Scheduler periodically wakes up and check Tasks
                const int cVbSchedulerSleepMinutes = 30;
                while (true)
                {
                    Utils.Logger.Info($"VbSchedulerThreadRun() loop BEGIN. Awake at every {cVbSchedulerSleepMinutes}min.");
                    bool isMarketTradingDay;
                    DateTime marketOpenTimeUtc, marketCloseTimeUtc;
                    bool isTradingHoursOK = Utils.DetermineUsaMarketTradingHours(DateTime.UtcNow, out isMarketTradingDay, out marketOpenTimeUtc, out marketCloseTimeUtc, TimeSpan.FromDays(3));
                    if (!isTradingHoursOK)
                    {
                        Utils.Logger.Error("DetermineUsaMarketTradingHours() was not ok.");
                    }
                    else
                    {
                        foreach (var taskSchema in taskSchemas)
                        {
                            foreach (var trigger in taskSchema.Triggers)
                            {
                                ScheduleHmVbTrigger(trigger, isMarketTradingDay, marketOpenTimeUtc, marketCloseTimeUtc);
                            }
                        }
                    }

                    Thread.Sleep(TimeSpan.FromMinutes(cVbSchedulerSleepMinutes));     // try reschedulement in 30 minutes
                }
            }
            catch (Exception e)
            {
                Utils.Logger.Error(e, "VbSchedulerThreadRun Thread");
                //HealthMonitorMessage.SendException("BrokerScheduler.RecreateTasksAndLoop Thread", e, HealthMonitorMessageID.ReportErrorFromVirtualBroker);
            }
        }

        public static void ScheduleHmVbTrigger(TriggerBase p_trigger, bool p_isMarketTradingDay, DateTime p_marketOpenTimeUtc, DateTime p_marketCloseTimeUtc)
        {
            DateTime? proposedTime = CalcNextHmVbTriggerTime(p_trigger, p_isMarketTradingDay, p_marketOpenTimeUtc, p_marketCloseTimeUtc);
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
            String msg = "Schedule HealthMon-Task '" + (p_trigger.TriggeredTaskSchema?.Name ?? String.Empty) + "' next time: " + ((p_trigger.NextScheduleTimeUtc != null) ? ((DateTime)p_trigger.NextScheduleTimeUtc).ToString("dd'T'HH':'mm':'ss") : "null");
            //Console.WriteLine($"{DateTime.UtcNow.ToString("dd'T'HH':'mm':'ss")}: HealthMon-Task '" + p_trigger.TriggeredTaskSchema.Name + "' next time: " + ((p_trigger.NextScheduleTimeUtc != null) ? ((DateTime)p_trigger.NextScheduleTimeUtc).ToString("dd'T'HH':'mm':'ss") : "null"));
            Utils.Logger.Info(msg);
        }

        public static DateTime? CalcNextHmVbTriggerTime(TriggerBase p_trigger, bool p_isMarketTradingDay, DateTime p_marketOpenTimeUtc, DateTime p_marketCloseTimeUtc)
        {
            if (!p_isMarketTradingDay)  // in this case market open and close times are not given
                return null;

            // if it is scheduled 5 seconds from now, just forget it (1 seconds was not enough)
            // once the timer was set to ellapse at 20:30:00, but it ellapsed at 20:29:58sec.5, so the trade was scheduled again, because it was later than 1 sec
            DateTime tresholdNowTime = DateTime.UtcNow.AddSeconds(5);

            if (p_trigger.StartTimeBase == StartTimeBase.BaseOnUsaMarketOpen)
            {
                DateTime proposedTime = p_marketOpenTimeUtc + p_trigger.StartTimeOffset;
                if (proposedTime > tresholdNowTime)
                    return proposedTime;
            }

            if (p_trigger.StartTimeBase == StartTimeBase.BaseOnUsaMarketClose)
            {
                DateTime proposedTime = p_marketCloseTimeUtc + p_trigger.StartTimeOffset;
                if (proposedTime > tresholdNowTime)
                    return proposedTime;
            }
            return null;
        }


        internal void ExitVbScheduler()
        {
        }

    }
}
