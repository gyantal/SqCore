using System;
using System.Collections.Generic;
using BrokerCommon;
using FinTechCommon;
using SqCommon;
using IBApi;

namespace SqCoreWeb
{
    public enum BrPrtfCheckerTaskSettingAction : byte
    {
        Unknown = 0, AtApplicationStartupCheck, OpenCheck, CloseCheck, PeriodicCheck
    }

    // Firstly, the service can check periodically and warn users if a position is out of order (bigger than -20% loss today)
    // Secondly, every 1h RTH, it stores the portfolio positions in MemDb, and Dashboard can quickly show it in the webpage
    public class BrPrtfChecker // shortened BrokerPortfolioPositionsChecker
    {
        public static BrPrtfChecker gBrPrtfChecker = new BrPrtfChecker();

        public void Init()
        {
            Utils.Logger.Info("****BrPrtfChecker:Init()");
            var sqTask = new SqTask()
            {
                Name = "BrPrtfChecker",
                ExecutionFactory = BrPrtfCheckerExecution.ExecutionFactoryCreate,
            };

            // trigger times: it is worth doing: 5 minutes after Open, 5 minutes after close, because that is when trades happen.
            // also schedule at every whole hours: 10:00, 11:00, ... but if it is OTH, only execute portfolio position updates if lastDate is more than 5h old.
            // user can also initiate forced update, but this is the automatic ones.

            sqTask.Triggers.Add(new SqTrigger()
            {
                Name = "AtApplicationStartupCheck",
                SqTask = sqTask,
                TriggerType = TriggerType.AtApplicationStartup,
                Start = new RelativeTime() { Base = RelativeTimeBase.Unknown, TimeOffset = TimeSpan.FromSeconds(30) },   // a bit later then App startup, to give time to Gateways to connect
                TriggerSettings = new Dictionary<object, object>() { { TaskSetting.ActionType, BrPrtfCheckerTaskSettingAction.AtApplicationStartupCheck } }
            });
            sqTask.Triggers.Add(new SqTrigger()
            {
                Name = "OpenCheck",
                SqTask = sqTask,
                TriggerType = TriggerType.DailyOnUsaMarketDay,
                Start = new RelativeTime() { Base = RelativeTimeBase.BaseOnUsaMarketOpen, TimeOffset = TimeSpan.FromMinutes(5) },
                TriggerSettings = new Dictionary<object, object>() { { TaskSetting.ActionType, BrPrtfCheckerTaskSettingAction.OpenCheck } }
            });
            sqTask.Triggers.Add(new SqTrigger()
            {
                Name = "CloseCheck",
                SqTask = sqTask,
                TriggerType = TriggerType.DailyOnUsaMarketDay,
                Start = new RelativeTime() { Base = RelativeTimeBase.BaseOnUsaMarketClose, TimeOffset = TimeSpan.FromMinutes(5) },
                TriggerSettings = new Dictionary<object, object>() { { TaskSetting.ActionType, BrPrtfCheckerTaskSettingAction.CloseCheck } }
            });
            // at every whole hours: 10:10, 11:10, ... but not in OTH, when positions will not change
            sqTask.Triggers.Add(new SqTrigger()
            {
                Name = "PeriodicCheck",
                SqTask = sqTask,
                TriggerType = TriggerType.Periodic,
                Start = new RelativeTime() { Base = RelativeTimeBase.BaseOnAbsoluteTimeAtEveryHourUtc, TimeOffset = TimeSpan.FromMinutes(10) },
                TriggerSettings = new Dictionary<object, object>() { { TaskSetting.ActionType, BrPrtfCheckerTaskSettingAction.PeriodicCheck } }
            });
            SqTaskScheduler.gSqTasks.Add(sqTask);
        }

        public void Exit()
        {
            Utils.Logger.Info("****BrPrtfChecker:Exit()");
        }
    }

    public class BrPrtfCheckerExecution : SqExecution
    {
        public static SqExecution ExecutionFactoryCreate()
        {
            return new BrPrtfCheckerExecution();
        }

        public override void Run()  // try/catch is only necessary if there is a non-awaited async that continues later in a different tPool thread. See comment in SqExecution.cs
        {
            Utils.Logger.Info($"BrPrtfCheckerExecution.Run() BEGIN, Trigger: '{Trigger!.Name}'");

            BrPrtfCheckerTaskSettingAction action = BrPrtfCheckerTaskSettingAction.Unknown;
            if (Trigger!.TriggerSettings.TryGetValue(TaskSetting.ActionType, out object? actionObj))
                action = (BrPrtfCheckerTaskSettingAction)actionObj;

            bool isPossUpdateNeeded = true;
            if (action == BrPrtfCheckerTaskSettingAction.PeriodicCheck)
            {
                // at every whole hours: 10:10, 11:10, ... but if it is OTH, we assume there is no position change.
                // it is last updated at 16:05 anyway, because of CloseCheck
                if (!Utils.IsInRegularUsaTradingHoursNow())
                    isPossUpdateNeeded = false;
            }
            if (isPossUpdateNeeded)
            {
                UpdateBrPrtfPoss(GatewayId.CharmatMain);
                UpdateBrPrtfPoss(GatewayId.DeBlanzacMain);
                UpdateBrPrtfPoss(GatewayId.GyantalMain);
                Console.WriteLine("BrokerPortfolios are updated.");
            }
        }

        private void UpdateBrPrtfPoss(GatewayId p_gatewayId)
        {
            List<AccSum>? accSums = BrokersWatcher.gWatcher.GetAccountSums(p_gatewayId);
            if (accSums == null)
                return;

            List<AccPos>? accPoss = BrokersWatcher.gWatcher.GetAccountPoss(p_gatewayId);
            if (accPoss == null)
                return;

            BrPortfolio? brPortfolio = null;
            foreach (var portfolio in MemDb.gMemDb.BrPortfolios)
            {
                if (portfolio.GatewayId == p_gatewayId)
                {
                    brPortfolio = portfolio;
                    break;
                }
            }
            if (brPortfolio == null)
            {
                brPortfolio = new BrPortfolio() { GatewayId = p_gatewayId };
                MemDb.gMemDb.BrPortfolios.Add(brPortfolio);
            }

            brPortfolio.NetLiquidation = accSums.GetValue(AccountSummaryTags.NetLiquidation);
            brPortfolio.GrossPositionValue = accSums.GetValue(AccountSummaryTags.GrossPositionValue);
            brPortfolio.TotalCashValue = accSums.GetValue(AccountSummaryTags.TotalCashValue);
            brPortfolio.InitMarginReq = accSums.GetValue(AccountSummaryTags.InitMarginReq);
            brPortfolio.MaintMarginReq = accSums.GetValue(AccountSummaryTags.MaintMarginReq);
            brPortfolio.AccPoss = accPoss;
            brPortfolio.LastUpdate = DateTime.UtcNow;
        }
    }

}