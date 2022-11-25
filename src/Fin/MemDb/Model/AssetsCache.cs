using System;
using System.Collections.Generic;
using System.Linq;
using SqCommon;

namespace FinTechCommon;

public class AssetsCache // the whole asset data should be hidden behind a single pointer, so the whole structure can be updated in an atomic operation in a multithread environment
{
    // MemDb should mirror persistent data in RedisDb. For Trades in Portfolios. The AssetId in MemDb should be the same AssetId as in Redis.
    // Alphabetical order of tickers for faster search is not realistic without Index tables or Hashtable/Dictionary.
    // There are ticker renames every other day, and we will not reorganize the whole Assets table in Redis just because there were ticker renames in real life.
    // In Redis, AssetId will be permanent. Starting from 1...increasing by 1. Redis 'tables' will be ordered by AssetId, because of faster JOIN operations.
    // The top bits of AssetId is the Type=Stock, Options, so there can be gaps in the AssetId ordered list. But at least, we can aim to order this array by AssetId. (as in Redis)
    public List<Asset> Assets { get; set; } = new List<Asset>();    // it is loaded from RedisDb by filtering 'memDb.allAssets' by 'memDb.SqCoreWebAssets'

    readonly uint m_stocksSubTableIdMax = 0;   // for generating new AssetID for NonPersistent Stocks, Options that might come from IB-TWS
    readonly uint m_optionsSubTableIdMax = 0;  // for generating new AssetID for NonPersistent Stocks, Options that might come from IB-TWS
    readonly uint m_navSubTableIdMax = 0;    // for generating new AssetID for Aggregated BrokerNav assets

    public uint NextUnusedOptionsSubTableId = 0;

    // O(1) search for Dictionaries. Tradeoff between CPU-usage and RAM-usage. Use excess memory in order to search as fast as possible.
    // Another alternative is to implement a virtual ordering in an index table: int[] m_idxByTicker (used by Sql DBs), but that also uses RAM and the access would be only O(logN). Hashtable uses more memory, but it is faster.
    // BinarySearch is a good idea for 10,000 Dates in time series, but not for this, when we have a small number of discrete values of AssetID or Tickers
    public Dictionary<AssetId32Bits, Asset> AssetsByAssetID = new();  // Assets are ordered by AssetId, so BinarySearch could be used, but Hashtable is faster.
    public Dictionary<string, Asset> AssetsBySqTicker = new();

    // AssetID uint is unique identifier for fast access.
    // SqTicker is also a unique identifier. Not too fast, because a hashcode is generated from string. But it is human readable.
    // Symbol is not unique, so many assets can fall into the same category. ILookup allows that. We keep this field here for future possibility

    // Dictionary<Key, List<Value>> vs. a Lookup<Key, Value> ; The main difference is a Lookup is immutable: it has no Add() methods and no public constructor
    // If you are trying to get a structure as efficient as a Dictionary but you dont know for sure there is no duplicate key in input, Lookup is safer.
    // It also supports null keys, and returns always a valid result, so it appears as more resilient to unknown input (less prone than Dictionary to raise exceptions).
    // https://stackoverflow.com/questions/13362490/difference-between-lookup-and-dictionaryof-list
    public ILookup<string, Asset> AssetsBySymbol = Enumerable.Empty<Asset>().ToLookup(x => default(string)!); // LSE:"VOD", NYSE:"VOD" both can be in database  // Lookup doesn't have default constructor.

