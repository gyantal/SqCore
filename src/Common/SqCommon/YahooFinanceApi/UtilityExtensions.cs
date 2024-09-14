using System;
using System.Collections.Generic;

namespace YahooFinanceApi;

public static class UtilityExtensions
{
    public static TValue? GetValueOrNull<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, TKey key)
    where TValue : class
    {
        return dictionary.TryGetValue(key, out TValue? value) ? value : null;
    }

    public static TValue? GetValueOrNull<TKey, TValue>(this IDictionary<TKey, TValue?> dictionary, TKey key)
        where TValue : struct
    {
        return dictionary.TryGetValue(key, out TValue? value) ? value : null;
    }

    public static TValue GetValueOrDefault<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, TKey key)
    {
        return dictionary.TryGetValue(key, out TValue? value) ? value! : default!; // default can be null
    }
}