using System;
using System.Collections.Generic;
using System.Threading;
using SqCommon;
using StackExchange.Redis;
using Microsoft.Extensions.Primitives;
using System.Threading.Tasks;
using BrokerCommon;
using System.Linq;

namespace FinTechCommon
{
    // - Created a Google Calendar item:
    // Subject: SqCore Maintenance: Update deposit/withdrawal & daily NAV history annually in RedisDb
    // Text: "
    // SqCore saves daily NAV values that it tries to query every day at 16:00 ET (but it is not accurate)  
    // SqCore RedisDb is not updated automatically with Deposit/Withdrawal data. If there is a deposit into the account, the NAV value will go up, and it is stored in RedisDb. Without knowing that it came from Deposit, SqCore will think it come from trading profit. Giving a false impression of the real profit. 
    // In theory, we should update the RedisDb with Deposit data every time there was a deposit into the account. (Otherwise, it is seen as a trading profit.) If the deposit is small, this can be neglected or just done once per year.
    // If there is no deposit during the year, we don't have to do anything. The NAV values were derived from the real-time NAV at 16:00 ET, which are slightly inaccurate, but not important.
    // ----------------------------------
    // IB TWS API doesn't have the functionality to retrieve the deposit/withdrawal or daily NAV or order history.
    // IB Gateway or TWS doesn't have this data themselves. It can only be done on the website by Flex queries, which require user interaction.
    // https://stackoverflow.com/questions/48942917/interactive-brokers-how-to-retrieve-transaction-history-records
    // https://www.interactivebrokers.com/en/software/am/am/funding/viewingtransactionhistory.htm
    // Writing a program to login to the website, to simulate browser interaction, mouse movements, can take 2 weeks (100h) and it is a moving target, which requires future maintenance. As deposits and withdrawals are not frequent, a more time-saving method to do it once per year manually. (1h per year).
    // What to do:
    // (see G:\work\Archi-data\GitHubRepos\SqCore\src\Tools\RedisManager\Controller.cs)
    // // How to create the CVS containing NAV data + deposit?
    // // IB: PortfolioAnalyst/Reports/CreateCustomReport (SinceInception, Daily, Detailed + AccountOverview/Allocation by Financial Instrument/Deposits). Create in PDF + CSV.
    // Start SqCore/Tools/RedisManager.exe and run InsertNavAssetFromCsvFile()
    // While Debugging, put a breakpoint and check that outputCsv (NAVs) and depositCsv text are good, before RedisDb is written over with brotli version."

    public class UpdateNavsParam
    {
        public Db? Db { get; set; } = null;
    }


    public class BrAccJsonHelper
    {
        public string? BrAcc;
        public string? Timestamp;
        public List<Dictionary<string, string>>? AccSums;
        public List<Dictionary<string, string>>? AccPoss;
    }

    public class UpdateNavsService
    {
        public static Timer? g_updateTimer = null;

        public static void Timer_Elapsed(object? p_state)    // Timer is coming on a ThreadPool thread
        {
            if (p_state == null)
                throw new Exception("Timer_Elapsed() received null object.");
            try
            {
                Update((UpdateNavsParam)p_state);
            }
            catch (System.Exception e)  // Exceptions in timers crash the app.
            {
                Utils.Logger.Error(e, "UpdateNavsService.Timer_Elapsed() exception.");
            }
            SetTimer((UpdateNavsParam)p_state);
        }

        public static void SetTimer(UpdateNavsParam p_state)
        {
            DateTime etNow = Utils.ConvertTimeFromUtcToEt(DateTime.UtcNow);
            DateTime targetTimeEt = new DateTime(etNow.Year, etNow.Month, etNow.Day, 16, 1, 0); // 1 minute after market close to avoid too busy periods when VirtualBroker trades happen or when IB is busy
            TimeSpan tsTillTarget = targetTimeEt - etNow;
            if (tsTillTarget < TimeSpan.FromSeconds(10))   // if negative timespan or too close to targetTime, it means etNow is after target time. Then target next day.
            {
                targetTimeEt = targetTimeEt.AddDays(1);
                targetTimeEt = new DateTime(targetTimeEt.Year, targetTimeEt.Month, targetTimeEt.Day, 16, 1, 0);
                tsTillTarget = targetTimeEt - etNow;
            }
            Utils.Logger.Info($"UpdateNavsService next targetdate: {targetTimeEt.ToSqDateTimeStr()} ET");

            if (g_updateTimer == null)
                // g_updateTimer = new System.Threading.Timer(new TimerCallback(Timer_Elapsed), p_state, TimeSpan.FromMilliseconds(1*1000), TimeSpan.FromMilliseconds(-1.0));    // first time: start almost immediately
                g_updateTimer = new System.Threading.Timer(new TimerCallback(Timer_Elapsed), p_state, tsTillTarget, TimeSpan.FromMilliseconds(-1.0));    // first time: start almost immediately
            else
                g_updateTimer.Change(tsTillTarget, TimeSpan.FromMilliseconds(-1.0));     // runs only once per day
        }