    public AssetsCache()
    {
    }
    public AssetsCache(List<Asset> p_assets)
    {
        Assets = p_assets;    // replace AssetsCache in one atomic operation by changing the pointer, so no inconsistency
        AssetsByAssetID = p_assets.ToDictionary(r => r.AssetId);
        AssetsBySqTicker = p_assets.ToDictionary(r => r.SqTicker);
        AssetsBySymbol = p_assets.ToLookup(r => r.Symbol); // if it contains duplicates, ToLookup() allows for multiple values per key.

        // actualize m_stocksSubTableIdMax, m_navSubTableIdMax, m_optionsSubTableIdMax
        for (int i = 0; i < Assets.Count; i++) // Fast code for about 2-3000 assets
        {
            var asset = Assets[i];
            var assetTypeId = asset.AssetId.AssetTypeID;
            var subTableId = asset.AssetId.SubTableID;
            if (assetTypeId == AssetType.Stock && subTableId > m_stocksSubTableIdMax)
                m_stocksSubTableIdMax = subTableId;
            if (assetTypeId == AssetType.Option && subTableId > m_optionsSubTableIdMax)
                m_optionsSubTableIdMax = subTableId;
            if (assetTypeId == AssetType.BrokerNAV && subTableId > m_navSubTableIdMax)
                m_navSubTableIdMax = subTableId;
        }

        NextUnusedOptionsSubTableId = m_optionsSubTableIdMax + 1;
    }

    internal AssetId32Bits GenerateUniqueAssetId(Asset newAsset)
    {
        if (newAsset is Option)
            return new AssetId32Bits(AssetType.Option, NextUnusedOptionsSubTableId++);

        throw new NotImplementedException();    // at the moment, we only create new Options run-time
    }

    public static void AddAsset(Asset asset)
    {
        _ = asset; // StyleCop SA1313 ParameterNamesMustBeginWithLowerCaseLetter. They won't fix. Recommended solution for unused parameters, instead of the discard (_1) parameters
        throw new SqException("MemData.AssetsCache Readers will have inconsistent for(), foreach() enumerations. Use MemData.AddToAssetCacheIfMissing() instead.");
        // Assets.Add(p_asset);
        // AssetsByAssetID = Assets.ToDictionary(r => r.AssetId);
        // AssetsBySqTicker = Assets.ToDictionary(r => r.SqTicker);
        // AssetsBySymbol = Assets.ToLookup(r => r.Symbol); // if it contains duplicates, ToLookup() allows for multiple values per key.
    }

    public Asset GetAsset(uint p_assetID)
    {
        if (AssetsByAssetID.TryGetValue(p_assetID, out Asset? value))
            return value;
        throw new Exception($"AssetID '{p_assetID}' is missing from MemDb.Assets.");
    }

    public Asset GetAsset(string p_sqTicker) // if it is required that asset is found, then we throw exception as error
    {
        if (AssetsBySqTicker.TryGetValue(p_sqTicker, out Asset? value))
            return value;
        throw new Exception($"SqTicker '{p_sqTicker}' is missing from MemDb.Assets.");
    }

    public Asset? TryGetAsset(string p_sqTicker) // sometimes, it is expected that asset is not found. Just return null.
    {
        if (AssetsBySqTicker.TryGetValue(p_sqTicker, out Asset? value))
            return value;
        return null;
    }

    // Also can be historical using Assets.TickerChanges
    public Stock[] GetAllMatchingStocksBySymbol(string p_symbol, ExchangeId p_primExchangeID = ExchangeId.Unknown, DateTime? p_timeUtc = null)
    {
        return Assets.GetAllMatchingStocksBySymbol(p_symbol, p_primExchangeID, p_timeUtc).ToArray();
    }

    // Although Symbols are not unique (only AssetId), most of the time clients access data by LastSymbol.
    // It is not good for historical backtests, because it uses only the last Symbol, not historical tickers, but it is enough 95% of the time for clients.
    // This can find both "VOD" (Vodafone) ticker in LSE (in GBP), or in NYSE (in USD).
    public Stock? GetFirstMatchingStockBySymbol(string p_symbol, ExchangeId p_primExchangeID = ExchangeId.Unknown, bool p_raiseExceptionNotFound = true)
    {
        Stock? stock = Assets.GetFirstMatchingStockBySymbol(p_symbol, p_primExchangeID);
        if (stock == null && p_raiseExceptionNotFound)
            throw new Exception($"MemDb.GetFirstMatchingStockBySymbol(): Symbol '{p_symbol}' with Exchange '{p_primExchangeID}' is not found.");
        return stock;
    }
}