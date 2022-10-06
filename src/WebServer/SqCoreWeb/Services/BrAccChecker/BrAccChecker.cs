using System;
using System.Collections.Generic;
using BrokerCommon;
using FinTechCommon;
using SqCommon;

namespace SqCoreWeb;

public enum BrAccCheckerTaskSettingAction : byte
{
    Unknown = 0, AtApplicationStartupCheck, OpenCheck, CloseCheck, PeriodicCheck
}

// Firstly, the service can check periodically and warn users if a position is out of order (bigger than -20% loss today)
// Secondly, every 1h RTH, it stores the portfolio positions in MemDb, and Dashboard can quickly show it in the webpage
public class BrAccChecker // shortened BrokerAccountPositionsChecker
{
    public static readonly BrAccChecker gBrAccChecker = new();

    public void Init()
    {
        Utils.Logger.Info("****BrAccChecker:Init()");
        var sqTask = new SqTask()
        {
            Name = "BrAccChecker",
            ExecutionFactory = BrAccCheckerExecution.ExecutionFactoryCreate,
        };

        // trigger times: it is worth doing: 5 minutes after Open, 5 minutes after close, because that is when trades happen.
        // also schedule at every whole hours: 10:00, 11:00, ... but if it is OTH, only execute portfolio position updates if lastDate is more than 5h old.
        // user can also initiate forced update, but this is the automatic ones.

        // Later it was implemented that MemDb.gMemDb.UpdateBrAccount()-s were called when BOTH Gateways and MemDb is Initialized at Init(), but before, this fixed 5 seconds was fine.
        // sqTask.Triggers.Add(new SqTrigger()
        // {
        //     Name = "AtApplicationStartupCheck",
        //     SqTask = sqTask,
        //     TriggerType = TriggerType.AtApplicationStartup,
        //     Start = new RelativeTime() { Base = RelativeTimeBase.Unknown, TimeOffset = TimeSpan.FromSeconds(5) },   // a bit later then App startup, to give time to Gateways to connect
        //     TriggerSettings = new Dictionary<object, object>() { { TaskSetting.ActionType, BrAccCheckerTaskSettingAction.AtApplicationStartupCheck } }
        // });
        sqTask.Triggers.Add(new SqTrigger()
        {
            Name = "OpenCheck",
            SqTask = sqTask,
            TriggerType = TriggerType.DailyOnUsaMarketDay,
            Start = new RelativeTime() { Base = RelativeTimeBase.BaseOnUsaMarketOpen, TimeOffset = TimeSpan.FromMinutes(5) },
            TriggerSettings = new Dictionary<object, object>() { { TaskSetting.ActionType, BrAccCheckerTaskSettingAction.OpenCheck } }
        });
        sqTask.Triggers.Add(new SqTrigger()
        {
            Name = "CloseCheck",
            SqTask = sqTask,
            TriggerType = TriggerType.DailyOnUsaMarketDay,
            Start = new RelativeTime() { Base = RelativeTimeBase.BaseOnUsaMarketClose, TimeOffset = TimeSpan.FromMinutes(5) },
            TriggerSettings = new Dictionary<object, object>() { { TaskSetting.ActionType, BrAccCheckerTaskSettingAction.CloseCheck } }
        });
        // at every whole hours: 10:10, 11:10, ... but not in OTH, when positions will not change
        sqTask.Triggers.Add(new SqTrigger()
        {
            Name = "PeriodicCheck",
            SqTask = sqTask,
            TriggerType = TriggerType.Periodic,
            Start = new RelativeTime() { Base = RelativeTimeBase.BaseOnAbsoluteTimeAtEveryHourUtc, TimeOffset = TimeSpan.FromMinutes(30) },
            TriggerSettings = new Dictionary<object, object>() { { TaskSetting.ActionType, BrAccCheckerTaskSettingAction.PeriodicCheck } }
        });
        SqTaskScheduler.gSqTasks.Add(sqTask);
    }

    public void Exit()
    {
        Utils.Logger.Info("****BrAccChecker:Exit()");
    }
}

public class BrAccCheckerExecution : SqExecution
{
    public static SqExecution ExecutionFactoryCreate()
    {
        return new BrAccCheckerExecution();
    }

    public override void Run() // try/catch is only necessary if there is a non-awaited async that continues later in a different tPool thread. See comment in SqExecution.cs
    {
        Utils.Logger.Info($"BrAccCheckerExecution.Run() BEGIN, Trigger: '{Trigger!.Name}'");

        BrAccCheckerTaskSettingAction action = BrAccCheckerTaskSettingAction.Unknown;
        if (Trigger!.TriggerSettings.TryGetValue(TaskSetting.ActionType, out object? actionObj))
            action = (BrAccCheckerTaskSettingAction)actionObj;

        bool isPossUpdateNeeded = true;
        if (action == BrAccCheckerTaskSettingAction.PeriodicCheck)
        {
            // at every whole hours: 10:10, 11:10, ... but if it is OTH, we assume there is no position change.
            // it is last updated at 16:05 anyway, because of CloseCheck
            if (!Utils.IsInRegularUsaTradingHoursNow())
                isPossUpdateNeeded = false;
        }
        if (isPossUpdateNeeded)
        {
            Console.WriteLine("*Broker data (accInfo, positions) is getting acquired...");
            MemDb.gMemDb.UpdateBrAccount(GatewayId.CharmatMain);
            MemDb.gMemDb.UpdateBrAccount(GatewayId.DeBlanzacMain);
            MemDb.gMemDb.UpdateBrAccount(GatewayId.GyantalMain);
            Console.WriteLine("*Broker data (accInfo, positions) is ready.");
        }
    }
}