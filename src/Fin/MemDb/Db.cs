using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using DbCommon;
using Microsoft.Extensions.Primitives;
using SqCommon;
using StackExchange.Redis;

namespace Fin.MemDb;

public class SrvLoadPrHistInDb // for quick JSON deserialization. In DB the fields has short names, and not all Asset fields are in the DB anyway
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

    readonly IDatabase m_redisDb;

    public IDatabase? m_sqlDb;
    readonly int m_redisDbIdx;
    public int RedisDbIdx { get { return m_redisDbIdx; } }
    string? m_lastUsersStr = null;
    string? m_lastAssetsStr = null;
    string? m_lastSrvLoadPrHistStr = string.Empty;
    HashEntry[]? m_lastPortfolioFoldersRds = null;
    HashEntry[]? m_lastPortfoliosRds = null;

    public Db(IDatabase p_redisDb, IDatabase? p_sqlDb)
    {
        m_redisDb = p_redisDb;
        m_redisDbIdx = 0;
        m_sqlDb = p_sqlDb;
    }

    public Db(string p_redisConnString, int p_redisDbIdx, IDatabase? p_sqlDb)
    {
        m_redisDbIdx = p_redisDbIdx;
        m_redisDb = RedisManager.GetDb(p_redisConnString, m_redisDbIdx);   // lowest level DB module
        m_sqlDb = p_sqlDb;
    }

    public (bool IsDbReloadNeeded, User[]? Users, List<Asset>? Assets, Dictionary<int, PortfolioFolder>? PortfolioFolders, Dictionary<int, Portfolio>? Portfolios) GetDataIfReloadNeeded()
    {
        // Although 'Assets.brotli' would be 520bytes instead of 1.52KB, we don't not use binary brotli data for Assets, only for historical data.
        // Reason is that it is difficult to maintain, append new Stocks into Redis.Assets if it is binary brotli. Just a lot of headache.
        // At the same time it is a small table (not historical), and it is only loaded at once at program start, when we can afford longer loading times.

        string? sqUserDataStr = m_redisDb.StringGet("sq_user");
        bool isUsersChangedInDb = m_lastUsersStr != sqUserDataStr;
        string? assetsStr = m_redisDb.HashGet("memDb", "Assets");
        bool isAllAssetsChangedInDb = m_lastAssetsStr != assetsStr;
        string? srvLoadPrHistStr = m_redisDb.HashGet("memDb", "Srv.LoadPrHist");
        bool isSqCoreWebAssetsChanged = m_lastSrvLoadPrHistStr != srvLoadPrHistStr;
        HashEntry[]? portfolioFoldersRds = m_redisDb.HashGetAll("portfolioFolder");
        bool isPortfFoldersChangedInDb = !DbUtils.IsRedisAllHashEqual(portfolioFoldersRds, m_lastPortfolioFoldersRds);
        HashEntry[]? portfoliosRds = m_redisDb.HashGetAll("portfolio");
        bool isPortfoliosChangedInDb = !DbUtils.IsRedisAllHashEqual(portfoliosRds, m_lastPortfoliosRds);

        // ToDo: Refactoring in the future: if RedisDb is down now, and it is null => don't overwrite our MemDb data (we should keep our old data)
        bool isReloadNeeded = isUsersChangedInDb || isAllAssetsChangedInDb || isSqCoreWebAssetsChanged || isPortfFoldersChangedInDb || isPortfoliosChangedInDb;
        if (!isReloadNeeded)
            return (false, null, null, null, null);

        m_lastUsersStr = sqUserDataStr;
        User[] users = GetUsers(m_lastUsersStr);

        m_lastAssetsStr = assetsStr;
        List<Asset> assets = GetAssetsFromJson(m_lastAssetsStr, users);

        m_lastPortfolioFoldersRds = portfolioFoldersRds;
        Dictionary<int, PortfolioFolder> portfolioFolders = GetPortfolioFolders(portfolioFoldersRds, users);

        m_lastPortfoliosRds = portfoliosRds;
        Dictionary<int, Portfolio> portfolios = GetPortfolios(portfoliosRds, users, assets);

        // Add the user's AggNavAsset into assets very early, so we don't have to Add items to AssetCache later, because that is problematic in multithreaded app (of something iterates over AssetCache)
        // Imitate that the Db already has the AggNavAsset (other option is to move this addition up to MemDb, instead of this Db class)
        // NAV assets should be grouped by user, because we create a synthetic new aggregatedNAV. This aggregate should add up the RAW UnadjustedNAV (not adding up the adjustedNAV), so we have to create it at MemDbReload.
        var navAssets = assets.Where(r => r.AssetId.AssetTypeID == AssetType.BrokerNAV).Select(r => (BrokerNav)r);
        var navAssetsByUser = navAssets.ToLookup(r => r.User); // ToLookup() uses User.Equals()
        int nVirtualAggNavAssets = 0;
        foreach (IGrouping<User?, BrokerNav>? navAssetsOfUser in navAssetsByUser)
        {
            // only aggregate IB assets, not TradeStation assets, because we only have histQuotes for IB. So "DC.IM", "DC.ID" is considered, but "DC.TM" is not. Trader code starts with letter "I"
            List<BrokerNav> subNavAssets = navAssetsOfUser.Where(r => r.Symbol[3] == 'I').ToList();    // it adds all BrokerNav for DC: IbMain+IbDeBlan+TradeStation  (if problem, just code in that TradeStation Navs are not considered: "DC.TM","TS Main NAV, DC","DC.TM.NAV")
            if (subNavAssets.Count >= 2) // if more than 2 NAVs for the user has valid history, a virtual synthetic aggregatedNAV and a virtual AssetID should be generated
            {
                User user = navAssetsOfUser.Key!;
                string aggAssetSqTicker = "N/" + user.Initials; // e.g. "N/DC";
                var aggAssetId = new AssetId32Bits(AssetType.BrokerNAV, (uint)(10000 + nVirtualAggNavAssets++));
                var aggNavAsset = new BrokerNav(aggAssetId, user.Initials, "Aggregated NAV, " + user.Initials, string.Empty, CurrencyId.USD, false, user, GetExpectedHistoryStartDate("1y", aggAssetSqTicker), subNavAssets);
                subNavAssets.ForEach(r => r.AggregateNavParent = aggNavAsset);
                assets.Add(aggNavAsset);
            }
        } // NAVs per user

        m_lastSrvLoadPrHistStr = srvLoadPrHistStr;
        AddLoadPrHistToAssets(m_lastSrvLoadPrHistStr, assets);
        return (isReloadNeeded, users, assets, portfolioFolders, portfolios);
    }

    static List<Asset> GetAssetsFromJson(string? p_json, User[] users)
    {
        if (p_json == null)
            return new List<Asset>();

        List<Asset> assets = new();
        using (JsonDocument doc = JsonDocument.Parse(p_json))
        {
            StrongAssert.True(doc.RootElement.GetProperty("P").GetArrayLength() == 0, Severity.ThrowException, "Portfolios  should come from RedisDb, because they can have many parameters");

            foreach (JsonElement row in doc.RootElement.GetProperty("C").EnumerateArray())
            {
                assets.Add(new Cash(row));
            }
            foreach (JsonElement row in doc.RootElement.GetProperty("D").EnumerateArray())
            {
                assets.Add(new CurrPair(row));
            }
            foreach (JsonElement row in doc.RootElement.GetProperty(AssetHelper.gAssetTypeCode[AssetType.FinIndex].ToString()).EnumerateArray())
            {
                assets.Add(new FinIndex(row));
            }
            foreach (JsonElement row in doc.RootElement.GetProperty("R").EnumerateArray())
            {
                assets.Add(new RealEstate(row, users));
            }
            foreach (JsonElement row in doc.RootElement.GetProperty("N").EnumerateArray())
            {
                assets.Add(new BrokerNav(row, users));
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
        Dictionary<string, Asset> assetChecker = new();
        foreach (var asset in assets)
        {
            if (assetChecker.ContainsKey(asset.SqTicker))
                throw new SqException($"Warning! Asset.SqTicker '{asset.SqTicker}' is not unique.");
            assetChecker[asset.SqTicker] = asset;
        }

        return assets;
    }

    private static void AddLoadPrHistToAssets(string? p_json, List<Asset> assets)
    {
        if (p_json == null)
            return;
        var srvLoadPrHist = JsonSerializer.Deserialize<Dictionary<string, SrvLoadPrHistInDb>>(p_json)
            ?? throw new SqException($"Deserialize failed on '{p_json}'");
        foreach (var item in srvLoadPrHist)
        {
            string sqTicker = item.Key;
            DateTime startDate = GetExpectedHistoryStartDate(item.Value.LoadPrHist, sqTicker);
            Asset asset = assets.Find(r => r.SqTicker == sqTicker)!;
            asset.ExpectedHistoryStartDateLoc = startDate;
        }
    }

    public static DateTime GetExpectedHistoryStartDate(string p_expectedHistorySpan, string p_ticker)
    {
        DateTime startDateET = new(2018, 02, 01, 0, 0, 0);
        if (p_expectedHistorySpan.StartsWith("Date:"))
        {
            if (!DateTime.TryParseExact(p_expectedHistorySpan["Date:".Length..], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out startDateET))
                throw new SqException($"ReloadHistoricalDataAndSetTimer(): wrong ExpectedHistorySpan for ticker {p_ticker}");
        }
        else if (p_expectedHistorySpan.EndsWith("y"))
        {
            if (!Int32.TryParse(p_expectedHistorySpan[0..^1], out int nYears))
                throw new SqException($"ReloadHistoricalDataAndSetTimer(): wrong ExpectedHistorySpan for ticker {p_ticker}");
            startDateET = DateTime.UtcNow.FromUtcToEt().AddYears(-1 * nYears).Date;
        }
        else if (p_expectedHistorySpan.EndsWith("m")) // RenewedUber requires only the last 2-3 days. Last 1year is unnecessary, so do only last 2 months
        {
            if (!Int32.TryParse(p_expectedHistorySpan[0..^1], out int nMonths))
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
        string? sqUserDataStr = m_redisDb.StringGet("sq_user");
        return GetUsers(sqUserDataStr);
    }

    public static User[] GetUsers(string? p_sqUserDataStr)
    {
        if (p_sqUserDataStr == null)
            return Array.Empty<User>();
        var usersInDb = JsonSerializer.Deserialize<List<UserInDb>>(p_sqUserDataStr, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new SqException($"Deserialize failed on '{p_sqUserDataStr}'");
        var users = new List<User>(usersInDb.Count);
        foreach (var usrDb in usersInDb)
        {
            users.Add(new User(usrDb));
        }
        // after all users are created, process visibleUsers list.
        var visibleUsers = new List<User>[usersInDb.Count];
        for (int i = 0; i < users.Count; i++)
        {
            visibleUsers[i] = new List<User>();
            string visibleUsersStrt = usersInDb[i].Visibleusers;    // reach back to the usersInDb list
            if (String.IsNullOrEmpty(visibleUsersStrt))
                continue;
            string[] usernames = visibleUsersStrt.Split(',', StringSplitOptions.RemoveEmptyEntries);
            foreach (var username in usernames)
            {
                User? user = users.Find(r => r.Username == username);
                if (user == null)
                    continue;
                visibleUsers[i].Add(user);
            }
        }

        for (int i = 0; i < users.Count; i++)
        {
            users[i].VisibleUsers = visibleUsers[i].ToArray();
        }

        return users.ToArray();
    }

    public Dictionary<string, List<Split>> GetMissingYfSplits()
    {
        string? missingYfSplitsJson = m_redisDb.HashGet("memDb", "Hist.Splits.MissingYF");
        if (missingYfSplitsJson == null)
            return new Dictionary<string, List<Split>>();
        Dictionary<string, List<Split>>? potentialMissingYfSplits = JsonSerializer.Deserialize<Dictionary<string, List<Split>>>(missingYfSplitsJson) // JsonSerializer: Dictionary key <int>,<uint> is not supported
            ?? throw new SqException($"Deserialize failed on '{missingYfSplitsJson}'");
        return potentialMissingYfSplits;
    }

    public string? GetAssetQuoteRaw(AssetId32Bits p_assetId)
    {
        string redisKey = p_assetId.ToString() + ".brotli"; // // key: "9:1.brotli"
        byte[]? dailyNavBrotli = m_redisDb.HashGet("assetQuoteRaw", redisKey);
        if (dailyNavBrotli == null)
            return null;
        var dailyNavStr = Utils.BrotliBin2Str(dailyNavBrotli);  // "D/C" for Date/Closes: "D/C,20090102/16461,20090105/16827,..."
        return dailyNavStr;
    }

    public void SetAssetQuoteRaw(AssetId32Bits p_assetId, string p_dailyNavStr)
    {
        string redisKey = p_assetId.ToString() + ".brotli"; // // key: "9:1.brotli"
        var outputCsvBrotli = Utils.Str2BrotliBin(p_dailyNavStr);
        m_redisDb.HashSet("assetQuoteRaw", redisKey, RedisValue.CreateFrom(new System.IO.MemoryStream(outputCsvBrotli)));
    }

    public KeyValuePair<SqDateOnly, double>[] GetAssetBrokerNavDeposit(AssetId32Bits p_assetId)
    {
        string redisKey = p_assetId.ToString() + ".brotli"; // key: "9:1.brotli"
        byte[]? dailyDepositBrotli = m_redisDb.HashGet("assetBrokerNavDeposit", redisKey);
        string? dailyDepositStr = Utils.BrotliBin2Str(dailyDepositBrotli);  // 479 byte text data from 179 byte brotli data, starts with FormatString: "20090310/1903,20100305/2043,..."
        if (dailyDepositStr == null)
            return Array.Empty<KeyValuePair<SqDateOnly, double>>();
        KeyValuePair<SqDateOnly, double>[] deposits = dailyDepositStr.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(r =>
        {
            // format: "20200323/-1000000"
            var depositsDays = r.Split('/', StringSplitOptions.RemoveEmptyEntries);
            DateTime date = Utils.FastParseYYYYMMDD(new StringSegment(r, 0, 8));
            double deposit = Double.Parse(new StringSegment(r, 9, r.Length - 9));
            return new KeyValuePair<SqDateOnly, double>(new SqDateOnly(date), deposit);
        }).ToArray();
        return deposits;
    }

    private static Dictionary<int, PortfolioFolder> GetPortfolioFolders(HashEntry[]? portfolioFoldersRds, User[] users)
    {
        Dictionary<int, PortfolioFolder> result = new();
        if (portfolioFoldersRds == null)
            return result;
        for (int i = 0; i < portfolioFoldersRds.Length; i++)
        {
            HashEntry hashRow = portfolioFoldersRds[i];
            string? rowValue = hashRow.Value;
            if (!hashRow.Name.TryParse(out int id) || rowValue == null) // Name is the 'Key' that contains the Id
                continue;   // Sometimes, there is an extra line 'New field'. But it can be deleted from Redis Manager. It is a kind of expected.

            PortfolioFolderInDb prtfFolderInDb = JsonSerializer.Deserialize<PortfolioFolderInDb>(rowValue, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                ?? throw new SqException($"Deserialize failed on '{rowValue}'");
            PortfolioFolder pf = new(id, prtfFolderInDb, users); // PortfolioFolder.Id is not in the JSON, which is the HashEntry.Value. It comes separately from the HashEntry.Key
            result[id] = pf;
        }
        return result;
    }
    internal void InsertPortfolioFolder(PortfolioFolder fld)
    {
        string redisKey = fld.Id.ToString();
        PortfolioFolderInDb prtfFolderInDb = new(fld);
        string redisValue = JsonSerializer.Serialize<PortfolioFolderInDb>(prtfFolderInDb);
        m_redisDb.HashSet("portfolioFolder", redisKey, redisValue);
    }

    internal string DeletePortfolioFolder(int fldId)
    {
        string redisKey = fldId.ToString();

        // Before deleting the Folder, we should check whether it has any Portolio or Folder child. Only Empty folders can be deleted.
        int childFolderId = -1;
        HashEntry[]? portfolioFoldersRds = m_redisDb.HashGetAll("portfolioFolder");
        if (portfolioFoldersRds == null)
            return "Error in DeletePortfolioFolder(): Redis DB is not available";
        for (int i = 0; i < portfolioFoldersRds.Length; i++)
        {
            HashEntry hashRow = portfolioFoldersRds[i];
            string? rowValue = hashRow.Value;
            if (!hashRow.Name.TryParse(out int id) || rowValue == null) // Name is the 'Key' that contains the Id
                continue;   // Sometimes, there is an extra line 'New field'. But it can be deleted from Redis Manager. It is a kind of expected.

            var prtfFolderInDb = JsonSerializer.Deserialize<PortfolioFolderInDb>(rowValue, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (prtfFolderInDb == null)
                return $"Error in DeletePortfolioFolder(): Deserialize failed on '{rowValue}'";

            if (prtfFolderInDb.ParentFolderId == fldId)
            {
                childFolderId = id;
                break;
            }
        }
        if (childFolderId != -1)
            return $"Error in DeletePortfolioFolder(): The Folder {fldId} has child folder '{childFolderId}'";

        int childPortfolioId = -1;
        HashEntry[]? portfoliosRds = m_redisDb.HashGetAll("portfolio");
        if (portfoliosRds == null)
            return "Error in DeletePortfolioFolder(): Redis DB is not available";
        for (int i = 0; i < portfoliosRds.Length; i++)
        {
            HashEntry hashRow = portfoliosRds[i];
            string? rowValue = hashRow.Value;
            if (!hashRow.Name.TryParse(out int id) || rowValue == null) // Name is the 'Key' that contains the Id
                continue;   // Sometimes, there is an extra line 'New field'. But it can be deleted from Redis Manager. It is a kind of expected.

            var prtfInDb = JsonSerializer.Deserialize<PortfolioInDb>(rowValue, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (prtfInDb == null)
                return $"Error in DeletePortfolioFolder(): Deserialize failed on '{rowValue}'";

            if (prtfInDb.ParentFolderId == fldId)
            {
                childPortfolioId = id;
                break;
            }
        }
        if (childPortfolioId != -1)
            return $"Error in DeletePortfolioFolder(): The Folder {fldId} has child portfolio '{childPortfolioId}'";

        m_redisDb.HashDelete("portfolioFolder", redisKey); // remove the folder based on the folder key
        return string.Empty;
    }
    internal string DeletePortfolio(int p_id)
    {
        string redisKey = p_id.ToString();
        m_redisDb.HashDelete("portfolio", redisKey);
        return string.Empty;
    }
    private static Dictionary<int, Portfolio> GetPortfolios(HashEntry[] portfoliosRds, User[] users, List<Asset> assets) // Portfolio will require Assets in the future
    {
        _ = assets; // StyleCop SA1313 ParameterNamesMustBeginWithLowerCaseLetter. They won't fix. Recommended solution for unused parameters, instead of the discard (_1) parameters

        Dictionary<int, Portfolio> result = new();
        if (portfoliosRds == null)
            return result;
        for (int i = 0; i < portfoliosRds.Length; i++)
        {
            HashEntry hashRow = portfoliosRds[i];
            string? rowValue = hashRow.Value;
            if (!hashRow.Name.TryParse(out int id) || rowValue == null) // Name is the 'Key' that contains the Id
                continue;   // Sometimes, there is an extra line 'New field'. But it can be deleted from Redis Manager. It is a kind of expected.

            var portfInDb = JsonSerializer.Deserialize<PortfolioInDb>(rowValue, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                ?? throw new SqException($"Deserialize failed on '{rowValue}'");
            Portfolio portfolio = new(id, portfInDb, users); // Portfolio.Id is not in the JSON, which is the HashEntry.Value. It comes separately from the HashEntry.Key
            result[id] = portfolio;
        }
        return result;
    }

    internal void InsertPortfolio(Portfolio prtf)
    {
        string redisKey = prtf.Id.ToString();
        PortfolioInDb prtfInDb = new(prtf);
        string redisValue = JsonSerializer.Serialize<PortfolioInDb>(prtfInDb);
        m_redisDb.HashSet("portfolio", redisKey, redisValue);
    }

    internal string UpdatePortfolioFolder(int p_id, User? p_user, string p_name, int p_parentFldId, string p_note)
    {
        string redisKey = p_id.ToString();
        string? prtfFolderInDb = m_redisDb.HashGet("portfolioFolder", redisKey);
        if (prtfFolderInDb == null)
            return $"Error in UpdatePortfolioFolder(): folder id '{redisKey}' doesnt exists";

        PortfolioFolderInDb? prtfFolderInDbCandidate = JsonSerializer.Deserialize<PortfolioFolderInDb>(prtfFolderInDb, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        if (prtfFolderInDbCandidate == null)
            return $"Error in UpdatePortfolioFolder(): Deserialize failed on '{prtfFolderInDb}'";

        prtfFolderInDbCandidate.UserId = p_user?.Id ?? -1;
        prtfFolderInDbCandidate.Name = p_name;
        prtfFolderInDbCandidate.ParentFolderId = p_parentFldId;
        prtfFolderInDbCandidate.Note = p_note;
        string redisValue = JsonSerializer.Serialize<PortfolioFolderInDb>(prtfFolderInDbCandidate);
        m_redisDb.HashSet("portfolioFolder", redisKey, redisValue);
        return string.Empty;
    }

    internal string UpdatePortfolio(int p_id, User? p_user, string p_name, int p_parentFldId, CurrencyId p_currency, PortfolioType p_type, string p_algorithm, string p_algorithmParam, SharedAccess p_sharedAccess, string p_note, List<User> p_sharedUsersWith, int p_tradeHistoryId)
    {
        string redisKey = p_id.ToString();
        string? pfInDb = m_redisDb.HashGet("portfolio", redisKey);
        if (pfInDb == null)
            return $"Error in UpdatePortfolio(): portfolio id '{redisKey}' doesnt exists";

        PortfolioInDb? pfInDbCandidate = JsonSerializer.Deserialize<PortfolioInDb>(pfInDb, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        if (pfInDbCandidate == null)
            return $"Error in UpdatePortfolio(): Deserialize failed on '{pfInDb}'";

        pfInDbCandidate.UserId = p_user?.Id ?? -1;
        pfInDbCandidate.Name = p_name;
        pfInDbCandidate.ParentFolderId = p_parentFldId;
        pfInDbCandidate.BaseCurrency = p_currency.ToString();
        pfInDbCandidate.Type = p_type.ToString();
        pfInDbCandidate.Algorithm = p_algorithm;
        pfInDbCandidate.AlgorithmParam = p_algorithmParam;
        pfInDbCandidate.SharedAccess = p_sharedAccess.ToString();
        pfInDbCandidate.Note = p_note;
        Utils.Logger.Debug($"shareduserwith{p_sharedUsersWith}"); // need to develop this - Daya
        // pfInDbCandidate.SharedUsersWith = p_sharedUsersWith.ToString();
        pfInDbCandidate.TradeHistoryId = p_tradeHistoryId;
        string redisValue = JsonSerializer.Serialize<PortfolioInDb>(pfInDbCandidate);
        m_redisDb.HashSet("portfolio", redisKey, redisValue);
        return string.Empty;
    }

    // Benchmark runs for GetPortfolioTradeHistory(). On Linux server with local Redis-sever. First run: 10ms, consecutive runs: 0.5ms
    public IEnumerable<Trade> GetPortfolioTradeHistory(int p_tradeHistoryId, DateTime? p_startIncLoc, DateTime? p_endIncLoc) // Slim version of returning 5,000 trades one by one with IEnumerable
    {
        string redisKey = p_tradeHistoryId.ToString();
        string? portfTradeHistInDbStr = m_redisDb.HashGet("portfolioTradeHistory", redisKey); // portfTradeHistInDbStr: allocates RAM (e.g. 0.5MB) for all (5,000) trades 1x. We cannot make it slimmer, because RedisDb doesn't support enumeration.
        if (portfTradeHistInDbStr == null)
        {
            Utils.Logger.Error($"Error in GetPortfolioTradeHistory(): portfolio id '{p_tradeHistoryId}");
            yield break; // exits the iterator
        }

        IEnumerable<TradeInDb> tradeInDbs = JsonSerializer.Deserialize<IEnumerable<TradeInDb>>(portfTradeHistInDbStr, new JsonSerializerOptions { PropertyNameCaseInsensitive = false }) // Case sensitive is the fastest.
                ?? throw new SqException($"Deserialize failed on '{portfTradeHistInDbStr}'");
        foreach (TradeInDb tradeInDb in tradeInDbs)
        {
            Trade trade = new(tradeInDb);
            yield return trade;
        }
        // return Enumerable.Empty<Trade>();
    }

    // The IEnumerable<> version is faster as it allocates less memory.
    // However, if you iterate over it many times, or you need the Count, or you search a specific Trade.Date in it with binary search, the user of this will need a List<>. So, use this 90% of the time.
    public List<Trade> GetPortfolioTradeHistoryToList(int p_tradeHistoryId, DateTime? p_startIncLoc, DateTime? p_endIncLoc) // Fat version of returning 5,000 trades (e.g. 0.5MB) in a List
    {
        string redisKey = p_tradeHistoryId.ToString();
        string? portfTradeHistInDbStr = m_redisDb.HashGet("portfolioTradeHistory", redisKey); // portfTradeHistInDbStr: allocates RAM (e.g. 0.5MB) for all (5,000) trades 1x
        if (portfTradeHistInDbStr == null)
        {
            Utils.Logger.Error($"Error in GetPortfolioTradeHistory(): portfolio id '{p_tradeHistoryId}");
            return new(); // returns an empty List
        }

        TradeInDb[] tradeInDbs = JsonSerializer.Deserialize<TradeInDb[]>(portfTradeHistInDbStr, new JsonSerializerOptions { PropertyNameCaseInsensitive = false }) // Case sensitive is the fastest.
                ?? throw new SqException($"Deserialize failed on '{portfTradeHistInDbStr}'"); // TradeInDb[]: allocates RAM (e.g. 0.5MB) for all (5,000) trades 2x
        List<Trade> trades = new(tradeInDbs.Length); // List<Trade>: allocates RAM (e.g. 0.5MB) for all (5,000) trades 3x
        foreach (TradeInDb tradeInDb in tradeInDbs)
        {
            Trade trade = new(tradeInDb);
            trades.Add(trade);
        }

        return trades;
    }

    // Benchmark runs for WritePortfolioTradeHistory(). On Linux server with local Redis-sever. First run: 29ms, consecutive runs: 0.5ms
    public void WritePortfolioTradeHistory(int tradeHistoryId, List<Trade> tradeList)
    {
        HashEntry[] newTradeInDbs = new HashEntry[] { new(tradeHistoryId, TradeInDb.ToRedisValue(tradeList)) };
        m_redisDb.HashSet("portfolioTradeHistory", newTradeInDbs);
    }

    public void AppendPortfolioTradeHistory(int tradeHistoryId, List<Trade> p_newTradeList)
    {
        List<Trade> existingTrades = GetPortfolioTradeHistoryToList(tradeHistoryId, null, null); // if tradeHistoryId doesn't exist GetPortfolioTradeHistory() throws an exception. But we should do it without Exception.
        if (existingTrades.Count > 0)
        {
            int maxId = -1; // if empty list, newId will be 0, which is OK
            foreach (Trade trade in existingTrades)
            {
                if (maxId < trade.Id)
                    maxId = trade.Id;
            }
            int idOffset = maxId + 1;
            foreach (Trade trade in p_newTradeList)
            {
                trade.Id += idOffset;
                if (trade.ConnectedTrades == null)
                    continue;
                for (int i = 0; i < trade.ConnectedTrades.Count; i++)
                    trade.ConnectedTrades[i] += idOffset;
            }
        }

        existingTrades?.AddRange(p_newTradeList);
        WritePortfolioTradeHistory(tradeHistoryId, existingTrades!);
    }

    public static bool UpdateBrotlisIfNeeded()
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