        public static void Update(UpdateNavsParam p_updateParam)
        {
            Utils.Logger.Info($"UpdateNavsService.Update()");
            DateTime etNow = Utils.ConvertTimeFromUtcToEt(DateTime.UtcNow);
            if (etNow.IsWeekend())
                return;

            Dictionary<GatewayId, uint> GatewayId2SubTableId = new Dictionary<GatewayId, uint>() {
                    {GatewayId.GyantalMain, 1}, {GatewayId.CharmatMain, 2}, {GatewayId.DeBlanzacMain, 3}};

            foreach (var gw2SubTableId in GatewayId2SubTableId)
            {
                GatewayId gatewayId = gw2SubTableId.Key;
                List<AccSum>? accSums = BrokersWatcher.gWatcher.GetAccountSums(gatewayId);
                if (accSums == null)
                    continue;

                string navStr = accSums.First(r => r.Tag == "NetLiquidation").Value;
                if (!Double.TryParse(navStr, out double nav))
                    nav = Double.NegativeInfinity;
                UpdateAssetInDb(p_updateParam, new AssetId32Bits(AssetType.BrokerNAV, gw2SubTableId.Value), nav);
            }
        }


        private static void UpdateAssetInDb(UpdateNavsParam p_updateParam, AssetId32Bits p_assetId, double p_todayNav)
        {
            var dailyNavStr = p_updateParam.Db!.GetAssetQuoteRaw(p_assetId); // "D/C" for Date/Closes: "D/C,20090102/16461,20090105/16827,..."
            
            int iFirstComma = dailyNavStr!.IndexOf(',');
            string formatString = dailyNavStr.Substring(0, iFirstComma);  // "D/C" for Date/Closes
            if (formatString != "D/C")
                return;

            int nearestIntValue = (int)Math.Round(p_todayNav, MidpointRounding.AwayFromZero); // 0.5 is rounded to 1, -0.5 is rounded to -1. Good.

            // var dailyNavStrSplit = dailyNavStr.Substring(iFirstComma + 1, dailyNavStr.Length - (iFirstComma + 1)).Split(',', StringSplitOptions.RemoveEmptyEntries);
            // DateOnly[] dates = dailyNavStrSplit.Select(r => new DateOnly(Int32.Parse(r.Substring(0, 4)), Int32.Parse(r.Substring(4, 2)), Int32.Parse(r.Substring(6, 2)))).ToArray();
            // double[] unadjustedClosesNav = dailyNavStrSplit.Select(r => Double.Parse(r.Substring(9))).ToArray();
            //unadjustedClosesNav[dates.Length - 1] = todayNav;   // update the last item.
            int iLastComma = dailyNavStr.LastIndexOf(',');
            string lastRecord = dailyNavStr.Substring(iLastComma + 1);
            DateTime lastDate = Utils.FastParseYYYYMMDD(lastRecord.Substring(0, 8));

            DateTime todayEt = Utils.ConvertTimeFromUtcToEt(DateTime.UtcNow).Date;
            int lengthToUseFromOld = dailyNavStr.Length;
            if (lastDate == todayEt)  // if updater runs twice a day, last item is the today already. Remove the last item from the old string.
                lengthToUseFromOld = iLastComma;
                
            // dailyNavStr = dailyNavStr.Substring(0, lengthToUseFromOld);
            var useFromOldSg = new StringSegment(dailyNavStr, 0, lengthToUseFromOld);   // StringSegment doesn't duplicate the long string
            string newDailyNavStr = useFromOldSg + $",{todayEt.ToString("yyyyMMdd")}/{nearestIntValue}";    // append last record at end

            p_updateParam.Db!.SetAssetQuoteRaw(p_assetId, newDailyNavStr);
        }
    }
}