using System;
using System.Diagnostics;
using System.Net.NetworkInformation;
using Microsoft.Extensions.Configuration;
using Npgsql;
using StackExchange.Redis;
using SqCommon;
using DbCommon;
using System.IO;
using System.Collections.Generic;
using System.Linq;

namespace RedisManager
{
    class Controller
    {
        static public Controller g_controller = new Controller();

        internal void Start()
        {
            // gMainThreadExitsResetEvent = new ManualResetEventSlim(false);
            // ScheduleDailyTimers();
        }

        internal void Exit()
        {
            //gMainThreadExitsResetEvent.Set();
        }


        public void TestPing()
        {
            string address = Program.gConfiguration.GetConnectionString("PingDefault");
            int nTries = Utils.InvariantConvert<int>(Program.gConfiguration["AppSettings:TestPingNTries"]);
            long sumPingTimes = 0;
            for (int i = 0; i < nTries; i++)
            {
                try
                {
                    Ping myPing = new Ping();
                    PingReply reply = myPing.Send(address, 1000);
                    if (reply != null)
                    {
                        sumPingTimes += reply.RoundtripTime;
                        Console.WriteLine($"Status :  {reply.Status}, Time : {reply.RoundtripTime}ms, Address :'{reply.Address}'");
                    }
                }
                catch
                {
                    Console.WriteLine("ERROR: You have Some TIMEOUT issue");
                }
            }

            Console.WriteLine($"Average Ping time: {sumPingTimes / (double)nTries :0.00}ms");       // Ping takes 24 ms

        }

        //https://www.npgsql.org/doc/index.html
        public void TestPostgreSql()
        {
            var pSqlConnString = Program.gConfiguration.GetConnectionString("PostgreSqlDefault");
            using var conn = new NpgsqlConnection(pSqlConnString);

            Stopwatch watch0 = Stopwatch.StartNew();
            conn.Open();
            watch0.Stop();
            Console.WriteLine($"Connection takes {watch0.Elapsed.TotalMilliseconds:0.00}ms");   // first connection: 360-392ms, later: 0, so connections are cached

            // Insert some data
            Stopwatch watch1 = Stopwatch.StartNew();
            using (var cmd = new NpgsqlCommand())
            {
                cmd.Connection = conn;
                cmd.CommandText = "INSERT INTO testtable (column1) VALUES (@p)";
                cmd.Parameters.AddWithValue("p", "Hello SqCore.Tools.RedisManager");
                cmd.ExecuteNonQuery();
            }
            watch1.Stop();
            Console.WriteLine($"INSERT takes {watch1.Elapsed.TotalMilliseconds:0.00}ms");    // "INSERT takes 27,33,30,37,29,30 ms". If I do it from pgAdmin, it says: 50msec

            // Retrieve all rows
            Stopwatch watch2 = Stopwatch.StartNew();
            using (var cmd = new NpgsqlCommand("SELECT column1 FROM testtable", conn))
            using (var reader = cmd.ExecuteReader())
                while (reader.Read())
                    Console.WriteLine(reader.GetString(0));
            watch2.Stop();
            Console.WriteLine($"SELECT takes {watch2.Elapsed.TotalMilliseconds:0.00}ms");    // "SELECT takes 22,29,32,37,43,47,45 ms", If I do it from pgAdmin, it says: 64msec


            // Delete inserted data
            Stopwatch watch3 = Stopwatch.StartNew();
            using (var cmd = new NpgsqlCommand())
            {
                cmd.Connection = conn;
                cmd.CommandText = "DELETE FROM testtable WHERE column1=@p;";
                cmd.Parameters.AddWithValue("p", "Hello SqCore.Tools.RedisManager");
                cmd.ExecuteNonQuery();
            }
            watch3.Stop();
            Console.WriteLine($"DELETE takes {watch3.Elapsed.TotalMilliseconds:0.00}ms");    // "INSERT takes 27,33,30,37,29,30 ms". If I do it from pgAdmin, it says: 50msec
                                                                                             // pgAdmin running on local webserver reports worse numbers. maybe because pgAdmin first access local webserver + implemented badly. And that overhead is also calculated.
        }


