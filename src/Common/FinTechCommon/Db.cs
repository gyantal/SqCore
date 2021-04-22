using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using DbCommon;
using Microsoft.Extensions.Primitives;
using SqCommon;
using StackExchange.Redis;

namespace FinTechCommon
{
    public class SrvLoadPrHistInDb	// for quick JSON deserialization. In DB the fields has short names, and not all Asset fields are in the DB anyway
    {
        public string LoadPrHist { get; set; } = string.Empty;
    }

    // Abstract class representing pshysical DBs (Sql, Redis as an aggregate)
    // Decouple the concrete databases from the using code. 
    // This front end wraps Redis and Sql Database, so a user doesn't even know whether data comes from Sql or Redis.
    // The user of the DB receives only higher level data (User), but never actual database implementation (UserInDb)
    // Later, Redis can be changed to other noSql memory database, or PostgreSql can be changed to MySql
    public partial class Db
    {
        // public static Db gDb = new Db();

#pragma warning disable CS8618 // Non-nullable field 'm_redisDb' is uninitialized.
        IDatabase m_redisDb;
         //public IDatabase? SqlDb { get; set; } = null;
#pragma warning restore CS8618
        int m_redisDbInd;
        string m_lastUsersStr = string.Empty;
        string m_lastAssetsStr = string.Empty;
        string m_lastSrvLoadPrHistStr = string.Empty;

        public Db(IDatabase p_redisDb, IDatabase? p_sqlDb)
        {
            m_redisDb = p_redisDb;
        }

        public Db(string p_redisConnString, int p_redisDbNum, IDatabase? p_sqlDb)
        {
            m_redisDbInd = p_redisDbNum;
            m_redisDb = RedisManager.GetDb(p_redisConnString, m_redisDbInd);   // lowest level DB module
        }


        public (bool, User[]?, List<Asset>?) GetDataIfReloadNeeded()
        {
            // Although 'Assets.brotli' would be 520bytes instead of 1.52KB, we don't not use binary brotli data for Assets, only for historical data.
            // Reason is that it is difficult to maintain, append new Stocks into Redis.Assets if it is binary brotli. Just a lot of headache.
            // At the same time it is a small table (not historical), and it is only loaded at once at program start, when we can afford longer loading times.
            
            string sqUserDataStr = m_redisDb.StringGet("sq_user");
            bool isUsersChangedInDb = m_lastUsersStr != sqUserDataStr;
            string assetsStr = m_redisDb.HashGet("memDb", "Assets");
            bool isAllAssetsChangedInDb = m_lastAssetsStr != assetsStr;
            string srvLoadPrHistStr = m_redisDb.HashGet("memDb", "Srv.LoadPrHist");
            bool isSqCoreWebAssetsChanged = m_lastSrvLoadPrHistStr != srvLoadPrHistStr;

            bool isReloadNeeded = isUsersChangedInDb || isAllAssetsChangedInDb || isSqCoreWebAssetsChanged;
            if (!isReloadNeeded)
                return (false, null, null);

            m_lastUsersStr = sqUserDataStr;
            m_lastAssetsStr = assetsStr;
            m_lastSrvLoadPrHistStr = srvLoadPrHistStr;

            User[] users = GetUsers(m_lastUsersStr);
            List<Asset> assets = GetAssetsFromJson(m_lastAssetsStr, users);
            AddLoadPrHistToAssets(m_lastSrvLoadPrHistStr, assets);
            return (isReloadNeeded, users, assets);
        }


        List<Asset> GetAssetsFromJson(string p_json, User[] users)
        {
            List<Asset> assets = new List<Asset>();
            using (JsonDocument doc = JsonDocument.Parse(p_json))
            {
                foreach (JsonElement row in doc.RootElement.GetProperty("C").EnumerateArray())
                {
                    assets.Add(new Cash(row));
                }
                foreach (JsonElement row in doc.RootElement.GetProperty("D").EnumerateArray())
                {
                    assets.Add(new CurrPair(row));
                }
                foreach (JsonElement row in doc.RootElement.GetProperty("R").EnumerateArray())
                {
                    assets.Add(new RealEstate(row, users));
                }
                foreach (JsonElement row in doc.RootElement.GetProperty("N").EnumerateArray())
                {
                    assets.Add(new BrokerNav(row, users));
                }
                foreach (JsonElement row in doc.RootElement.GetProperty("P").EnumerateArray())
                {
                    assets.Add(new Portfolio(row, users));
                }
                foreach (JsonElement row in doc.RootElement.GetProperty("A").EnumerateArray())
                {
                    assets.Add(new Company(row));
                }
                foreach (JsonElement row in doc.RootElement.GetProperty("S").EnumerateArray())
                {
                    assets.Add(new Stock(row, assets));
                }
            }

            // Double check that sqTickers are unique id
            Dictionary<string, Asset> assetChecker = new Dictionary<string, Asset>();
            foreach (var asset in assets)
            {
                if (assetChecker.ContainsKey(asset.SqTicker))
                    throw new SqException($"Warning! Asset.SqTicker '{asset.SqTicker}' is not unique.");
                assetChecker[asset.SqTicker] = asset;
            }

            return assets;
        }

