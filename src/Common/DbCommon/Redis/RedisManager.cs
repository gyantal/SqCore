using System.Collections.Concurrent;
using System.Collections.Generic;
using StackExchange.Redis;

namespace DbCommon;

// https://redis.io/clients#c
// https://stackoverflow.com/questions/33103441/difference-between-stackexchange-redis-and-servicestack-redis ServiceStack is fee based, so the free version has limitations
// https://stackexchange.github.io/StackExchange.Redis/  free, under MIT licence, and it is the engine under StackOverflow (very high performance needs), so it is fast. Use that.
// https://github.com/thepirat000/CachingFramework.Redis built on  StackExchange.Redis, extra features: tagging, serialization on a cluster of Redis servers (not needed now)

// Unlike Sql, ConnectionMultiplexer.Connect() is not caching connections, but rebuilt it again every time which takes 90ms. We have to handle connection caching.
// Because Connect takes 80ms, in an app, we have to keep it somewhere globally, so we don't Connect to Redis every time.
// In a big App, we can keep a couple of this connections open if we want. That maybe good, because it can be latency bound, so better to keep more connections open.

// Proper Lazy initialization: https://gigi.nullneuron.net/gigilabs/setting-up-a-connection-with-stackexchange-redis/

public static partial class RedisManager
{
    // sometimes we keep open connections to different Redis servers (local vs. server1 vs. server2). We need support for multiple connections.
    private static readonly ConcurrentDictionary<string, ConnectionMultiplexer> m_conns = new();

    // "The point of ConnectionMultiplexer is to have just one ConnectionMultiplexer which is shared between all requests (which is why you want a static singleton);
    // that’s how it pipelines things to make them really efficient. If the connection dies you want it to automatically recover."
    // 1 ConnectionMultiplexer in general handles 2 physical connections and distribute the load between them.
    private static ConnectionMultiplexer? m_defaultMultiConns = null;

    public static ConnectionMultiplexer? DefMultiConns
    {
        get
        {
            return m_defaultMultiConns;
        }
    }

    // we don't neet to cache GetDatabase() calls.
    // https://stackoverflow.com/questions/25591845/the-correct-way-of-using-stackexchange-redis
    // "Is there a performance impact by using a static IDatabase object vs calling GetDatabase every time you need ?"
    // "for db 0-15 and without an async state, in recent builds: not at all. (note: .GetDatabase() is db 0 without an async-state, so: no overhead)."
    public static IDatabase? DefDb
    {
        get
        {
            return DefMultiConns?.GetDatabase();
        }
    }

    public static void SetDefMultiConns(string p_connStr)
    {
        m_defaultMultiConns = ConnectionMultiplexer.Connect(p_connStr);
    }

    public static ConnectionMultiplexer GetConnection(string p_connStr)
    {
        ConnectionMultiplexer? conn = m_conns.GetValueOrDefault(p_connStr);
        if (conn != null)
            return conn;

        // var configOptions = new ConfigurationOptions();
        // ConnectionMultiplexer redis = ConnectionMultiplexer.Connect("server1:6379,server2:6379");  // a possible a master/slave setup (connection with many servers), but we don't do that.
        ConnectionMultiplexer redis = ConnectionMultiplexer.Connect(p_connStr);
        m_conns[p_connStr] = redis;
        return redis;
    }

    public static IDatabase GetDb(string p_connStr, int p_dbNum)
    {
        ConnectionMultiplexer conn = GetConnection(p_connStr);
        return conn.GetDatabase(p_dbNum);
    }
}