        public void TestRedisCache()
        {
            var redisConnString = Program.gConfiguration.GetConnectionString("RedisDefault");   // read from file
     
            Stopwatch watch0 = Stopwatch.StartNew();
            IDatabase db = DbCommon.RedisManager.GetDb(redisConnString, 0);
            watch0.Stop();
            Console.WriteLine($"Connection (GetDb()) takes {watch0.Elapsed.TotalMilliseconds :0.00}ms");     // first connection: 292(first)/72/70/64/83ms, so connections are not cached, but we can cache the connection manually

            // Insert some data
            Stopwatch watch1 = Stopwatch.StartNew();
            string value = "SqCore.Tools.RedisManager";
            db.StringSet("SqCoreRedisManagerKey", value);
            watch1.Stop();
            Console.WriteLine($"INSERT takes {watch1.Elapsed.TotalMilliseconds :0.00}ms");    // "INSERT takes 31(first)/21/20/19/22/24 ms". 

            // Retrieve
            Stopwatch watch2 = Stopwatch.StartNew();
            string value2 = db.StringGet("SqCoreRedisManagerKey");
            watch2.Stop();
            Console.WriteLine(value2);
            Console.WriteLine($"SELECT takes {watch2.Elapsed.TotalMilliseconds :0.00}ms");    // "SELECT takes 30(first)/20/23/20/20/24 ms"

            // Delete
            Stopwatch watch3 = Stopwatch.StartNew();
            bool wasRemoved = db.KeyDelete("SqCoreRedisManagerKey");
            watch3.Stop();
            Console.WriteLine("Key was removed: " + wasRemoved);
            Console.WriteLine($"DELETE takes {watch3.Elapsed.TotalMilliseconds :0.00}ms");     // "SELECT takes 30(first)/20/23/20/20/24 ms"

            // pSql Insert (30ms)/select (45ms) is longer than Redis  Insert (19ms)/select (21ms). So, Redis cost basically 0ms CPU time, all is latency, while pSql is not.
        }


        // 1. How to convert Table data to JSON data
        // https://stackoverflow.com/questions/24006291/postgresql-return-result-set-as-json-array/24006432     // we used this, "PostgreSQL return result set as JSON array?"
        // https://stackoverflow.com/questions/5083709/convert-from-sqldatareader-to-json                       // it is more general for all cases.
        //
        // 2. How to do Redis insertions very fast?
        // Mass-insert: with pipelines.
        // https://redis.io/topics/mass-insert
	    // https://redislabs.com/ebook/part-2-core-concepts/chapter-4-keeping-data-safe-and-ensuring-performance/4-5-non-transactional-pipelines/ there is no transactional pipeline, only non-transactional pipeline. So, just do pipeline.
	    // https://stackoverflow.com/questions/32149626/how-to-insert-billion-of-data-to-redis-efficiently
        // But note that reading SQL will be the bottleneck, not the insertion to the fast Redis. So, it is not very important to work on it now.
        public void ConvertTableDataToRedis(string[] p_tables)
        {
            Console.WriteLine($"Converting tables...{string.Join(",", p_tables)}");
            var pSqlConnString = Program.gConfiguration.GetConnectionString("PostgreSqlDefault");
            using var conn = new NpgsqlConnection(pSqlConnString);
            conn.Open();

            var redisConnString = Program.gConfiguration.GetConnectionString("RedisDefault");   // read from file
            IDatabase redisDb = DbCommon.RedisManager.GetDb(redisConnString, 0);

            foreach (var tableName in p_tables)
            {
                Console.WriteLine($"Converting table {tableName}...");

                using (var cmd = new NpgsqlCommand($"SELECT to_jsonb(array_agg({tableName})) FROM {tableName};", conn))     // this gives back the whole table in one JSON string.
                using (var reader = cmd.ExecuteReader())
                    while (reader.Read()) {
                        var tableInJson = reader.GetString(0);
                        Console.WriteLine(tableInJson);
                        redisDb.StringSet($"{tableName}", tableInJson);

                        break;  // there should be only one result per table.
                    }

                
            }
        }


        class DailyNavData {
            public string DateStr = string.Empty;
            public string ValueStr = string.Empty;
        }

        struct DailyNavDataBin {
            public DateOnly dateOnly;
            public float floatValue;
        }

