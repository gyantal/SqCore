using System;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;
using DbCommon;
using SqCommon;
using StackExchange.Redis;

namespace FinTechCommon
{
    public partial class Db
    {
        public void TestRedisExecutePing()
        {
            string cacheCommand = "PING"; // There are "server" commands, not a "database" (DB-0) commands, but redisDb.Execute() tricks it, and we can send Server commands as well.
            Console.WriteLine("\nCache command  : " + cacheCommand);
            Console.WriteLine("Cache response : " + m_redisDb.Execute(cacheCommand).ToString());
        }

        public void MirrorProdDb(string p_targetDb) // DEV1 or DEV2
        {
            int targetDbInd = Int32.Parse(p_targetDb.Last().ToString());    // DEV1 => 1, DEV2 => 2
            if (m_redisDbInd != 0 || targetDbInd < 1 || targetDbInd > 9)
                throw new ArgumentOutOfRangeException("MirrorProdDb() expects mirroring from DB-0 to DB index [1..9]");
            // KeyMove() from db0 to db1 is possible, but that will delete the key from source  // https://redis.io/commands/move
            // KeyMigrate() copy possible, but that is designed between 2 Servers, so it timeouts and never executes  // https://redis.io/commands/MIGRATE
            // https://github.com/StackExchange/StackExchange.Redis/issues/775
            // Anyhow, for future maintenance, we want to copy everything; and not specifying every key separately. As the DB later grows, C# code should be updated all the time
            // Except if we iterate over all keys. But we should know how to Execute LUA commands on Redis server anyway (for backups)
            // Also, don't do the Dump/Restore way, because that downloads and uploads all data to between RedisClient and RedisServer. "COPY sq_user sq_user DB 1 REPLACE" works fully on the server.

            var redisConnString = (Utils.RunningPlatform() == Platform.Windows) ? Utils.Configuration["ConnectionStrings:RedisDefault"] : Utils.Configuration["ConnectionStrings:RedisLinuxLocalhost"];
            var redisConn = RedisManager.GetConnection(redisConnString);    // get the main connection. Doesn't recreate a new connection.
            EndPoint endPoint = redisConn.GetEndPoints().First();
            var server = redisConn.GetServer(endPoint);
            RedisKey[] keys = server.Keys(0, pattern: "*").ToArray();   // it automatically do KEYS or the more efficient SCAN commands in the background
            foreach (var key in keys)
            {
                // Console.WriteLine($"key: {key.ToString()}");
                // "SELECT 0"   // by default DB-0 is selected. You might need to use "SELECT 1" if copy from DB-1 to DB-0
                // "COPY sq_user sq_user DB 1 REPLACE" works from redis-cli, but ArgumentOutOfRangeException if it is a command bigger than 23 binary bytes.
                RedisResult result = m_redisDb.Execute("COPY", $"{key.ToString()}", $"{key.ToString()}", "DB", targetDbInd.ToString(), "REPLACE");  // https://redis.io/commands/copy

                // Dump to binary, download to client and Restore back
                // byte[] dump = m_redisDb.KeyDump(key);
                // var redisDbTarget = redisConn.GetDatabase(1);
                // redisDbTarget.KeyRestore(key, dump); // if it already exists, exception: 'BUSYKEY Target key name already exists.'
            }
        }

