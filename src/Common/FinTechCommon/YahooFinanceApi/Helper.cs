using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;

namespace YahooFinanceApi;

internal static class Helper
{
    private static readonly DateTime Epoch = new(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private static readonly TimeZoneInfo TzEst = TimeZoneInfo
        .GetSystemTimeZones()
        .Single(tz => tz.Id == "Eastern Standard Time" || tz.Id == "America/New_York");

    private static DateTime ToUtcFrom(this DateTime dt, TimeZoneInfo tzi) =>
        TimeZoneInfo.ConvertTimeToUtc(dt, tzi);

    internal static DateTime FromEstToUtc(this DateTime dt) =>
        DateTime.SpecifyKind(dt, DateTimeKind.Unspecified)
            .ToUtcFrom(TzEst);

    internal static string ToUnixTimestamp(this DateTime dt) =>
        DateTime.SpecifyKind(dt, DateTimeKind.Utc)
            .Subtract(Epoch)
            .TotalSeconds
            .ToString("F0");

    internal static string Name<T>(this T @enum)
    {
        string name = @enum?.ToString() ?? string.Empty;
        if (typeof(T).GetMember(name).First().GetCustomAttribute(typeof(EnumMemberAttribute)) is EnumMemberAttribute attr && attr.IsValueSetExplicitly)
            name = attr.Value ?? string.Empty;
        return name;
    }

    internal static string GetRandomString(int length) =>
        Guid.NewGuid().ToString()[..length];

    internal static string ToLowerCamel(this string pascal) =>
        pascal[..1].ToLower() + pascal[1..];

    internal static string ToPascal(this string lowerCamel) =>
        lowerCamel[..1].ToUpper() + lowerCamel[1..];

    internal static IEnumerable<string> Duplicates(this IEnumerable<string> items)
    {
        var hashSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        return items.Where(item => !hashSet.Add(item));
    }

}
