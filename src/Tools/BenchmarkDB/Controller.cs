using System;
using System.Diagnostics;
using System.Net.NetworkInformation;
using Microsoft.Extensions.Configuration;
using Npgsql;
using StackExchange.Redis;
using SqCommon;
using DbCommon;

namespace BenchmarkDB
{
    class Controller
    {
        private static readonly NLog.Logger gLogger = NLog.LogManager.GetCurrentClassLogger();
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
            Console.WriteLine($"Program.gConfiguration.GetConnectionString(PingDefault): '{address}'");
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
            gLogger.BeginGProfiling();
            Stopwatch watch = Stopwatch.StartNew();
            Stopwatch watch0 = Stopwatch.StartNew();
            conn.Open();
            watch0.Stop();
            Console.WriteLine($"Connection takes {watch0.Elapsed.TotalMilliseconds:0.00}ms");   // first connection: 360-392ms, later: 0, so connections are cached
            gLogger.GProfiledInfo("CONNECTION", true);
            gLogger.ProfiledInfo("CONNECTION", watch, true);

            // Insert some data
            Stopwatch watch1 = Stopwatch.StartNew();
            using (var cmd = new NpgsqlCommand())
            {
                cmd.Connection = conn;
                cmd.CommandText = "INSERT INTO testtable (column1) VALUES (@p)";
                cmd.Parameters.AddWithValue("p", "Hello SqCore.Tools.BenchmarkDB");
                cmd.ExecuteNonQuery();
            }
            watch1.Stop();
            Console.WriteLine($"INSERT takes {watch1.Elapsed.TotalMilliseconds:0.00}ms");    // "INSERT takes 27,33,30,37,29,30 ms". If I do it from pgAdmin, it says: 50msec
            gLogger.GProfiledInfo("INSERT", true);
            gLogger.ProfiledInfo("INSERT", watch, true);

            // Retrieve all rows
            Stopwatch watch2 = Stopwatch.StartNew();
            using (var cmd = new NpgsqlCommand("SELECT column1 FROM testtable", conn))
            using (var reader = cmd.ExecuteReader())
                while (reader.Read())
                    Console.WriteLine(reader.GetString(0));
            watch2.Stop();
            Console.WriteLine($"SELECT takes {watch2.Elapsed.TotalMilliseconds:0.00}ms");    // "SELECT takes 22,29,32,37,43,47,45 ms", If I do it from pgAdmin, it says: 64msec
            gLogger.GProfiledInfo("SELECT", true);
            gLogger.ProfiledInfo("SELECT", watch, true);

            // Delete inserted data
            Stopwatch watch3 = Stopwatch.StartNew();
            using (var cmd = new NpgsqlCommand())
            {
                cmd.Connection = conn;
                cmd.CommandText = "DELETE FROM testtable WHERE column1=@p;";
                cmd.Parameters.AddWithValue("p", "Hello SqCore.Tools.BenchmarkDB");
                cmd.ExecuteNonQuery();
            }
            watch3.Stop();
            Console.WriteLine($"DELETE takes {watch3.Elapsed.TotalMilliseconds:0.00}ms");    // "INSERT takes 27,33,30,37,29,30 ms". If I do it from pgAdmin, it says: 50msec
                                                                                             // pgAdmin running on local webserver reports worse numbers. maybe because pgAdmin first access local webserver + implemented badly. And that overhead is also calculated.
            gLogger.GProfiledInfo("DELETE", true);
            gLogger.ProfiledInfo("DELETE", watch, true);
        }


        public void TestRedisCache()
        {
            var redisConnString = Program.gConfiguration.GetConnectionString("RedisDefault");   // read from file
     
            Stopwatch watch0 = Stopwatch.StartNew();
            IDatabase db = RedisManager.GetDb(redisConnString, 0);
            watch0.Stop();
            Console.WriteLine($"Connection (GetDb()) takes {watch0.Elapsed.TotalMilliseconds :0.00}ms");     // first connection: 292(first)/72/70/64/83ms, so connections are not cached, but we can cache the connection manually

            // Insert some data
            Stopwatch watch1 = Stopwatch.StartNew();
            string value = "SqCore.Tools.BenchmarkDB";
            db.StringSet("SqCoreBenchmarkDbKey", value);
            watch1.Stop();
            Console.WriteLine($"INSERT takes {watch1.Elapsed.TotalMilliseconds :0.00}ms");    // "INSERT takes 31(first)/21/20/19/22/24 ms". 

            // Retrieve
            Stopwatch watch2 = Stopwatch.StartNew();
            string value2 = db.StringGet("SqCoreBenchmarkDbKey");
            watch2.Stop();
            Console.WriteLine(value2);
            Console.WriteLine($"SELECT takes {watch2.Elapsed.TotalMilliseconds :0.00}ms");    // "SELECT takes 30(first)/20/23/20/20/24 ms"

            // Delete
            Stopwatch watch3 = Stopwatch.StartNew();
            bool wasRemoved = db.KeyDelete("SqCoreBenchmarkDbKey");
            watch3.Stop();
            Console.WriteLine("Key was removed: " + wasRemoved);
            Console.WriteLine($"DELETE takes {watch3.Elapsed.TotalMilliseconds :0.00}ms");     // "SELECT takes 30(first)/20/23/20/20/24 ms"

            // pSql Insert (30ms)/select (45ms) is longer than Redis  Insert (19ms)/select (21ms). So, Redis cost basically 0ms CPU time, all is latency, while pSql is not.
        }

