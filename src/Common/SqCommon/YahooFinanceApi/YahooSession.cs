using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Flurl.Http;
using SqCommon;

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

    // public const string UserAgentValue = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/122.0.0.0 Safari/537.36 Edg/122.0.0.0";  // 2025-05-06: stopped working, giving "429 Too Many Requests"
    public const string UserAgentValue = "Mozilla/5.0";

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
            int maxRetryNum = 5;
            int nDownload = 0;
            do
            {
                nDownload++;
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
                    Utils.Logger.Error($"YahooSession: Failed to obtain Yahoo auth cookie.");
                }
                else
                {
                    Utils.Logger.Info($"YF. A3 Cookie is obtained.");
                    _crumb = await "https://query1.finance.yahoo.com/v1/test/getcrumb"
                        .AllowHttpStatus("401") // YF returns status code 401 (Unauthorized) sporadically. Exception is annoying, so allow it, but retry.
                        .WithCookie(_cookie.Name, _cookie.Value)
                        .WithHeader(UserAgentKey, UserAgentValue) // Fixed too many requests error caused by missing user agent header, https://github.com/karlwancl/YahooFinanceApi/commit/29431526c9f20e9655f6e4a857fc8798c8d8508d
                        .GetAsync(token)
                        .ReceiveString();

                    if (!string.IsNullOrEmpty(_crumb) && _crumb.IndexOf("Unauthorized") == -1) // getcrumb returns sometimes: "{\"finance\":{\"result\":null,\"error\":{\"code\":\"Unauthorized\",\"description\":\"Invalid Cookie\"}}}"
                    {
                        Console.WriteLine($"Retrieved Yahoo crumb: {_crumb}");
                        Utils.Logger.Info($"Retrieved Yahoo crumb: {_crumb}");
                        break; // we have the crumb and it is not "Unauthorized". Good. Exit the loop
                    }
                    else
                    {
                        Utils.Logger.Error($"YahooSession. Failed to retrieve Yahoo crumb. Try again.");
                        Thread.Sleep(200);
                    }
                }
            }
            while (nDownload < maxRetryNum);
        }
        finally
        {
            _semaphore.Release();
        }

        if (string.IsNullOrEmpty(_crumb))
            Console.WriteLine("Failed to retrieve Yahoo crumb.");
    }
}