        private void AddLoadPrHistToAssets(string p_json, List<Asset> assets)
        {
            var srvLoadPrHist = JsonSerializer.Deserialize<Dictionary<string, SrvLoadPrHistInDb>>(p_json);
            if (srvLoadPrHist == null)
                throw new SqException($"Deserialize failed on '{p_json}'");

            foreach (var item in srvLoadPrHist)
            {
                string sqTicker = item.Key;
                DateTime startDate =  GetExpectedHistoryStartDate(item.Value.LoadPrHist, sqTicker);
                Asset asset = assets.Find(r => r.SqTicker == sqTicker)!;
                if (asset is Stock)
                    ((Stock)asset).ExpectedHistoryStartDateLoc = startDate;
                else if (asset is BrokerNav)
                    ((BrokerNav)asset).ExpectedHistoryStartDateLoc = startDate;
                else if (asset is CurrPair)
                    ((CurrPair)asset).ExpectedHistoryStartDateLoc = startDate;
                else
                    throw new NotImplementedException();
            }
        }

        public DateTime GetExpectedHistoryStartDate(string p_expectedHistorySpan, string p_ticker)
        {
            DateTime startDateET = new DateTime(2018, 02, 01, 0, 0, 0);
            if (p_expectedHistorySpan.StartsWith("Date:"))
            {
                if (!DateTime.TryParseExact(p_expectedHistorySpan.Substring("Date:".Length), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out startDateET))
                    throw new SqException($"ReloadHistoricalDataAndSetTimer(): wrong ExpectedHistorySpan for ticker {p_ticker}");
            }
            else if (p_expectedHistorySpan.EndsWith("y"))
            {
                if (!Int32.TryParse(p_expectedHistorySpan.Substring(0, p_expectedHistorySpan.Length - 1), out int nYears))
                    throw new SqException($"ReloadHistoricalDataAndSetTimer(): wrong ExpectedHistorySpan for ticker {p_ticker}");
                startDateET = DateTime.UtcNow.FromUtcToEt().AddYears(-1 * nYears).Date;
            }
            else if (p_expectedHistorySpan.EndsWith("m")) // RenewedUber requires only the last 2-3 days. Last 1year is unnecessary, so do only last 2 months
            {
                if (!Int32.TryParse(p_expectedHistorySpan.Substring(0, p_expectedHistorySpan.Length - 1), out int nMonths))
                    throw new SqException($"ReloadHistoricalDataAndSetTimer(): wrong ExpectedHistorySpan for ticker {p_ticker}");
                startDateET = DateTime.UtcNow.FromUtcToEt().AddMonths(-1 * nMonths).Date;
            }

            if (!p_expectedHistorySpan.StartsWith("Date:")) // if "Date:" was given, we assume admin was specific for a reason. Then don't go back 1 day earlier. Otherwise (months, years), go back 1 day earlier for safety.
            {
                // Keep this method in MemDb, cos we might use MemDb.Holiday data in the future.
                // if startDateET is weekend, we have to go back to previous Friday
                if (startDateET.DayOfWeek == DayOfWeek.Sunday)
                    startDateET = startDateET.AddDays(-2);
                if (startDateET.DayOfWeek == DayOfWeek.Saturday)
                    startDateET = startDateET.AddDays(-1);
                startDateET = startDateET.AddDays(-1);  // go back another extra day, in case that Friday was a stock market holiday
            }

            return startDateET;
        }

        public User[] GetUsers()
        {
            string sqUserDataStr = m_redisDb.StringGet("sq_user");
            return GetUsers(sqUserDataStr);
        }

