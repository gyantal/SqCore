using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using Microsoft.Extensions.Primitives;
using SqCommon;
using StackExchange.Redis;

namespace FinTechCommon
{
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
        string m_lastUsersStr = string.Empty;
        string m_lastAllAssetsStr = string.Empty;
        string m_lastSqCoreWebAssetsStr = string.Empty;

        public Db(IDatabase p_redisDb, IDatabase? p_sqlDb)
        {
            m_redisDb = p_redisDb;
        }

        public (bool, User[]?, List<Asset>?) GetDataIfReloadNeeded()
        {
            string sqUserDataStr = m_redisDb.StringGet("sq_user");
            bool isUsersChangedInDb = m_lastUsersStr != sqUserDataStr;
            if (isUsersChangedInDb)
            {
                m_lastUsersStr = sqUserDataStr;
            }

            // start using Redis:'allAssets.brotli' (520bytes instead of 1.52KB) immediately. See UpdateRedisBrotlisService();
            byte[] allAssetsBin = m_redisDb.HashGet("memDb", "allAssets.brotli");
            var allAssetsBinToStr = Utils.BrotliBin2Str(allAssetsBin);
            bool isAllAssetsChangedInDb = m_lastAllAssetsStr != allAssetsBinToStr;
            if (isAllAssetsChangedInDb)
            {
                m_lastAllAssetsStr = allAssetsBinToStr;
            }

            string sqCoreWebAssetsStr = m_redisDb.HashGet("memDb", "SqCoreWebAssets");
            bool isSqCoreWebAssetsChanged = m_lastSqCoreWebAssetsStr != sqCoreWebAssetsStr;
            if (isSqCoreWebAssetsChanged)
            {
                m_lastSqCoreWebAssetsStr = sqCoreWebAssetsStr;
            }

            bool isReloadNeeded = isUsersChangedInDb || isAllAssetsChangedInDb || isSqCoreWebAssetsChanged;
            if (!isReloadNeeded)
                return (false, null, null);

            User[]? users = GetUsers(sqUserDataStr);

            var allAssets = JsonSerializer.Deserialize<Dictionary<string, AssetInDb[]>>(m_lastAllAssetsStr);
            if (allAssets == null)
                throw new Exception($"Deserialize failed on '{m_lastAllAssetsStr}'");

            var sqCoreWebAssets = JsonSerializer.Deserialize<Dictionary<string, SqCoreWebAssetInDb>>(m_lastSqCoreWebAssetsStr);
            if (sqCoreWebAssets == null)
                throw new Exception($"Deserialize failed on '{m_lastSqCoreWebAssetsStr}'");

            // select only a subset of the allAssets in DB that SqCore webapp needs
            List<Asset> sqCoreAssets = sqCoreWebAssets.Select(r =>
            {
                var assetId = new AssetId32Bits(r.Key);
                var assetTypeArr = allAssets[((byte)assetId.AssetTypeID).ToString()];
                        // Linq is slow. List<T>.Find() is faster than Linq.FirstOrDefault() https://stackoverflow.com/questions/14032709/performance-of-find-vs-firstordefault
                        var assetFromDb = Array.Find(assetTypeArr, k => k.ID == assetId.SubTableID);
                if (assetFromDb == null)
                    throw new Exception($"Asset is not found: '{assetId.AssetTypeID}:{assetId.SubTableID}'");

                User? user = null;
                if (assetId.AssetTypeID == AssetType.BrokerNAV)
                {
                    user = users.FirstOrDefault(k => k.Id == Int32.Parse(assetFromDb.user_id));
                }
                return new Asset()
                {
                    AssetId = assetId,
                    PrimaryExchange = ExchangeId.NYSE, // NYSE is is larger than Nasdaq. If it is not specified assume NYSE. Saving DB space. https://www.statista.com/statistics/270126/largest-stock-exchange-operators-by-market-capitalization-of-listed-companies/
                            LastTicker = assetFromDb.Ticker,
                    LastName = assetFromDb.Name,
                    ExpectedHistorySpan = r.Value.LoadPrHist,
                    ExpectedHistoryStartDateET = GetExpectedHistoryStartDate(r.Value.LoadPrHist, assetFromDb.Ticker),
                    User = user
                };
            }).ToList();

            return (isReloadNeeded, users, sqCoreAssets);
        }

        public DateTime GetExpectedHistoryStartDate(string p_expectedHistorySpan, string p_ticker)
        {
            DateTime startDateET = new DateTime(2018, 02, 01, 0, 0, 0);
            if (p_expectedHistorySpan.StartsWith("Date:"))
            {
                if (!DateTime.TryParseExact(p_expectedHistorySpan.Substring("Date:".Length), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out startDateET))
                    throw new Exception($"ReloadHistoricalDataAndSetTimer(): wrong ExpectedHistorySpan for ticker {p_ticker}");
            }
            else if (p_expectedHistorySpan.EndsWith("y"))
            {
                if (!Int32.TryParse(p_expectedHistorySpan.Substring(0, p_expectedHistorySpan.Length - 1), out int nYears))
                    throw new Exception($"ReloadHistoricalDataAndSetTimer(): wrong ExpectedHistorySpan for ticker {p_ticker}");
                startDateET = DateTime.UtcNow.FromUtcToEt().AddYears(-1 * nYears).Date;
            }
            else if (p_expectedHistorySpan.EndsWith("m")) // RenewedUber requires only the last 2-3 days. Last 1year is unnecessary, so do only last 2 months
            {
                if (!Int32.TryParse(p_expectedHistorySpan.Substring(0, p_expectedHistorySpan.Length - 1), out int nMonths))
                    throw new Exception($"ReloadHistoricalDataAndSetTimer(): wrong ExpectedHistorySpan for ticker {p_ticker}");
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
                throw new Exception($"Deserialize failed on '{p_sqUserDataStr}'");
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

        public Dictionary<AssetId32Bits, List<Split>> GetMissingYfSplits()
        {
            string missingYfSplitsJson = m_redisDb.HashGet("memDb", "missingYfSplits");
            Dictionary<AssetId32Bits, List<Split>>? potentialMissingYfSplits = JsonSerializer.Deserialize<Dictionary<string, List<Split>>>(missingYfSplitsJson) // JsonSerializer: Dictionary key <int>,<uint> is not supported
                !.ToDictionary(r => new AssetId32Bits(r.Key), r => r.Value);
            if (potentialMissingYfSplits == null)
                throw new Exception($"Deserialize failed on '{missingYfSplitsJson}'");
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
            string allAssetsJson = m_redisDb.HashGet("memDb", "allAssets");
            byte[] allAssetsBin = m_redisDb.HashGet("memDb", "allAssets.brotli");
            var allAssetsBinToStr = Utils.BrotliBin2Str(allAssetsBin);

            bool wasAnyBrotliUpdated = false;
            if (allAssetsJson != allAssetsBinToStr)
            {
                // Write brotli to DB
                var allAssetsBrotli = Utils.Str2BrotliBin(allAssetsJson);
                m_redisDb.HashSet("memDb", "allAssets.brotli", RedisValue.CreateFrom(new System.IO.MemoryStream(allAssetsBrotli)));
                wasAnyBrotliUpdated = true;
            }
            return wasAnyBrotliUpdated;
        }
    }
}