using System;
using System.Collections.Generic;
using System.Linq;

namespace FinTechCommon
{
public class AssetsCache    // the whole asset data should be hidden behind a single pointer, so the whole structure can be updated in an atomic operation in a multithread environment
    {
        // MemDb should mirror persistent data in RedisDb. For Trades in Portfolios. The AssetId in MemDb should be the same AssetId as in Redis.
        // Alphabetical order of tickers for faster search is not realistic without Index tables or Hashtable/Dictionary.
        // There are ticker renames every other day, and we will not reorganize the whole Assets table in Redis just because there were ticker renames in real life.
        // In Redis, AssetId will be permanent. Starting from 1...increasing by 1. Redis 'tables' will be ordered by AssetId, because of faster JOIN operations.
        // The top bits of AssetId is the Type=Stock, Options, so there can be gaps in the AssetId ordered list. But at least, we can aim to order this array by AssetId. (as in Redis)
        public List<Asset> Assets { get; set; } = new List<Asset>();    // it is loaded from RedisDb by filtering 'memDb.allAssets' by 'memDb.SqCoreWebAssets'

        // O(1) search for Dictionaries. Tradeoff between CPU-usage and RAM-usage. Use excess memory in order to search as fast as possible.
        // Another alternative is to implement a virtual ordering in an index table: int[] m_idxByTicker (used by Sql DBs), but that also uses RAM and the access would be only O(logN). Hashtable uses more memory, but it is faster.
        // BinarySearch is a good idea for 10,000 Dates in time series, but not for this, when we have a small number of discrete values of AssetID or Tickers
        public Dictionary<AssetId32Bits, Asset> AssetsByAssetID = new Dictionary<AssetId32Bits, Asset>();  // Assets are ordered by AssetId, so BinarySearch could be used, but Hashtable is faster.

        // Dictionary<Key, List<Value>> vs. a Lookup<Key, Value> ; The main difference is a Lookup is immutable: it has no Add() methods and no public constructor
        // If you are trying to get a structure as efficient as a Dictionary but you dont know for sure there is no duplicate key in input, Lookup is safer.
        // It also supports null keys, and returns always a valid result, so it appears as more resilient to unknown input (less prone than Dictionary to raise exceptions).
        // https://stackoverflow.com/questions/13362490/difference-between-lookup-and-dictionaryof-list
        public ILookup<string, Asset> AssetsByLastTicker = Enumerable.Empty<Asset>().ToLookup(x => default(string)!); // LSE:"VOD", NYSE:"VOD" both can be in database  // Lookup doesn't have default constructor.

        public static AssetsCache CreateAssetCache(List<Asset> p_assets)
        {
            return new AssetsCache()
            {
                Assets = p_assets,    // replace AssetsCache in one atomic operation by changing the pointer, so no inconsistency
                AssetsByLastTicker = p_assets.ToLookup(r => r.LastTicker), // if it contains duplicates, ToLookup() allows for multiple values per key.
                AssetsByAssetID = p_assets.ToDictionary(r => r.AssetId)
            };
        }

        public void AddAsset(Asset p_asset)
        {
            Assets.Add(p_asset);
            AssetsByLastTicker = Assets.ToLookup(r => r.LastTicker); // if it contains duplicates, ToLookup() allows for multiple values per key.
            AssetsByAssetID = Assets.ToDictionary(r => r.AssetId);
        }

        public Asset GetAsset(uint p_assetID)
        {
            if (AssetsByAssetID.TryGetValue(p_assetID, out Asset value))
                return value;
            throw new Exception($"AssetID '{p_assetID}' is missing from MemDb.Assets.");
        }

        // Also can be historical using Assets.TickerChanges
        public Asset[] GetAllMatchingAssets(string p_ticker, ExchangeId p_primExchangeID =  ExchangeId.Unknown, DateTime? p_timeUtc = null)
        {
            return Assets.GetAllMatchingAssets(p_ticker, p_primExchangeID, p_timeUtc).ToArray();
        }

        // Although Tickers are not unique (only AssetId), most of the time clients access data by LastTicker.
        // It is not good for historical backtests, because it uses only the last ticker, not historical tickers, but it is enough 95% of the time for clients.
        // This can find both "VOD" (Vodafone) ticker in LSE (in GBP), or in NYSE (in USD).
        public Asset? GetFirstMatchingAssetByLastTicker(string p_lasTicker, ExchangeId p_primExchangeID = ExchangeId.Unknown, bool p_raiseExceptionNotFound = true)
        {
            IEnumerable<Asset> assets = AssetsByLastTicker[p_lasTicker];
            if (assets == null)
                throw new Exception($"Ticker '{p_lasTicker}' is missing from MemDb.Assets.");

            foreach (var asset in assets)
            {
                if (p_primExchangeID == ExchangeId.Unknown || p_primExchangeID == asset.PrimaryExchange)
                    return asset;
            }
            if (p_raiseExceptionNotFound)
                throw new Exception($"MemDb.GetFirstMatchingAssetByLastTicker(): Ticker '{p_lasTicker}' with Exchange '{p_primExchangeID}' is not found.");
            
            return null;
        }
    }

}