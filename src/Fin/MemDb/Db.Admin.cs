using System;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;
using DbCommon;
using SqCommon;
using StackExchange.Redis;

namespace Fin.MemDb;

public partial class Db
{
    public string TestRedisExecutePing()
    {
        string command = "PING"; // There are "server" commands, not a "database" (DB-0) commands, but redisDb.Execute() tricks it, and we can send Server commands as well.
        return "RedisDb response: " + m_redisDb.Execute(command).ToString();
    }

    public static void DbCopy(int sourceDbIdx, int destDbIdx) // copy DB-copyFromIdx to DB-copyToIdx
    {
        // KeyMove() from db0 to db1 is possible, but that will delete the key from source  // https://redis.io/commands/move
        // KeyMigrate() copy possible, but that is designed between 2 Servers, so it timeouts and never executes  // https://redis.io/commands/MIGRATE
        // https://github.com/StackExchange/StackExchange.Redis/issues/775
        // Anyhow, for future maintenance, we want to copy everything; and not specifying every key separately. As the DB later grows, C# code should be updated all the time
        // Except if we iterate over all keys. But we should know how to Execute LUA commands on Redis server anyway (for backups)
        // Also, don't do the Dump/Restore way, because that downloads and uploads all data to between RedisClient and RedisServer. "COPY sq_user sq_user DB 1 REPLACE" works fully on the server.

        if (sourceDbIdx < 0 || sourceDbIdx > 15 || destDbIdx < 0 || destDbIdx > 15 || sourceDbIdx == destDbIdx)
            throw new ArgumentOutOfRangeException(nameof(sourceDbIdx), "MirrorDb() expects idx from [0..15], and they should be different.");

        // 1. Create a new connection. Don't use the MemDb main connection, because we might want to switch to a non-default DB, like DB-1. It is safer this way. Don't tinker with the MemDb main connection
        string? redisConnString = (OperatingSystem.IsWindows() ? Utils.Configuration["ConnectionStrings:RedisDefault"] : Utils.Configuration["ConnectionStrings:RedisLinuxLocalhost"])
            ?? throw new SqException("Redis ConnectionStrings is missing from Config");
        ConnectionMultiplexer newConn = ConnectionMultiplexer.Connect(redisConnString);
        EndPoint endPoint = newConn.GetEndPoints().First();
        var server = newConn.GetServer(endPoint);
        var sourceDb = newConn.GetDatabase(sourceDbIdx);
        var destDb = newConn.GetDatabase(destDbIdx);
        // There are "server" commands, not a "database" (DB-0) commands, but redisDb.Execute() tricks it, and we can send Server commands as well.
        // redisDb.Execute("select","1"); cannot be used to select DB-1. But that is OK. See https://github.com/StackExchange/StackExchange.Redis/issues/774
        // "The database is specified when using GetDatabase, and is bound to that database instance. This is very deliberate for multi-threaded scenarios, so that two concurrent callers don't trip over each-other.

        // 2. Delete all keys from target DB
        Console.WriteLine($"Deleting all keys in db{destDbIdx}...by FLUSHDB");
        RedisResult result = destDb.Execute("FLUSHDB");  // clears currently active database
        if (result.IsNull)
            Console.WriteLine($"Error in executing FLUSHDB");

        // 3. Iterate keys over source DB
        RedisKey[] keys = server.Keys(sourceDbIdx, pattern: "*").ToArray();   // it automatically do KEYS or the more efficient SCAN commands in the background
        foreach (RedisKey key in keys)
        {
            Console.WriteLine($"Copying from db{sourceDbIdx} to db{destDbIdx}: key '{key}'");
            // "COPY sq_user sq_user DB 1 REPLACE" works from redis-cli, but ArgumentOutOfRangeException if it is a command bigger than 23 binary bytes.
            result = sourceDb.Execute("COPY", $"{key}", $"{key}", "DB", $"{destDbIdx}", "REPLACE");  // https://redis.io/commands/copy   You need to use "SELECT 1" if copy from DB-1 to DB-0

            // Option 2: Dump to binary, download to client and Restore back
            // byte[] dump = m_redisDb.KeyDump(key);
            // var redisDbTarget = redisConn.GetDatabase(1);
            // redisDbTarget.KeyRestore(key, dump); // if it already exists, exception: 'BUSYKEY Target key name already exists.'
        }
    }