        // How to create the CVS containing NAV data + deposit?
        // IbMain: used 2nd user, IbDeBlan: used 1st user.
        // IB: PortfolioAnalyst/Reports/CreateCustomReport (SinceInception, Daily, Detailed + AccountOverview/Allocation by Financial Instrument/Deposits). Create in PDF + CSV.
        // 2021-09-09: both IbMain, IbDbl worked without timeout. If it timeouts, run a Custom date for the last 2-5 years. It can be merged together manually as a last resort.
        // >DC-IB-MAIN, it seems: 2011-02-02 is the inception date. 2011-02-02, 2011-03-01: didn't work. Timeout. But 2014-12-31 worked. Try at another time.
        public void InsertNavAssetFromCsvFile(string p_redisKeyPrefix, string p_csvFullpath)
        {
            List<DailyNavData> dailyNavData = new List<DailyNavData>();
            List<DailyNavData> dailyDepositData = new List<DailyNavData>();

            using (StreamReader sr = new StreamReader(p_csvFullpath))
            {
                int iNavColumn = -1;
                int iRow = 0;
                string? currentLine;
                while ((currentLine = sr.ReadLine()) != null)  // currentLine will be null when the StreamReader reaches the end of file
                {
                    iRow++;
                    if (iRow == 1 && currentLine != @"Introduction,Header,Name,Account,Alias,BaseCurrency,AccountType,AnalysisPeriod,PerformanceMeasure")
                        throw new Exception();

                    // "Allocation by Financial Instrument,Header,Date,ETFs,Options,Stocks,Cash,NAV" (Agy) or "Allocation by Financial Instrument,Header,Date,ETFs,Futures Options,Options,Stocks,Warrants,Cash,NAV" (DC)
                    if (iRow == 7)
                    {
                        if (!currentLine.StartsWith(@"Allocation by Financial Instrument,Header,Date,ETFs")) // just search prefix of the string. Don't include 'Futures, Warrants, etc', because DC's file will  fail
                            throw new Exception();
                        
                        var navHeaderParts = currentLine.Split(',', StringSplitOptions.RemoveEmptyEntries);
                        iNavColumn = Array.FindIndex(navHeaderParts, r => r == "NAV");
                    }

                    
                    if (currentLine.StartsWith(@"Allocation by Financial Instrument,Data,"))
                    {
                        var currentLineParts = currentLine.Split(',', StringSplitOptions.RemoveEmptyEntries);   // date is in this format: "20090102"  YYYYMMDD
                        dailyNavData.Add(new DailyNavData() {DateStr = currentLineParts[2], ValueStr = currentLineParts[iNavColumn]});
                    }
                    if (currentLine.StartsWith(@"Deposits And Withdrawals,Data,"))
                    {
                        var currentLineParts = currentLine.Split(',', StringSplitOptions.RemoveEmptyEntries);   // date is in this format: "03/10/09" MM/DD/YY
                        if (currentLineParts[3] == "Deposit" || currentLineParts[3] == "Incoming Account Transfer" || currentLineParts[3] == "Withdrawal" || currentLineParts[3] == "Outgoing Account Transfer")
                        {
                            var monthStr = currentLineParts[2].Substring(0, 2);
                            var dayStr = currentLineParts[2].Substring(3, 2);
                            var yearStr = currentLineParts[2].Substring(6, 2); // 09 means 2009, 99 means 1999
                            var year = Int32.Parse(yearStr);
                            if (year > 50)
                                yearStr = (year + 1900).ToString();
                            else
                                yearStr = (year + 2000).ToString();
                            dailyDepositData.Add(new DailyNavData() { DateStr = yearStr + monthStr + dayStr, ValueStr = currentLineParts[5] }); // Withdrawals are negative in CSV. Good.
                        }
                    }
                }
            }

            // for 3069 days.
            // 1. Text data: with fractional NAV values: 70K, with integer NAV values: 47.5K (brotlied: 9.578K), with integer NAV + DateStr-1900years: 44.4K  (brotlied: 9.557K, difference is 0.2%, not even 1%. Just forget the -1900). (Wow. 4x compression)
            string outputCsv = "D/C," + String.Join(",", dailyNavData.Select(r => {
                int year = Int32.Parse(r.DateStr.Substring(0, 4)); // - 1900; // subtracting -1900years only helps about 1/1000. It is not worth it.
                // casting Double to Int will just remove the fractionals, but not round it to the nearest integer.
                int nearestIntValue = (int)Math.Round(Double.Parse(r.ValueStr), MidpointRounding.AwayFromZero); // 0.5 is rounded to 1, -0.5 is rounded to -1. Good.
                return year.ToString("D3") + r.DateStr.Substring(4) + "/" + nearestIntValue.ToString();
            }));
            var outputCsvBrotli = Utils.Str2BrotliBin(outputCsv);

            // 2.1 Bin data: 3069*6=18.4K. Brotlied: 15.268K (less compression if date + float are mixed)
            var dailyStructsBin = dailyNavData.Select(r => {
                DateOnly dateOnly = new DateOnly(Int32.Parse(r.DateStr.Substring(0, 4)), Int32.Parse(r.DateStr.Substring(4, 2)), Int32.Parse(r.DateStr.Substring(6, 2)));
                float navValue = (float)Double.Parse(r.ValueStr);
                return new DailyNavDataBin() { dateOnly = dateOnly, floatValue = navValue  };
            }).ToArray();
            var outputBin1 = dailyStructsBin.SelectMany(r => {
                byte[] dateBytes = BitConverter.GetBytes(r.dateOnly.ToBinary());
                byte[] navBytes = BitConverter.GetBytes(r.floatValue);
                IEnumerable<byte> dailyBytes = dateBytes.Concat(navBytes);
                return dailyBytes;
             }).ToArray();
             var outputBin1Brotli = Utils.Bin2BrotliBin(outputBin1);

            // 2.2 Bin data: 3069*6=18.4K. Brotlied: 13.359K (more compression if date array is separate and float array is separate)
            var outputBin2 = new byte[dailyNavData.Count * 6];
            var dateOnlyArr = dailyNavData.Select(r =>
            {
                DateOnly dateOnly = new DateOnly(Int32.Parse(r.DateStr.Substring(0, 4)), Int32.Parse(r.DateStr.Substring(4, 2)), Int32.Parse(r.DateStr.Substring(6, 2)));
                return dateOnly.ToBinary();
            }).ToArray();
            Buffer.BlockCopy(dateOnlyArr, 0, outputBin2, 0, dateOnlyArr.Length * 2);     // 'Object must be an array of primitives.'
            var navOnlyArr = dailyNavData.Select(r =>
            {
                float navValue = (float)Double.Parse(r.ValueStr);
                return navValue;
            }).ToArray();
            Buffer.BlockCopy(navOnlyArr, 0 , outputBin2, dailyNavData.Count * 2, navOnlyArr.Length * 4);     // 'Object must be an array of primitives.'
            var outputBin2Brotli = Utils.Bin2BrotliBin(outputBin2);


            string depositCsv = String.Join(",", dailyDepositData.Select(r =>
            {
                int nearestIntValue = (int)Math.Round(Double.Parse(r.ValueStr), MidpointRounding.AwayFromZero); // 0.5 is rounded to 1, -0.5 is rounded to -1. Good.
                return r.DateStr + "/" + nearestIntValue.ToString();
            }));
            var depositCsvBrotli = Utils.Str2BrotliBin(depositCsv); // 479 bytes compressed to 179 bytes. For very long term, we can save 1KB per user if compressing. Do it.


            var redisConnString = Program.gConfiguration.GetConnectionString("RedisDefault");
            IDatabase db = DbCommon.RedisManager.GetDb(redisConnString, 0);
            string redisKey = p_redisKeyPrefix + ".brotli";
            db.HashSet("assetQuoteRaw", redisKey,  RedisValue.CreateFrom(new System.IO.MemoryStream(outputCsvBrotli)));
            db.HashSet("assetBrokerNavDeposit", redisKey,  RedisValue.CreateFrom(new System.IO.MemoryStream(depositCsvBrotli)));

            Console.WriteLine("InsertNavAssetFromCsvFile() Ended. Conclusion: For 11 years of daily Date/float data. Without compression binary (18.4K) is smaller then CSV text (47.5K). But with Brotli binary (13.4K) is 41% bigger then brotli CSV text (9.5K). Because there is not much repeatable pattern in Float data, while a lot of repeatable comma and digits in text data. And Brotli is attacking the limits of theoretical compression possibilites. Conclusion: USE CSV text data with Brotli. Tested: Brotlied CSV is 30% smaller than brotlied binary.");
        }

