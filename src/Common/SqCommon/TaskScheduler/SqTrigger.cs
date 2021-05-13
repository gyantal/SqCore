using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

// A common base used both in VBroker and HealthMonitor (E.g. HealthMonitor checks that VBroker OK message arrived properly from the Expected Strategy at the expected time. If not, it sends warning email.)
namespace SqCommon
{
    
    public enum TriggerType : byte
    {   // similar to Windows TaskScheduler
        // On a schedule: 
        OneTime, Daily, DailyOnUsaMarketDay, DailyOnUkMarketDay, Weekly, Monthly,
        // On an event:
        AtApplicationStartup, AtApplicationExit, OnGatewayDisconnectionEvent, OnError, Unknown
    };
    public enum StartTimeBase : byte { BaseOnAbsoluteTimeMidnightUtc, BaseOnUsaMarketOpen, BaseOnUsaMarketClose, Unknown };

    public enum TaskSetting // general Task settings valid for all Triggers or it can be specific TriggerSettings
    {
        ActionType,     // LeverageCheckerAlert, MarketFallMoreThan2PercentAlert, etc.
        ActionParams   // global for all portfolios 
    }
    
    public class SqTrigger
    {
        public string Name { get; set; } = string.Empty;
        public bool Enabled { get; set; }
        public TriggerType TriggerType { get; set; }   // currently only Daily supported
        public int RepeatEveryXSeconds { get; set; } // -1, if not Recur
        public StartTimeBase StartTimeBase { get; set; }
        public TimeSpan StartTimeOffset { get; set; }
        public DateTime StartTimeExplicitUtc { get; set; }
        public Dictionary<object, object> TriggerSettings { get; set; } = new Dictionary<object, object>(); // changing TriggerSettings should be merged with general TaskSettings

        public DateTime? NextScheduleTimeUtc { get; set; }
        public Timer Timer { get; set; }

        public SqTask? SqTask { get; set; }

        public SqTrigger()
        {
            Timer = new System.Threading.Timer(new TimerCallback(Timer_Elapsed), null, TimeSpan.FromMilliseconds(-1.0), TimeSpan.FromMilliseconds(-1.0));
        }

        public virtual void Timer_Elapsed(object? state)    // Timer is coming on a ThreadPool thread
        {
            Utils.Logger.Info("Trigger.Timer_Elapsed() ");
            NextScheduleTimeUtc = null;

            bool isMarketTradingDay;
            DateTime marketOpenTimeUtc, marketCloseTimeUtc;
            bool isMarketHoursValid = Utils.DetermineUsaMarketTradingHours(DateTime.UtcNow, out isMarketTradingDay, out marketOpenTimeUtc, out marketCloseTimeUtc, TimeSpan.FromDays(3));
            if (!isMarketHoursValid)
                Utils.Logger.Error("DetermineUsaMarketTradingHours() was not ok.");
            
            SqTaskScheduler.gTaskScheduler.ScheduleTrigger(this, isMarketHoursValid, isMarketTradingDay, marketOpenTimeUtc, marketCloseTimeUtc);

            try
            {
                if (SqTask != null)
                {
                    SqExecution sqExecution = ((SqTask)SqTask).ExecutionFactory();
                    sqExecution.SqTask = (SqTask)SqTask;
                    sqExecution.Trigger = this;
                    sqExecution.Run();
                }
            }
            catch (Exception e)
            {
                HealthMonitorMessage.SendAsync($"Exception in BrokerTaskExecutionThreadRun(). Exception: '{ e.ToStringWithShortenedStackTrace(1600)}'", HealthMonitorMessageID.SqCoreWebCsError).TurnAsyncToSyncTask();
            }
        }

    }
}
