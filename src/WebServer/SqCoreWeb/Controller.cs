using System;
using FinTechCommon;

namespace SqCoreWeb;

class Controller
{
    public static Controller g_controller = new();

    internal static void RedisMirrorDb() // Mirror DB-i to DB-j
    {
        Console.Write("SourceDb index [0..15] (or 'q' to quit): ");
        string sourceDbIdxStr = Console.ReadLine() ?? string.Empty;
        if (sourceDbIdxStr.ToLower() == "q")
            return;
        if (!Int32.TryParse(sourceDbIdxStr, out int sourceDbIdx))
        {
            Console.Write($"Unrecognized number: '{sourceDbIdxStr}'");
            return;
        }
        if (sourceDbIdx < 0 || sourceDbIdx > 15)
        {
            Console.Write($"Number '{sourceDbIdx}' should be in range [0..15]");
            return;
        }

        Console.Write("DestinationDb index [0..15] (or 'q' to quit): ");
        string destDbIdxStr = Console.ReadLine() ?? string.Empty;
        if (destDbIdxStr.ToLower() == "q")
            return;
        if (!Int32.TryParse(destDbIdxStr, out int destDbIdx))
        {
            Console.Write($"Unrecognized number: '{destDbIdxStr}'");
            return;
        }
        if (destDbIdx < 0 || destDbIdx > 15)
        {
            Console.Write($"Number '{destDbIdx}' should be in range [0..15]");
            return;
        }

        Console.Write($"This will copy db{sourceDbIdx} to db{destDbIdx}. Are you sure? (Y/N)");
        string confirmFirstStr = Console.ReadLine() ?? string.Empty;
        if (confirmFirstStr.ToLower() != "y")
            return;

        if (destDbIdx == 0) // this is ProductionDB. Ask confirmation again.
        {
            Console.Write($"The db0 is the Production DB. This will overwrrite db{destDbIdx} with db{sourceDbIdx}. Are you absolutely sure? (Y/N)");
            string confirmSecondStr = Console.ReadLine() ?? string.Empty;
            if (confirmSecondStr.ToLower() != "y")
                return;
        }

        Db.DbCopy(sourceDbIdx, destDbIdx);
    }

    // When we find a new Stock that is not in RedisDb, we have to insert it, but try to avoid that we override the production DB-0 with wrong data
    // (for example, if into GoogleSheet you add a Stock ticker, but forgot to add the same company ticker, then loading Asset data from RedisDb will fail)
    // Do the following STEPS:
    // 1. Run the local webserver, in Local Debug environment (!not on the Linux server)
    // 2. 'Mirror DB-0 (Production) to DB-1' (in the Console menu: DbAdmin)  (you can use DB2 or whatever)
    // 3. Stop the local webserver.
    // 4. In Program.cs , change "int redisDbIndex = 0;" to "int redisDbIndex = 1;"
    // 5. Restart the local webserver. Now, the local webserver uses DB-1, while the Linux webserver uses DB-0. Check all error messages if any. Now, you can ruin DB-1, without ruining the Linux server.
    // 6. In Chrome: Go to the 'All Assets' gSheet, and insert a StockAsset and a CompanyAsset in the proper tab pages
    // 7. 'Upsert gSheet Assets to DB-1' (in the Console menu: DbAdmin)
    // 8. Restart the local webserver. Check that there is no error in the console. Check in the browser: in Developer Dashboard, WebServer/ServerDiagnostics. Check that "StockAssets (#?)" are OK and the new ticker is there.
    // 9. If everything works: 'Mirror DB-1 to DB-0 (Production)' (in the Console menu: DbAdmin)
    // 10. In Program.cs , change "int redisDbIndex = 1;" back to "int redisDbIndex = 0;"
    // 11. Restart the local webserver. Now, the local webserver uses DB-0 again.
    public static void UpsertgSheetAssets() // Upsert gSheet Assets
    {
        Console.Write("DestinationDb index [0..15] (or 'q' to quit): ");
        string destDbIdxStr = Console.ReadLine() ?? string.Empty;
        if (destDbIdxStr.ToLower() == "q")
            return;
        if (!Int32.TryParse(destDbIdxStr, out int destDbIdx))
        {
            Console.Write($"Unrecognized number: '{destDbIdxStr}'");
            return;
        }
        if (destDbIdx < 0 || destDbIdx > 15)
        {
            Console.Write($"Number '{destDbIdx}' should be in range [0..15]");
            return;
        }

        Console.Write($"This will Upsert gSheet Asset data to db{destDbIdx}. Are you sure? (Y/N)");
        string confirmFirstStr = Console.ReadLine() ?? string.Empty;
        if (confirmFirstStr.ToLower() != "y")
            return;

        if (destDbIdx == 0) // this is ProductionDB. Ask confirmation again.
        {
            Console.Write($"The db0 is the Production DB. This will overwrrite db{destDbIdx} with gSheet Asset data. Are you absolutely sure? (Y/N)");
            string confirmSecondStr = Console.ReadLine() ?? string.Empty;
            if (confirmSecondStr.ToLower() != "y")
                return;
        }
        Db.UpsertAssets(destDbIdx);
    }
}