    // "Redis Desktop Manager 0.9.3.817" is error prone when copying binary. Or when coping big text to Clipboard. Try to not use it.
    public static void UpsertAssets(int destDbIdx) // DB0 or DB1.  Developer is supposed to change the MemDb.ActiveDb to DB1, restart SqCore webserver. Change DB on this secondary DB1 first, and test if it works.
    {
        // AllAssets gSheet location: https://docs.google.com/spreadsheets/d/1gkZlvD5epmBV8zi-l0BbLaEtwScvhHvHOYFZ4Ah_MA4/edit#gid=898941432
        string? gApiKey = Utils.Configuration["Google:GoogleApiKeyKey"];
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

        StringBuilder sbCash = new("\"C\":[\n");
        bool isFirstCash = true;
        StringBuilder sbCpair = new("\"D\":[\n");
        bool isFirstCpair = true;
        StringBuilder sbIndex = new("\"I\":[\n");
        bool isFirstIndex = true;
        StringBuilder sbReEst = new("\"R\":[\n");
        bool isFirstReEst = true;
        StringBuilder sbNav = new("\"N\":[\n");
        bool isFirstNav = true;
        StringBuilder sbPortf = new("\"P\":[\n");
        bool isFirstPortf = true;

        StringBuilder sbComp = new("\"A\":[\n"); // companies should come first, because stocks refer to companies
        bool isFirstComp = true;
        StringBuilder sbStock = new("\"S\":[\n");
        bool isFirstStock = true;

        // https://marcroussy.com/2020/08/17/deserialization-with-system-text-json/     // POCO: Plain Old Class Object
        using JsonDocument baseDoc = JsonDocument.Parse(baseAssetsJson);
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

                if (rowArr[0].ToString() == "C") // CurrencyCash
                {
                    if (isFirstCash)
                        isFirstCash = false;
                    else
                        sbCash.Append(',');
                    sbCash.Append($"[{rowArr[1]},\"{rowArr[2]}\",\"{rowArr[3]}\",\"{rowArr[4]}\",\"{rowArr[5]}\"]");
                }
                if (rowArr[0].ToString() == "D") // CurrencyPair
                {
                    if (isFirstCpair)
                        isFirstCpair = false;
                    else
                        sbCpair.Append(',');
                    sbCpair.Append($"[{rowArr[1]},\"{rowArr[2]}\",\"{rowArr[3]}\",\"{rowArr[4]}\",\"{rowArr[5]}\",\"{rowArr[7]}\"]");
                }
                if (rowArr[0].ToString()[0] == AssetHelper.gAssetTypeCode[AssetType.FinIndex]) // Index, such as ^VIX
                {
                    if (isFirstIndex)
                        isFirstIndex = false;
                    else
                        sbIndex.Append(',');
                    sbIndex.Append($"[{rowArr[1]},\"{rowArr[2]}\",\"{rowArr[3]}\",\"{rowArr[4]}\",\"{rowArr[5]}\"]");
                }
                if (rowArr[0].ToString() == "R") // RealEstate
                {
                    if (isFirstReEst)
                        isFirstReEst = false;
                    else
                        sbReEst.Append(',');
                    sbReEst.Append($"[{rowArr[1]},\"{rowArr[2]}\",\"{rowArr[3]}\",\"{rowArr[4]}\",\"{rowArr[5]}\",\"{rowArr[8]}\"]");
                }
                if (rowArr[0].ToString() == "N") // BrokerNav
                {
                    if (isFirstNav)
                        isFirstNav = false;
                    else
                        sbNav.Append(',');
                    sbNav.Append($"[{rowArr[1]},\"{rowArr[2]}\",\"{rowArr[3]}\",\"{rowArr[4]}\",\"{rowArr[5]}\",\"{rowArr[8]}\"]");
                }
                if (rowArr[0].ToString() == "P") // Portfolio
                {
                    if (isFirstPortf)
                        isFirstPortf = false;
                    else
                        sbPortf.Append(',');
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

                    if (rowArr[0].ToString() == "A") // Company
                    {
                        if (isFirstComp)
                            isFirstComp = false;
                        else
                            sbComp.Append(',');
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

                    if (rowArr[0].ToString() == "S") // Stock
                    {
                        if (isFirstStock)
                            isFirstStock = false;
                        else
                            sbStock.Append(',');
                        sbStock.Append($"[{rowArr[1]},\"{rowArr[2]}\",\"{rowArr[3]}\",\"{Get(rowArr, 4)}\",\"{Get(rowArr, 5)}\",\"{Get(rowArr, 7)}\",\"{Get(rowArr, 8)}\",\"{Get(rowArr, 9)}\",\"{Get(rowArr, 10)}\",\"{Get(rowArr, 11)}\",\"{Get(rowArr, 12)}\",\"{Get(rowArr, 13)}\",\"{Get(rowArr, 14)}\",\"{Get(rowArr, 15)}\",\"{Get(rowArr, 16)}\"]");
                    }
                }
            }
        }

        sbCash.Append("],\n");
        sbCpair.Append("],\n");
        sbIndex.Append("],\n");
        sbReEst.Append("],\n");
        sbNav.Append("],\n");
        sbPortf.Append("],\n");
        sbComp.Append("],\n");
        sbStock.Append(']');
        StringBuilder sb = new("{");
        sb.Append(sbCash).Append(sbCpair).Append(sbIndex).Append(sbReEst).Append(sbNav).Append(sbPortf).Append(sbComp).Append(sbStock).Append('}');

        // Create a new connection. Don't use the MemDb main connection, because we might want to switch to a non-default DB, like DB-1. It is safer this way. Don't tinker with the MemDb main connection
        var redisConnString = (OperatingSystem.IsWindows() ? Utils.Configuration["ConnectionStrings:RedisDefault"] : Utils.Configuration["ConnectionStrings:RedisLinuxLocalhost"])
            ?? throw new SqException("Redis ConnectionStrings is missing from Config");
        ConnectionMultiplexer newConn = ConnectionMultiplexer.Connect(redisConnString);
        var destDb = newConn.GetDatabase(destDbIdx);

        destDb.HashSet("memDb", "Assets", new RedisValue(sb.ToString()));
        Console.WriteLine($"Hash 'memDb.Assets' was created in db{destDbIdx}.");

        // At the moment, the NAV's and StockAsset table's "Srv.LoadPrHist(Span)" column is not mirrored from gSheet to RedisDb automatically.
        // We add these manually to RedisDb now. Not bad, because adding them manually forces us to think about whether we really need that extra stock consuming RAM and YF downloads.
    }

    static string Get(JsonElement[] p_arr, int p_i)
    {
        if (p_i < p_arr.Length)
            return p_arr[p_i].ToString() ?? string.Empty;
        else
            return string.Empty;
    }
}