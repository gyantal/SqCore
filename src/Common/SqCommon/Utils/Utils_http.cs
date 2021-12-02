using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace SqCommon
{
    public static partial class Utils
    {
        static HttpClientHandler g_httpHandler = new HttpClientHandler()  // without this, returned stream is the raw binary if compressed
        {
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate   // otherwise if content is gzipped, we got binary in GetString()
        };

        // HttpWebRequest vs. HttpClient. Never use HttpWebRequest. HttpClient is preferred over HttpWebRequest. https://www.diogonunes.com/blog/webclient-vs-httpclient-vs-httpwebrequest/
        static HttpClient g_httpClient = new HttpClient(g_httpHandler); // for efficiency, we can make it global, because it can handle multiple queries in multithread

        public static async Task<string?> DownloadStringWithRetryAsync(string p_url)
        {
            return await DownloadStringWithRetryAsync(p_url, 3, TimeSpan.FromSeconds(2), true);
        }

        public static async Task<string?> DownloadStringWithRetryAsync(string p_url, int p_nRetry, TimeSpan p_sleepBetweenRetries, bool p_throwExceptionIfUnsuccesful = true)
        {
            string webpage = string.Empty;
            int nDownload = 0;
            var request = CreateRequest(p_url);

            do
            {
                try
                {
                    nDownload++;
                    var response = await g_httpClient.SendAsync(request);
                    using (HttpContent content = response.Content)
                    {
                        webpage = await content.ReadAsStringAsync();
                    }

                    // httpClient.DefaultRequestHeaders.AcceptEncoding.ParseAdd("gzip, deflate, br");
                    // httpClient.DefaultRequestHeaders.Connection.ParseAdd("keep-alive"); // for https://www.nasdaq.com/api keep-alive is needed, otherwise Timeout
                    // p_webpage = await httpClient.GetStringAsync(p_url);

                    Utils.Logger.Debug(String.Format("DownloadStringWithRetry() OK:{0}, nDownload-{1}, Length of reply:{2}", p_url, nDownload, webpage.Length));
                    return webpage;
                }
                catch (Exception ex)
                {
                    // it is quite expected that sometimes (once per month), there is a problem:
                    // "The operation has timed out " or "Unable to connect to the remote server" exceptions
                    // Don't raise Logger.Error() after the first attempt, because it is not really Exceptional, and an Error email will be sent
                    Utils.Logger.Info(ex, "Exception in DownloadStringWithRetry()" + p_url + ":" + nDownload + ": " + ex.Message);
                    if (ex is AggregateException)
                    {
                        foreach (var errInner in ((AggregateException)ex).InnerExceptions)
                        {
                            Utils.Logger.Info(ex, "Exception in DownloadStringWithRetry()" + p_url + ":" + nDownload + ": " + ex.Message);
                        }
                    }
                    Thread.Sleep(p_sleepBetweenRetries);
                    if ((nDownload >= p_nRetry) && p_throwExceptionIfUnsuccesful)
                        throw;  // if exception still persist after many tries, rethrow it to caller

                    // we reuse the same g_httpClient, however, request cannot be reused, because then using the same request again. "System.InvalidOperationException: The request message was already sent. Cannot send the same request message multiple times."
                    request = CreateRequest(p_url);
                }
            } while (nDownload < p_nRetry);

            return null;
        }

        // Typical usage, and queries so far. p_url = ?
        // MemDb:
        // Yahoo historical data is handled by CsvReader in YahooFinanceApi
        // "https://api.nasdaq.com/api/calendar/splits?date=2021-02-24"
        // ContangoVisualizer:
        // "https://www.cmegroup.com/CmeWS/mvc/Quotes/Future/425/G"
        // "https://www.cmegroup.com/CmeWS/mvc/ProductCalendar/Future/425"
        // "https://www.cmegroup.com/CmeWS/mvc/Quotes/Future/444/G"
        // "https://www.cmegroup.com/CmeWS/mvc/ProductCalendar/Future/444"
        // QuickfolioNews:
        // Yahoo RSS is handled in their code.
        // "https://sheets.googleapis.com/v4/spreadsheets/1c5ER22sXDEVzW3uKthclpArlZvYuZd6xUffXhs6rRsM/values/A1%3AA1?key=..."
        // "https://www.benzinga.com/stock/AMZN"
        // "https://www.tipranks.com/api/stocks/getNews/?ticker=AMZN"
        public static HttpRequestMessage CreateRequest(string p_url)
        {
            // var headers =  new HttpRequestHeaders { { "accept-encoding", "gzip, deflate, br" } };
            // This doesn't compile:  "'HttpRequestHeaders' does not contain a constructor that takes 0 arguments"
            // creating a HttpRequestHeaders object by user code is not allowed by design.
            // Dictionary initialization would work if there is an empty constructor + Add(string key, string value) + GetEnumerator()
            // https://stackoverflow.com/questions/11694910/how-do-you-use-object-initializers-for-a-list-of-key-value-pairs/11695018
            // It is not Array initialization, it is Dictionary initialization, but it is not allowed for HttpRequestHeaders by design. Only HttpRequestMessage can create an empty Header.

            if (p_url.StartsWith("https://api.nasdaq.com"))
                return new HttpRequestMessage
                {
                    RequestUri = new Uri(p_url),
                    Method = HttpMethod.Get,
                    Version = HttpVersion.Version20,    // This was the key on Linux!!! The default is Version11 (Silly: "In .NET Core 3.0+, the default value was reverted back from 2.0 to 1.1.")
                    Headers = {
                            { "accept", "application/json, text/plain, */*" }, // needed on Linux for api.nasdaq.com , otherwise it returns with 'no permission'.
                            { "user-agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/88.0.4324.182 Safari/537.36" },
                            { "accept-encoding", "gzip, deflate, br" },
                            { "accept-language", "en-US,en;q=0.9" }
                        }
                };
            else if (p_url.StartsWith("https://www.cmegroup.com"))
            // had to copy All headers from Linux Chrome + setting up Http1.1 to make it work with curl (wget is only http1.0, use curl)
            // there is no need for cookies (yet)
            // curl -v --http1.1 -H 'Host: www.cmegroup.com' -H 'Connection: keep-alive' -H 'Cache-Control: max-age=0' -H 'Upgrade-Insecure-Requests: 1' -H 'User-Agent: Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/88.0.4324.96 Safari/537.36' -H 'Accept: text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3;q=0.9' -H 'Sec-Fetch-Site: none' -H 'Sec-Fetch-Mode: navigate' -H 'Sec-Fetch-User: ?1' -H 'Sec-Fetch-Dest: document' -H 'Accept-Encoding: gzip, deflate, br' -H 'Accept-Language: en-US,en;q=0.9' --output cme444brotli.br  https://www.cmegroup.com/CmeWS/mvc/Quotes/Future/444/G
                return new HttpRequestMessage
                {
                    RequestUri = new Uri(p_url),
                    Method = HttpMethod.Get,
                    Version = HttpVersion.Version11,    // Changed from Version20 to 11, but it didn't help on Linux
                    Headers = {
                            { "Host", "www.cmegroup.com" }, // copied from Linux Chrome, worked in curl.
                            { "Connection", "keep-alive" },
                            { "Cache-Control", "max-age=0" },
                            { "Upgrade-Insecure-Requests", "1" },
                            { "User-Agent", "Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/88.0.4324.96 Safari/537.36" },
                            { "Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3;q=0." },
                            { "Sec-Fetch-Site", "none" },
                            { "Sec-Fetch-Mode", "navigate" },
                            { "Sec-Fetch-User", "?1" },
                            { "Sec-Fetch-Dest", "document" },
                            { "Accept-Encoding", "gzip, deflate, br" },
                            { "Accept-Language", "en-US,en;q=0.9" }
                        }
                };
            else if (p_url.StartsWith("http://vixcentral.com/ajax_update"))
            // curl -i -H "Accept: application/json" -H "Accept: text/javascript" -H "Accept: */*" -H "Accept-encoding: gzip deflate" -e "http://vixcentral.com/" -H "Host: vixcentral.com" -H "Connection: keep-alive" -H "X-Requested-With: XMLHttpRequest" "http://vixcentral.com/ajax_update"
                return new HttpRequestMessage
                {
                    RequestUri = new Uri(p_url),
                    Method = HttpMethod.Get,
                    Version = HttpVersion.Version20,    // This was the key on Linux!!! The default is Version11 (Silly: "In .NET Core 3.0+, the default value was reverted back from 2.0 to 1.1.")
                    Headers = {
                            { "accept", "application/json, text/javascript, */*" }, // needed on Linux for api.nasdaq.com , otherwise it returns with 'no permission'.
                            { "accept-encoding", "gzip, deflate" },
                            { "Host", "vixcentral.com" },
                            { "Connection", "keep-alive" },
                            { "X-Requested-With","XMLHttpRequest"}
                        }
                };
            else if (p_url.StartsWith("https://www.cnbc.com/id/100003114/device/rss/rss.html"))
                return new HttpRequestMessage
                {
                    RequestUri = new Uri(p_url),
                    Method = HttpMethod.Get,
                    Headers = {
                            { "user-agent", "Mozilla/4.0 (compatible; MSIE 6.0; Windows NT 5.2; .NET CLR 1.0.3705;)" }
                        }
                };

            return new HttpRequestMessage
            {
                RequestUri = new Uri(p_url),
                Method = HttpMethod.Get,
                Version = HttpVersion.Version20,    // The default is Version11 (Silly: "In .NET Core 3.0+, the default value was reverted back from 2.0 to 1.1.")
            };
        }

        public static async void TestDownloadApiNasdaqCom()
        {
            // 1. Case study for api.nasdaq.com
            // url = $"https://api.nasdaq.com/api/calendar/splits?date=2021-02-24";  // content-type: application/json; charset=utf-8  content-encoding: gzip
            // url = "https://www.nasdaq.com/api/v1/historical/A/stocks/2019-07-19/2020-07-19";  // this is text, no compression. Content-Type: application/csv
            // https://stackoverflow.com/questions/63016136/httpwebresponse-fails-when-trying-to-download-a-csv-file
            // "The response never comes back and it just times out.
            // "That website seems to run forever if you don't pass Accept-Encoding (e.g. gzip, deflate, br) and Connection (e.g. keep-alive) headers

            // 2. Windows: Adding AcceptEncoding and keep-alive worked on Windows, but not on Linux.

            // 3. Linux: this worked in Python:
            // https://stackoverflow.com/questions/66289443/no-response-from-request-get-for-nasdaq-webpage
            // "When adding the headers of the query that appear when inspecting the element in chrome, the request works well in python:"
            // response = requests.get('https://api.nasdaq.com/api/calendar/earnings?date=2021-02-23',headers={"authority":"api.nasdaq.com","scheme":"https","path":"/api/calendar/earnings?date=2021-02-23","pragma":"no-cache","cache-control":"no-cache","accept":"application/json, text/plain, */*","user-agent":"Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/88.0.4324.182 Safari/537.36","origin":"https://www.nasdaq.com","sec-fetch-site":"same-site","sec-fetch-mode":"cors","sec-fetch-dest":"empty","referer":"https://www.nasdaq.com/","accept-encoding":"gzip, deflate, br","accept-language":"en-US,en;q=0.9,es;q=0.8,nl;q=0.7"})
            // print(response.content)
            // but when I ported from Python to C#, it didn't work on Linux.
            // The solution was that Python and Chrome uses Http2.0 protocol, while by default (request.ToString()) uses Version: 1.1. Changing it to Version2 made it work on Linux.
            // It seems that Nasdaq server prefers Http2.0, but because many lame user uses old browsers on Windows, they allow Http1.1 only on Windows.

            // "connection:keep-alive" was necessary on Windows at first, but later it is not. And it is not necessary on Linux. So, we can treat Win/Linx the same way.

            var url = $"https://api.nasdaq.com/api/calendar/splits?date=2021-02-24";  // content-type: application/json; charset=utf-8  content-encoding: gzip
            //var url = "https://www.nasdaq.com/api/v1/historical/A/stocks/2019-07-19/2020-07-19";  // this is text, no compression. Content-Type: application/csv
            var request = new HttpRequestMessage
            {
                RequestUri = new Uri(url),
                Method = HttpMethod.Get,
                Version = HttpVersion.Version20,    // This was the key on Linux!!! The default is Version11 (Silly: "In .NET Core 3.0+, the default value was reverted back to 1.1.")
                Headers = {
                    //{ "authority", "api.nasdaq.com" }, // HERE IS HOW TO ADD HEADERS,
                    //{ "scheme", "https" },
                    //{ "path", "/api.nasdaq.com/api/calendar/splits?date=2021-02-24" },
                    //{ "cache-control", "no-cache" },
                    { "accept", "application/json, text/plain, */*" }, // needed on Linux for api.nasdaq.com , otherwise it returns with 'no permission'.
                    { "user-agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/88.0.4324.182 Safari/537.36" },
                    //{ "origin", "https://www.nasdaq.com" },
                    //{ "sec-fetch-site", "same-site" },
                    //{ "sec-fetch-mode", "cors" },
                    //{ "sec-fetch-dest", "empty" },
                    //{ "referer", "https://www.nasdaq.com/" },
                    { "accept-encoding", "gzip, deflate, br" },
                    { "accept-language", "en-US,en;q=0.9" }
                }
            };

            Console.WriteLine($"HttpRequestMessage:'{request.ToString()}'");

            HttpClient httpClient = new HttpClient(g_httpHandler);
            var response = await httpClient.SendAsync(request);
            using (HttpContent content = response.Content)
            {
                var contentStr = await content.ReadAsStringAsync();
                Console.WriteLine($"TestDownloadApiNasdaqCom Returned: '{contentStr}'");
            }
        }

    }
}