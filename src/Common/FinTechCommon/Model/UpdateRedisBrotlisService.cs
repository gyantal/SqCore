using System;
using System.Threading;
using SqCommon;
using StackExchange.Redis;


namespace FinTechCommon
{
    // This service can be outsourced to a 3rd App. 
    // It can update data that is not even related to SqCoreWeb server: such as Option price crawler puts huge data into SqlDb, 
    // and an OptionBacktester.exe consumer wants to use only fraction of that coming from RedisDb as a Brotli-d data.
    // In case of this Webserver, the data-source is also in RedisDb now, but in the future, its data-source might move into a file-based PostgreSql
    // Because of that, if Change is detected, this code will not update MemDb. The regular MemDb Asset checker will periodically do that.
    public class UpdateBrotliParam
    {
        public Db? Db { get; set; } = null;
    }

    public class UpdateRedisBrotlisService
    {
        public static Timer? g_updateTimer = null;

        public static void Timer_Elapsed(object? p_state)    // Timer is coming on a ThreadPool thread
        {
            if (p_state == null)
                throw new Exception("Timer_Elapsed() received null object.");
            try
            {
                Update((UpdateBrotliParam)p_state);
            }
            catch (System.Exception e)  // Exceptions in timers crash the app.
            {
                Utils.Logger.Error(e, "UpdateRedisBrotlisService.Timer_Elapsed() exception.");
            }
            SetTimer((UpdateBrotliParam)p_state);
        }

        public static void SetTimer(UpdateBrotliParam p_state)
        {
            if (g_updateTimer == null)
                g_updateTimer = new System.Threading.Timer(new TimerCallback(Timer_Elapsed), p_state, TimeSpan.FromMilliseconds(10*1000), TimeSpan.FromMilliseconds(-1.0));    // first time: start almost immediately
            else
            {
                DateTime etNow = Utils.ConvertTimeFromUtcToEt(DateTime.UtcNow);
                DateTime targetTimeEt = etNow.AddHours(3);  // Polling for change in every 3 hours
                Utils.Logger.Info($"UpdateRedisBrotlisService next targetdate: {targetTimeEt.ToSqDateTimeStr()} ET");
                g_updateTimer.Change(targetTimeEt - etNow, TimeSpan.FromMilliseconds(-1.0));     // runs only once
            }
        }

        public static void Update(UpdateBrotliParam p_state)
        {
            // 1. Check if binary BrotliRecords in RedisDb is Consistent With the text Json source (Json in either in RedisDb, but more likely in PostgreSql)
            // start using Redis:'allAssets.brotli' (520bytes instead of 1.52KB) immediately. User only modifies the JSON version Redis:'allAssets'.
            // 15 seconds later check the Redis consistency. In a very rare case when that finds discrepancy between 'allAssets.brotli' vs. 'allAssets' then 
            // it updates Redis:'allAssets.brotli' and re-call HistoricalDataReloadAndSetTimer()

            Utils.Logger.Info($"UpdateRedisBrotlisService.Update()");
            bool wasAnyBrotliUpdated = p_state.Db!.UpdateBrotlisIfNeeded();
            if (wasAnyBrotliUpdated)
                Utils.Logger.Info($"Some Brotlis were updated in RedisDb.");

            // if any brotli was updated, do NOT invoke Reload. It is not the task of this service.
            // ReloadAssetsDataIfChangedAndSetTimer();
        }

    }
}