        public void ExportNavAssetToTxt(string p_redisKeyPrefix, string p_filename)
        {
            IDatabase db = DbCommon.RedisManager.GetDb(Program.gConfiguration.GetConnectionString("RedisDefault"), 0);

            string redisKey = p_redisKeyPrefix + ".brotli"; // key: "9:1.brotli"
            byte[] dailyNavBrotli = db.HashGet("assetQuoteRaw", redisKey);
            if (dailyNavBrotli == null) {
                Console.WriteLine($"Error getting redisKey {redisKey}");
                return;
            }
            var dailyNavStr = Utils.BrotliBin2Str(dailyNavBrotli);
            string fullPath = Directory.GetCurrentDirectory() + "/" + p_filename;
            System.IO.File.WriteAllText (fullPath, dailyNavStr);
            Console.WriteLine($"Created file in CWD:'{fullPath}' from Redis/assetQuoteRaw/'{redisKey}'");
        }

        public void ImportNavAssetFromTxt(string p_redisKeyPrefix, string p_filename)
        {
            IDatabase db = DbCommon.RedisManager.GetDb(Program.gConfiguration.GetConnectionString("RedisDefault"), 0);

            string fullPath = Directory.GetCurrentDirectory() + "/" + p_filename;
            var dailyNavStr = System.IO.File.ReadAllText(fullPath);
            if (String.IsNullOrEmpty(dailyNavStr)) {
                Console.WriteLine($"Error reading file {fullPath}. File is empty. It is unexpected. Do not overwrite the DB.");
                return;
            }

            var dailyNavBrotli = Utils.Str2BrotliBin(dailyNavStr);
            string redisKey = p_redisKeyPrefix + ".brotli"; // key: "9:1.brotli"
            db.HashSet("assetQuoteRaw", redisKey,  RedisValue.CreateFrom(new System.IO.MemoryStream(dailyNavBrotli)));
            Console.WriteLine($"The assetQuoteRaw/'{redisKey}' was overwritten in RedisDb from the file '{fullPath}'");
        }
    }

}