        public void UpsertAssets(string p_targetDb) // DEV1 or DEV2
        {
            // AllAssets gSheet location: https://docs.google.com/spreadsheets/d/1gkZlvD5epmBV8zi-l0BbLaEtwScvhHvHOYFZ4Ah_MA4/edit#gid=898941432
            string gApiKey = Utils.Configuration["Google:GoogleApiKeyKey"];
            if (String.IsNullOrEmpty(Utils.Configuration["Google:GoogleApiKeyKey"]))
                throw new SqException("GoogleApiKeyKey is missing.");

            string range = "BaseAssets";    // gets all data from the tab-page. range = "BaseAssets!A:A" is also possible
            string? baseAssetsJson = Utils.DownloadStringWithRetryAsync($"https://sheets.googleapis.com/v4/spreadsheets/1gkZlvD5epmBV8zi-l0BbLaEtwScvhHvHOYFZ4Ah_MA4/values/{range}?key={gApiKey}").TurnAsyncToSyncTask();
            range = "StockAssets";
            string? stockAssetsJson = Utils.DownloadStringWithRetryAsync($"https://sheets.googleapis.com/v4/spreadsheets/1gkZlvD5epmBV8zi-l0BbLaEtwScvhHvHOYFZ4Ah_MA4/values/{range}?key={gApiKey}").TurnAsyncToSyncTask();
            range = "CompanyAssets";
            string? companyAssetsJson = Utils.DownloadStringWithRetryAsync($"https://sheets.googleapis.com/v4/spreadsheets/1gkZlvD5epmBV8zi-l0BbLaEtwScvhHvHOYFZ4Ah_MA4/values/{range}?key={gApiKey}").TurnAsyncToSyncTask();
            if (baseAssetsJson == null || stockAssetsJson == null || companyAssetsJson == null)
                throw new SqException("DownloadStringWithRetryAsync() failed.");

            StringBuilder sbCash = new StringBuilder("\"C\":[\n");
            bool isFirstCash = true;
            StringBuilder sbCpair = new StringBuilder("\"D\":[\n");
            bool isFirstCpair = true;
            StringBuilder sbReEst = new StringBuilder("\"R\":[\n");
            bool isFirstReEst = true;
            StringBuilder sbNav = new StringBuilder("\"N\":[\n");
            bool isFirstNav = true;
            StringBuilder sbPortf = new StringBuilder("\"P\":[\n");
            bool isFirstPortf = true;

            StringBuilder sbComp = new StringBuilder("\"A\":[\n"); // companies should come first, because stocks refer to companies
            bool isFirstComp = true;
            StringBuilder sbStock = new StringBuilder("\"S\":[\n");
            bool isFirstStock = true;


            // https://marcroussy.com/2020/08/17/deserialization-with-system-text-json/     // POCO: Plain Old Class Object
            using (JsonDocument baseDoc = JsonDocument.Parse(baseAssetsJson))
            {
                JsonElement baseValues = baseDoc.RootElement.GetProperty("values");
                bool wasHeaderParsed = false;
                foreach (JsonElement row in baseValues.EnumerateArray())
                {
                    if (!wasHeaderParsed)
                        wasHeaderParsed = true;
                    else
                    {
                        JsonElement[] rowArr = row.EnumerateArray().ToArray();
                        if (rowArr.Length == 0)
                            continue;   // skip empty gSheet rows in JSON: "[],"

                        if (rowArr[0].ToString() == "C")    // CurrencyCash
                        {
                            if (isFirstCash)
                                isFirstCash = false;
                            else
                                sbCash.Append(",");
                            sbCash.Append($"[{rowArr[1]},\"{rowArr[2]}\",\"{rowArr[3]}\",\"{rowArr[4]}\",\"{rowArr[5]}\"]");
                        }
                        if (rowArr[0].ToString() == "D")    // CurrencyPair
                        {
                            if (isFirstCpair)
                                isFirstCpair = false;
                            else
                                sbCpair.Append(",");
                            sbCpair.Append($"[{rowArr[1]},\"{rowArr[2]}\",\"{rowArr[3]}\",\"{rowArr[4]}\",\"{rowArr[5]}\",\"{rowArr[7]}\"]");
                        }
                        if (rowArr[0].ToString() == "R")    // RealEstate
                        {
                            if (isFirstReEst)
                                isFirstReEst = false;
                            else
                                sbReEst.Append(",");
                            sbReEst.Append($"[{rowArr[1]},\"{rowArr[2]}\",\"{rowArr[3]}\",\"{rowArr[4]}\",\"{rowArr[5]}\",\"{rowArr[8]}\"]");
                        }
                        if (rowArr[0].ToString() == "N")    // BrokerNav
                        {
                            if (isFirstNav)
                                isFirstNav = false;
                            else
                                sbNav.Append(",");
                            sbNav.Append($"[{rowArr[1]},\"{rowArr[2]}\",\"{rowArr[3]}\",\"{rowArr[4]}\",\"{rowArr[5]}\",\"{rowArr[8]}\"]");
                        }
                        if (rowArr[0].ToString() == "P")    // Portfolio
                        {
                            if (isFirstPortf)
                                isFirstPortf = false;
                            else
                                sbPortf.Append(",");
                            sbPortf.Append($"[{rowArr[1]},\"{rowArr[2]}\",\"{rowArr[3]}\",\"{rowArr[4]}\",\"{rowArr[5]}\",\"{rowArr[8]}\"]");
                        }
                    }
                }

                using (JsonDocument companyDoc = JsonDocument.Parse(companyAssetsJson))
                {
                    wasHeaderParsed = false;
                    foreach (JsonElement row in companyDoc.RootElement.GetProperty("values").EnumerateArray())
                    {
                        if (!wasHeaderParsed)
                            wasHeaderParsed = true;
                        else
                        {
                            JsonElement[] rowArr = row.EnumerateArray().ToArray();
                            if (rowArr.Length == 0)
                                continue;   // skip empty gSheet rows in JSON: "[],"

                            if (rowArr[0].ToString() == "A")    // Stock
                            {
                                if (isFirstComp)
                                    isFirstComp = false;
                                else
                                    sbComp.Append(",");
                                sbComp.Append($"[{rowArr[1]},\"{rowArr[2]}\",\"{rowArr[3]}\",\"{Get(rowArr, 4)}\",\"{Get(rowArr, 5)}\",\"{Get(rowArr, 7)}\",\"{Get(rowArr, 8)}\",\"{Get(rowArr, 9)}\",\"{Get(rowArr, 10)}\",\"{Get(rowArr, 11)}\"]");
                            }
                        }
                    }
                }

                using (JsonDocument stockDoc = JsonDocument.Parse(stockAssetsJson))
                {
                    wasHeaderParsed = false;
                    foreach (JsonElement row in stockDoc.RootElement.GetProperty("values").EnumerateArray())
                    {
                        if (!wasHeaderParsed)
                            wasHeaderParsed = true;
                        else
                        {
                            JsonElement[] rowArr = row.EnumerateArray().ToArray();
                            if (rowArr.Length == 0)
                                continue;   // skip empty gSheet rows in JSON: "[],"

                            if (rowArr[0].ToString() == "S")    // Stock
                            {
                                if (isFirstStock)
                                    isFirstStock = false;
                                else
                                    sbStock.Append(",");
                                sbStock.Append($"[{rowArr[1]},\"{rowArr[2]}\",\"{rowArr[3]}\",\"{Get(rowArr, 4)}\",\"{Get(rowArr, 5)}\",\"{Get(rowArr, 7)}\",\"{Get(rowArr, 8)}\",\"{Get(rowArr, 9)}\",\"{Get(rowArr, 10)}\",\"{Get(rowArr, 11)}\",\"{Get(rowArr, 12)}\",\"{Get(rowArr, 13)}\",\"{Get(rowArr, 14)}\",\"{Get(rowArr, 15)}\",\"{Get(rowArr, 16)}\"]");
                            }
                        }
                    }
                }

                sbCash.Append("],\n");
                sbCpair.Append("],\n");
                sbReEst.Append("],\n");
                sbNav.Append("],\n");
                sbPortf.Append("],\n");
                sbComp.Append("],\n");
                sbStock.Append("]");
                StringBuilder sb = new StringBuilder("{");
                sb.Append(sbCash).Append(sbCpair).Append(sbReEst).Append(sbNav).Append(sbPortf).Append(sbComp).Append(sbStock).Append("}");
                m_redisDb.HashSet("memDb", "Assets", new RedisValue(sb.ToString()));

                // At the moment, the NAV's and StockAsset table's "Srv.LoadPrHist(Span)" column is not mirrored from gSheet to RedisDb automatically.
                // We add these manually to RedisDb now. Not bad, because adding them manually forces us to think about whether we really need that extra stock consuming RAM and YF downloads.
            }

        }

        string Get(JsonElement[] p_arr, int p_i)
        {
            if (p_i < p_arr.Length)
                return p_arr[p_i].ToString() ?? string.Empty;
            else
                return string.Empty;
        }
    }
}