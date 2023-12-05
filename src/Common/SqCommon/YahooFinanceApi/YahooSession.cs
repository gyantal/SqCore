using System;
using System.Threading;
using System.Threading.Tasks;
using Flurl.Http;

namespace YahooFinanceApi
{
    /// <summary>
    /// Holds state for Yahoo HTTP calls
    /// </summary>
    internal static class YahooSession
    {
        private static readonly SemaphoreSlim _semaphore = new(1, 1);

        private static string? _crumb;
        private static FlurlCookie? _cookie;

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

        public static async Task InitAsync(bool reset, CancellationToken token = default)
        {
            if (_crumb != null && !reset)
            {
                return;
            }

            await _semaphore.WaitAsync(token).ConfigureAwait(false);
            try
            {
                const string userAgentKey = "User-Agent";
                const string userAgentValue = "Mozilla/5.0 (Windows NT 6.1; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/58.0.3029.110 Safari/537.36";

                var response = await "https://fc.yahoo.com"
                    .AllowHttpStatus("404")
                    .AllowHttpStatus("502") // 2023-12-05: fc.yahoo.com returns 502, instead 404 (that it returned before). Even though it fails, it gives back the _cookie as "A3", and it can be used to get the crumb. See more: https://stackoverflow.com/questions/76065035/yahoo-finance-v7-api-now-requiring-cookies-python
                    .WithHeader(userAgentKey, userAgentValue)
                    .GetAsync()
                    .ConfigureAwait(false);

                if (response.Cookies.Count == 0)
                {
                    throw new Exception("Failed to obtain Yahoo auth cookie.");
                }
                else
                {
                    _cookie = response.Cookies[0];

                    _crumb = await "https://query1.finance.yahoo.com/v1/test/getcrumb"
                        .WithCookie(_cookie.Name, _cookie.Value)
                        .WithHeader(userAgentKey, userAgentValue) // Fixed too many requests error caused by missing user agent header, https://github.com/karlwancl/YahooFinanceApi/commit/29431526c9f20e9655f6e4a857fc8798c8d8508d
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
}