        public User[] GetUsers(string p_sqUserDataStr)
        {
            var usersInDb = JsonSerializer.Deserialize<List<UserInDb>>(p_sqUserDataStr);
            if (usersInDb == null)
                throw new SqException($"Deserialize failed on '{p_sqUserDataStr}'");
            return usersInDb.Select(r =>
            {
                return new User()
                {
                    Id = r.id,
                    Username = r.username,
                    Password = r.password,
                    Title = r.title,
                    Firstname = r.firstname,
                    Lastname = r.lastname,
                    Email = r.email
                };
            }).ToArray();
        }

        public Dictionary<string, List<Split>> GetMissingYfSplits()
        {
            string missingYfSplitsJson = m_redisDb.HashGet("memDb", "Hist.Splits.MissingYF");
            Dictionary<string, List<Split>>? potentialMissingYfSplits = JsonSerializer.Deserialize<Dictionary<string, List<Split>>>(missingYfSplitsJson); // JsonSerializer: Dictionary key <int>,<uint> is not supported
            if (potentialMissingYfSplits == null)
                throw new SqException($"Deserialize failed on '{missingYfSplitsJson}'");
            return potentialMissingYfSplits;
        }

        public string? GetAssetQuoteRaw(AssetId32Bits p_assetId)
        {
            string redisKey = p_assetId.ToString() + ".brotli"; // // key: "9:1.brotli"
            byte[] dailyNavBrotli = m_redisDb.HashGet("assetQuoteRaw", redisKey);
            if (dailyNavBrotli == null)
                return null;
            var dailyNavStr = Utils.BrotliBin2Str(dailyNavBrotli);  // "D/C" for Date/Closes: "D/C,20090102/16461,20090105/16827,..."
            return dailyNavStr;
        }

        public void SetAssetQuoteRaw(AssetId32Bits p_assetId, string p_dailyNavStr)
        {
            string redisKey = p_assetId.ToString() + ".brotli"; // // key: "9:1.brotli"
            var outputCsvBrotli = Utils.Str2BrotliBin(p_dailyNavStr);
            m_redisDb.HashSet("assetQuoteRaw", redisKey,  RedisValue.CreateFrom(new System.IO.MemoryStream(outputCsvBrotli)));
        }

        public KeyValuePair<DateOnly, double>[] GetAssetBrokerNavDeposit(AssetId32Bits p_assetId)
        {
            string redisKey = p_assetId.ToString() + ".brotli"; // key: "9:1.brotli"
            byte[] dailyDepositBrotli = m_redisDb.HashGet("assetBrokerNavDeposit", redisKey);
            var dailyDepositStr = Utils.BrotliBin2Str(dailyDepositBrotli);  // 479 byte text data from 179 byte brotli data, starts with FormatString: "20090310/1903,20100305/2043,..."
            KeyValuePair<DateOnly, double>[] deposits = dailyDepositStr.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(r =>
            {
                // format: "20200323/-1000000"
                var depositsDays = r.Split('/', StringSplitOptions.RemoveEmptyEntries);
                DateTime date = Utils.FastParseYYYYMMDD(new StringSegment(r, 0, 8));
                double deposit = Double.Parse(new StringSegment(r, 9, r.Length - 9));
                return new KeyValuePair<DateOnly, double>(new DateOnly(date), deposit);
            }).ToArray();
            return deposits;
        }

        public bool UpdateBrotlisIfNeeded()
        {
            // For assets, and small tables, it is too much of a hassle and not much RAM saving. And it is better that we are able to change text data manually in RedisDesktop
            // We only use brotli compression for big price data in the future.

            // string allAssetsJson = m_redisDb.HashGet("memDb", "allAssets");
            // byte[] allAssetsBin = m_redisDb.HashGet("memDb", "allAssets.brotli");
            // var allAssetsBinToStr = Utils.BrotliBin2Str(allAssetsBin);

            // bool wasAnyBrotliUpdated = false;
            // if (allAssetsJson != allAssetsBinToStr)
            // {
            //     // Write brotli to DB
            //     var allAssetsBrotli = Utils.Str2BrotliBin(allAssetsJson);
            //     m_redisDb.HashSet("memDb", "allAssets.brotli", RedisValue.CreateFrom(new System.IO.MemoryStream(allAssetsBrotli)));
            //     wasAnyBrotliUpdated = true;
            // }
            // return wasAnyBrotliUpdated;
            return false;
        }
    }
}