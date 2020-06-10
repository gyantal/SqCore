using System;
using System.Diagnostics;
using System.Net.NetworkInformation;
using Microsoft.Extensions.Configuration;
using Npgsql;
using StackExchange.Redis;
using SqCommon;
using DbCommon;

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

    }

}