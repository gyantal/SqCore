using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Flurl.Http;

namespace YahooFinanceApi;

/// <summary>
/// Holds state for Yahoo HTTP calls
/// </summary>
internal static class YahooSession
{
    private static string? _crumb;
    private static FlurlCookie? _cookie;
    private static SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);
    private static Dictionary<string, TimeZoneInfo> timeZoneCache = new Dictionary<string, TimeZoneInfo>();

    public const string UserAgentKey = "User-Agent";

    public const string UserAgentValue = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/122.0.0.0 Safari/537.36 Edg/122.0.0.0";

    public static string? Crumb
    {
        get
        {
            return _crumb;
        }
    }

    public static FlurlCookie? Cookie
    {
        get
        {
            return _cookie;
        }
    }

    public static async Task InitAsync(CancellationToken token = default)
    {
        if (_crumb != null)
        {
            return;
        }

        await _semaphore.WaitAsync(token).ConfigureAwait(false);
        try
        {
            var response = await "https://fc.yahoo.com"
                .AllowHttpStatus("404") // 404 (Not Found) received instantly, but that is expected behaviour as it gives back 1 cookie neeeded for getting the Crumb
                .AllowHttpStatus("500") // 2024-02-22: fc.yahoo.com returns 500.(Internal Server Error), "A generic error message, given when an unexpected condition was encountered and no more specific message is suitable"
                .AllowHttpStatus("502") // 2023-12-05: fc.yahoo.com returns 502 (Connection timed out) received after 20sec, instead 404 (that it returned before). Even though it fails, it gives back the _cookie as "A3", and it can be used to get the crumb. See more: https://stackoverflow.com/questions/76065035/yahoo-finance-v7-api-now-requiring-cookies-python
                .WithHeader(UserAgentKey, UserAgentValue)
                .GetAsync()
                .ConfigureAwait(false);

            _cookie = response.Cookies.FirstOrDefault(c => c.Name == "A3");
            if (_cookie == null)
            {
                throw new Exception("Failed to obtain Yahoo auth cookie.");
            }
            else
            {
                _crumb = await "https://query1.finance.yahoo.com/v1/test/getcrumb"
                    .WithCookie(_cookie.Name, _cookie.Value)
                    .WithHeader(UserAgentKey, UserAgentValue) // Fixed too many requests error caused by missing user agent header, https://github.com/karlwancl/YahooFinanceApi/commit/29431526c9f20e9655f6e4a857fc8798c8d8508d
                    .GetAsync(token)
                    .ReceiveString();

                if (string.IsNullOrEmpty(_crumb))
                {
                    throw new Exception("Failed to retrieve Yahoo crumb.");
                }
            }
        }
        finally
        {
            _semaphore.Release();
        }
    }
}