        public void BenchmarkAllAndConclusions(string p_pingConnStr, string p_pSqlConnStr, string p_redisConnStr)
        {
            // What to expect? : https://www.youtube.com/watch?v=sWh97AP1rB0  "Redis Query vs the PostgreSQL Query", 
            // Local server: Redis: 0.3msec, pSql: 3-4ms, on his slower web server: Redis: 1.6-2msec, pSql: 12-32ms
            // so Redis is 10-13x faster even for simple queries, and when both are in RAM. However, if pSQL data is on HDD, Redis should be 100x...1000x faster. (same 0.3msec vs. 300ms from reading disc)
            // So, it is totally worth to use Redis.
            // When I query from Windows PC the Irish remote servers, the latency (=ping time) = 24ms, so I cannot measure the Redis cash 1ms speed if latency is that big.
            // So, comparision of quering remote servers from local PC is not meaningful. We have to run this BenchmarkAllConclusions on the server.
            Console.WriteLine("Redis is shown to be 10-13x faster even for simple queries when all data is in RAM. However, when pSQL data is on HDD (which is always) or complex JOIN queries (300ms), Redis (same 0.3msec) should be 100x...1000x faster.");
            Console.WriteLine("Benchmarking remote servers from local PC is not meaningful, because the 1ms Redis responses cannot be measured if ping latency to Dublin is 24ms. Run this benchmark on the server where the WebServer resides.");
            Console.WriteLine("Expected latency times (speedtest.net) from London client. Servers locations: London: 9ms, Dublin: 20-21-23 ms, NY: 78-86-110ms" + Environment.NewLine);




            // Part 1: PING
            int nPingTries = Utils.InvariantConvert<int>(Program.gConfiguration["AppSettings:TestPingNTries"]);   
            long sumPingTimes = 0;
            for (int i = 0; i < nPingTries; i++)
            {
                try
                {
                    Ping myPing = new Ping();
                    PingReply reply = myPing.Send(p_pingConnStr, 1000);
                    if (reply != null)
                    {
                        sumPingTimes += reply.RoundtripTime;
                        //Console.WriteLine("Status :  " + reply.Status + ", Time : " + reply.RoundtripTime.ToString() + "ms, Address : " + reply.Address);
                    }
                }
                catch
                {
                    Console.WriteLine("ERROR: There is some TIMEOUT issue.");
                }
            }
            Console.WriteLine($"PING: nTries {nPingTries}. Average time to server '{p_pingConnStr}': {sumPingTimes / (double)nPingTries :0.00}ms");       // Ping takes 24 ms

            // Part 2: pSql
            using (var conn = new NpgsqlConnection(p_pSqlConnStr))
            {
                Stopwatch watch0 = Stopwatch.StartNew();
                conn.Open();
                watch0.Stop();
                Console.WriteLine($"{Environment.NewLine}PostgreSql:  ({p_pSqlConnStr.TruncateLongString(50)}) {Environment.NewLine}Connection takes {watch0.ElapsedMilliseconds :0.00}ms");    // first connection: 360-392ms, later: 0, so connections are cached

                // Insert some data
                Stopwatch watch1 = Stopwatch.StartNew();
                using (var cmd = new NpgsqlCommand())
                {
                    cmd.Connection = conn;
                    cmd.CommandText = "INSERT INTO testtable (column1) VALUES (@p)";
                    cmd.Parameters.AddWithValue("p", "Hello SqCore.Tools.BenchmarkDB");
                    cmd.ExecuteNonQuery();
                }
                watch1.Stop();
                //Console.WriteLine("INSERT takes " + watch1.ElapsedMilliseconds + " ms");    // "INSERT takes 27,33,30,37,29,30 ms". If I do it from pgAdmin, it says: 50msec

                // Retrieve all rows
                //int nSqlSelectTries = 10;     // don't implement it for a while to see the variability too.
                Stopwatch watch2 = Stopwatch.StartNew();
                int nRows = 0;
                using (var cmd = new NpgsqlCommand("SELECT column1 FROM testtable", conn))
                using (var reader = cmd.ExecuteReader())
                    while (reader.Read())
                        nRows++;
                        //Console.WriteLine(reader.GetString(0));
                watch2.Stop();
                Console.WriteLine($"SELECT {nRows} rows takes {watch2.Elapsed.TotalMilliseconds :0.00}ms");    // "SELECT takes 22,29,32,37,43,47,45 ms", If I do it from pgAdmin, it says: 64msec

                // Delete inserted data
                Stopwatch watch3 = Stopwatch.StartNew();
                using (var cmd = new NpgsqlCommand())
                {
                    cmd.Connection = conn;
                    cmd.CommandText = "DELETE FROM testtable WHERE column1=@p;";
                    cmd.Parameters.AddWithValue("p", "Hello SqCore.Tools.BenchmarkDB");
                    cmd.ExecuteNonQuery();
                }
                watch3.Stop();
                //Console.WriteLine("DELETE takes " + watch3.ElapsedMilliseconds + " ms");    // "INSERT takes 27,33,30,37,29,30 ms". If I do it from pgAdmin, it says: 50msec
            }

            // Part 3: Redis
            {
                Stopwatch watch0 = Stopwatch.StartNew();
                IDatabase db = RedisManager.GetDb(p_redisConnStr, 0);
                watch0.Stop();
                Console.WriteLine($"{Environment.NewLine}Redis:  ({p_redisConnStr.TruncateLongString(50)}){Environment.NewLine}Connection takes {watch0.ElapsedMilliseconds :0.00}ms");     // first connection: 292(first)/72/70/64/83ms, so connections are not cached, but we can cache the connection manually

                // Insert some data
                Stopwatch watch1 = Stopwatch.StartNew();
                string value = "SqCore.Tools.BenchmarkDB";
                db.StringSet("SqCoreBenchmarkDbKey", value);
                watch1.Stop();
                //Console.WriteLine($"INSERT takes {watch1.Elapsed.TotalMilliseconds:0.00}ms");    // "INSERT takes 31(first)/21/20/19/22/24 ms". 

                // Retrieve
                Stopwatch watch2 = Stopwatch.StartNew();
                string value2 = db.StringGet("SqCoreBenchmarkDbKey");
                watch2.Stop();
                Console.WriteLine($"SELECT 1 keys takes {watch2.Elapsed.TotalMilliseconds :0.00}ms");    // "SELECT takes 30(first)/20/23/20/20/24 ms"

                // Delete
                Stopwatch watch3 = Stopwatch.StartNew();
                bool wasRemoved = db.KeyDelete("SqCoreBenchmarkDbKey");
                watch3.Stop();
                //Console.WriteLine("Key was removed: " + wasRemoved);
                //Console.WriteLine($"DELETE takes {watch3.Elapsed.TotalMilliseconds:0.00}ms");     // "SELECT takes 30(first)/20/23/20/20/24 ms"
            }


            // Result of running BenchmarkDB on Linux server.
            // 1. ****************** Linux To 'Remote' itself (with IP numbers)
            // PostgreSql:
            // Connection takes 460.00ms
            // SELECT 6 rows takes 0.88ms

            // Redis:
            // Connection takes 175.00ms
            // SELECT 1 keys takes 2.06ms

            // PostgreSql:
            // Connection takes 0.00ms
            // SELECT 6 rows takes 0.30ms

            // Redis:
            // Connection takes 0.00ms
            // SELECT 1 keys takes 0.27ms

            // 2. ****************** Linux To 'localhost'
            // PostgreSql:
            // Connection takes 11.00ms
            // SELECT 6 rows takes 0.30ms

            // Redis:
            // Connection takes 6.00ms
            // SELECT 1 keys takes 0.24ms

            // PostgreSql:
            // Connection takes 0.00ms
            // SELECT 6 rows takes 0.42ms

            // Redis:
            // Connection takes 0.00ms
            // SELECT 1 keys takes 0.34ms

            // *************** Conclusion of the measurement: with this small data and all in memory, we cannot measure any difference in speed. Even though it was shown in a YouTube video. 
            // However, we are sure there is 10x-1000x speed difference in difficult data scenarios.
            // note INSERT takes 1-9msec on pSql and 0.4..1.2 msec on Redis, so INSERT is much faster, which is expected if pSql has to write data to file.
            // pSql should be used when data loss is not expectable, because SQL write data do disc immediately. Redis write dBFile to disc every 5 minutes, which is fine for us almost always.
            // Even if we write trade transactions to Portfolio, we can ask Redis to Save data file after we inserted all transactions.
        }

    }

}