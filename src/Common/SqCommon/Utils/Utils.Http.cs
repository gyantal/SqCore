using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SqCommon;

public static partial class Utils
{
    static readonly HttpClientHandler g_httpHandler = new() // without this, returned stream is the raw binary if compressed
    {
        AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate // otherwise if content is gzipped, we got binary in GetString()
    };

    // HttpWebRequest vs. HttpClient. Never use HttpWebRequest. HttpClient (asynchronous, can be reused) is preferred over HttpWebRequest. https://www.diogonunes.com/blog/webclient-vs-httpclient-vs-httpwebrequest/
    // Use 1 global HttpClient per App. Because every new ctor and initialization cost about 70msec as base cost. Just use this 1 global one everywhere in the code.
    // HttpClient can handle multiple queries in multithread.
    static HttpClient? g_httpClient = null; // Lazy eval is better for Apps that don't use it at all.

    private static void AssureHttpClientIsAlive()
    {
        if (g_httpClient == null)
        {
            g_httpClient = new(g_httpHandler)
            {
                DefaultRequestVersion = HttpVersion.Version30,
                // DefaultVersionPolicy = HttpVersionPolicy.RequestVersionExact // Defult is 3.0, but not explicitly require HTTP/3 only.
            };
        }
    }

    // Semi-hiding g_httpClient from users for better control. Use the inner g_httpClient only if it is very necessary! E.g. HistPriceApi: when YF API gives UTF8 bytes, and we don't want to convert it to 2-byte strings.
    public static HttpClient GetHttpClientDirect()
    {
        AssureHttpClientIsAlive();
        return g_httpClient!;
    }

    public static async Task<string?> DownloadStringWithRetryAsync(string p_url)
    {
        return await DownloadStringWithRetryAsync(p_url, 3, TimeSpan.FromSeconds(2), true);
    }

    public static async Task<string?> DownloadStringWithRetryAsync(string p_url, int p_nRetry, TimeSpan p_sleepBetweenRetries, bool p_throwExceptionIfUnsuccesful = true)
    {
        AssureHttpClientIsAlive();

        string webpage = string.Empty;
        int nDownload = 0;
        HttpRequestMessage request = CreateRequest(p_url);

        do
        {
            try
            {
                nDownload++;
                HttpResponseMessage response = await g_httpClient!.SendAsync(request);
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
                if (ex is AggregateException exception)
                {
                    foreach (var errInner in exception.InnerExceptions)
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
        }
        while (nDownload < p_nRetry);

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
        {
            return new HttpRequestMessage
            {
                RequestUri = new Uri(p_url),
                Method = HttpMethod.Get,
                Version = HttpVersion.Version20, // This was the key on Linux!!! The default is Version11 (Silly: "In .NET Core 3.0+, the default value was reverted back from 2.0 to 1.1.")
                Headers =
                {
                    { "accept", "application/json, text/plain, */*" }, // needed on Linux for api.nasdaq.com , otherwise it returns with 'no permission'.
                    { "user-agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/88.0.4324.182 Safari/537.36" },
                    { "accept-encoding", "gzip, deflate, br" },
                    { "accept-language", "en-US,en;q=0.9" }
                }
            };
        }
        else if (p_url.StartsWith("https://www.cmegroup.com"))
        {
            // had to copy All headers from Linux Chrome + setting up Http1.1 to make it work with curl (wget is only http1.0, use curl)
            // there is no need for cookies (yet)
            // curl -v --http1.1 -H 'Host: www.cmegroup.com' -H 'Connection: keep-alive' -H 'Cache-Control: max-age=0' -H 'Upgrade-Insecure-Requests: 1' -H 'User-Agent: Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/88.0.4324.96 Safari/537.36' -H 'Accept: text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3;q=0.9' -H 'Sec-Fetch-Site: none' -H 'Sec-Fetch-Mode: navigate' -H 'Sec-Fetch-User: ?1' -H 'Sec-Fetch-Dest: document' -H 'Accept-Encoding: gzip, deflate, br' -H 'Accept-Language: en-US,en;q=0.9' --output cme444brotli.br  https://www.cmegroup.com/CmeWS/mvc/Quotes/Future/444/G
            // 2024-01: on server, it stopped working with message: "This IP address is blocked due to suspected web scraping activity". But that is fake news, because the IP address works in Browser.
            // Giving the same Request header as Chrome and testing it with CURL in Terminal, this works now:
            // curl -v --http2 -H 'Accept: text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3;q=0.7' -H 'Accept-Encoding: gzip, deflate, br' -H 'Accept-Language: en-US,en;q=0.9' -H 'Cache-Control: no-cache' -H 'Pragma: no-cache' -H 'Sec-Fetch-Dest: document' -H 'Sec-Fetch-Mode: navigate' -H 'Sec-Fetch-Site: none' -H 'Sec-Fetch-User: ?1' -H 'Upgrade-Insecure-Requests: 1' -H 'User-Agent: Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/116.0.0.0 Safari/537.36' https://www.cmegroup.com/CmeWS/mvc/Quotes/Future/425/G --output cme444brotli.br
            return new HttpRequestMessage
            {
                RequestUri = new Uri(p_url),
                Method = HttpMethod.Get,
                Version = HttpVersion.Version20,
                Headers = // copied from Linux Chrome, worked in curl.
                {
                    { "Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3;q=0.7" },
                    { "Accept-Encoding", "gzip, deflate, br" },
                    { "Accept-Language", "en-US,en;q=0.9" },
                    { "Cache-Control", "no-cache" },
                    { "Pragma", "no-cache" },
                    { "Sec-Fetch-Dest", "document" },
                    { "Sec-Fetch-Mode", "navigate" },
                    { "Sec-Fetch-Site", "none" },
                    { "Sec-Fetch-User", "?1" },
                    { "Upgrade-Insecure-Requests", "1" },
                    { "User-Agent", "Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/116.0.0.0 Safari/537.36" }
                }
            };
        }
        else if (p_url.StartsWith("http://vixcentral.com/ajax_update"))
        {
            // curl -i -H "Accept: application/json" -H "Accept: text/javascript" -H "Accept: */*" -H "Accept-encoding: gzip deflate" -e "http://vixcentral.com/" -H "Host: vixcentral.com" -H "Connection: keep-alive" -H "X-Requested-With: XMLHttpRequest" "http://vixcentral.com/ajax_update"
            return new HttpRequestMessage
            {
                RequestUri = new Uri(p_url),
                Method = HttpMethod.Get,
                Version = HttpVersion.Version20,    // This was the key on Linux!!! The default is Version11 (Silly: "In .NET Core 3.0+, the default value was reverted back from 2.0 to 1.1.")
                Headers =
                {
                        { "accept", "application/json, text/javascript, */*" }, // needed on Linux for api.nasdaq.com , otherwise it returns with 'no permission'.
                        { "accept-encoding", "gzip, deflate" },
                        { "Host", "vixcentral.com" },
                        { "Connection", "keep-alive" },
                        { "X-Requested-With", "XMLHttpRequest" }
                }
            };
        }
        else if (p_url.StartsWith("https://www.cnbc.com/id/100003114/device/rss/rss.html"))
        {
            return new HttpRequestMessage
            {
                RequestUri = new Uri(p_url),
                Method = HttpMethod.Get,
                Headers =
                {
                        { "user-agent", "Mozilla/4.0 (compatible; MSIE 6.0; Windows NT 5.2; .NET CLR 1.0.3705;)" }
                }
            };
        }
        else if (p_url.StartsWith("https://www.spglobal.com"))
        {
            // curl -v --insecure --http2  -H "Accept: text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3;q=0.7" -H "User-Agent: Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36" https://www.spglobal.com/spdji/en/indices/equity/sp-500/ -o sp500.html
            return new HttpRequestMessage
            {
                RequestUri = new Uri(p_url),
                Method = HttpMethod.Get,
                Version = HttpVersion.Version20,
                Headers = // copied from Linux Chrome, worked in curl.
                {
                    { "Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3;q=0.7" },
                    { "User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36" }
                }
            };
        }
        else if (p_url.StartsWith("https://feeds.finance.yahoo.com/rss/2.0/headline")) // https://feeds.finance.yahoo.com/rss/2.0/headline?s=TSLA,AAPL
        {
            // In the browser (with default browser headers), it works: https://feeds.finance.yahoo.com/rss/2.0/headline?s=TSLA
            // curl -v https://feeds.finance.yahoo.com/rss/2.0/headline?s=TSLA // surprisingly it works, because cURL sends default User-Agent and Accept headers, even though it is not specified (always inspect with the "-v" verbose parameter what is happening)
            // curl -v https://feeds.finance.yahoo.com/rss/2.0/headline?s=TSLA -H "User-Agent: curl/8.7.1" -H "Accept: */*"   // with default cUrl headers, it works
            // curl -v https://feeds.finance.yahoo.com/rss/2.0/headline?s=TSLA -H "User-Agent: " -H "Accept: "   // with empty headers, it returns "429 Too Many Requests". So, if the User-Agent is empty, then it fails. Otherwise, it is OK.
            //
            // curl -v https://feeds.finance.yahoo.com/rss/2.0/headline?s=TSLA -H "User-Agent: Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/125.0.0.0 Safari/537.36" -H "Accept: " // implement this version in this C# code.
            return new HttpRequestMessage
            {
                RequestUri = new Uri(p_url),
                Method = HttpMethod.Get,
                Version = HttpVersion.Version20,
                Headers =
                {
                    { "User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/125.0.0.0 Safari/537.36" }
                }
            };
        }
        else if (p_url.StartsWith("https://query2.finance.yahoo.com/v8/finance/chart")) // https://query2.finance.yahoo.com/v8/finance/chart/AAPL?period1=0&period2=1729692470&interval=1d&events=history,split
        {
            // 2024-06:
            // curl -v --insecure "https://query2.finance.yahoo.com/v8/finance/chart/AAPL?period1=0&period2=1729692470&interval=1d&events=history,split" // surprisingly it works, because cURL sends default User-Agent and Accept headers, even though it is not specified (always inspect with the "-v" verbose parameter what is happening)
            // curl -v --insecure "https://query2.finance.yahoo.com/v8/finance/chart/AAPL?period1=0&period2=1729692470&interval=1d&events=history,split" -H "User-Agent: curl/8.7.1" -H "Accept: */*"   // with default cUrl headers, it works
            // curl -v --insecure "https://query2.finance.yahoo.com/v8/finance/chart/AAPL?period1=0&period2=1729692470&interval=1d&events=history,split" -H "User-Agent: " -H "Accept: "   // with empty headers, it returns "429 Too Many Requests". So, if the User-Agent is empty, then it fails. Otherwise, it is OK.
            //
            // curl -v --insecure "https://query2.finance.yahoo.com/v8/finance/chart/AAPL?period1=0&period2=1729692470&interval=1d&events=history,split" -H "User-Agent: Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/125.0.0.0 Safari/537.36" -H "Accept: " // implement this version in this C# code.
            //
            // 2025-05-06: previous long "User-Agent" started to give "429 Too Many Requests" (although the same works in the browser).
            // https://github.com/ranaroussi/yfinance/issues/2422 Python YF library bug discussion 'YFRateLimitError('Too Many Requests)'
            // Their solution was to impersonate 'Chrome' browser that exactly imitates the TSL and HTML2 handshake communication of Chrome. But by accident I figured out that this is not yet necessary.
            // This works now both local and server console:
            // curl -v -k --insecure --http2 "https://query2.finance.yahoo.com/v8/finance/chart/AAPL?period1=0&period2=1729692470&interval=1d&events=history,split" -H "User-Agent: Mozilla/5.0"
            // Laszlo info: "Surprisingly the YfQuoteCrawler was able to download the prices in the past days. One explanation could be, that it pays attention to not request too many data.
            // I have a “60 requests per minute” limit in my mind, I think it is on the yahoo page. Anyway, the crawler waits between 2 requests if necessary to have at least a 1001 ms time gap between requests."
            return new HttpRequestMessage
            {
                RequestUri = new Uri(p_url),
                Method = HttpMethod.Get,
                Version = HttpVersion.Version20,
                Headers =
                {
                    // { "User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/125.0.0.0 Safari/537.36" } // 2025-05-06: stopped working, giving "429 Too Many Requests"
                    { "User-Agent", "Mozilla/5.0" }
                }
            };
        }

        // 2022-07. .Net 6, Trying HTTP/3 protocol.
        // 1. Using HTTP3 in HttpClient
        // https://www.nyse.com/markets/hours-calendars only Http1.1
        // https://api.nasdaq.com/api/calendar/splits Http2
        // https://docs.google.com/spreadsheets uses Http3 in Chrome, but response is only Http2 here. Maybe it is only experimental and they will fix it in .NET 7.
        // So, we set it up to use Http3 if possible, but very few websites use it. Whatever, try to use it if server supports.

        // 2. Using HTTP3 in Server (Kestrel):
        // https://docs.microsoft.com/en-us/aspnet/core/fundamentals/servers/kestrel/http3?view=aspnetcore-6.0  Many problems.
        // It can only work via HTTPS, not HTTP. "You first need to enable the preview function in csproj:"
        // That will enable ALL .NET 6 preview features. With ALL potential bugs and Linux problems. Better to delay HTTP3 in Kestrel server until it becomes final, not Preview.
        return new HttpRequestMessage
        {
            RequestUri = new Uri(p_url),
            Method = HttpMethod.Get,
            Version = HttpVersion.Version30,    // The default is Version11 (Silly: "In .NET Core 3.0+, the default value was reverted back from 2.0 to 1.1.")
            VersionPolicy = HttpVersionPolicy.RequestVersionOrLower
        };
    }

    // To run it locally and reach HealthMonitor server, it requires to set up its AWS firewall port filter properly. It is problem because developer PC IP changes all the time.
    // So, we allowed HealthMonitor server to receive messages from everywhere. It is not much of a risk, it is only a MonitoringApp, not a traderApp.
    public static async Task<string?> DownloadStringRoutedToUsaProxy(string p_url /*, int p_nRetry, TimeSpan p_sleepBetweenRetries, bool p_throwExceptionIfUnsuccesful = true */)
    {
        Task<string?> tcpMsgTask = TcpMessage.Send(p_url, (int)HealthMonitorMessageID.ProxyServerDownloadUrl, ServerIp.HealthMonitorPublicIp, ServerIp.DefaultHealthMonitorServerPort);
        string? tcpMsgResponse = await tcpMsgTask;
        // Utils.Logger.Info("CheckHealthMonitorAlive() returned answer: " + tcpMsgResponse ?? string.Empty);
        Console.WriteLine($"HealthMonitor DownloadStringRoutedToUsaProxy return length: '{(tcpMsgResponse ?? string.Empty).Length}'");
        if (tcpMsgTask.Exception != null || String.IsNullOrEmpty(tcpMsgResponse))
        {
            string errorMsg = $"Error. DownloadStringRoutedToUsaProxy() to {ServerIp.HealthMonitorPublicIp}:{ServerIp.DefaultHealthMonitorServerPort}";
            Utils.Logger.Error(errorMsg);
            return string.Empty;
        }
        else
            return tcpMsgResponse;
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
        // var url = "https://www.nasdaq.com/api/v1/historical/A/stocks/2019-07-19/2020-07-19";  // this is text, no compression. Content-Type: application/csv
        var request = new HttpRequestMessage
        {
            RequestUri = new Uri(url),
            Method = HttpMethod.Get,
            Version = HttpVersion.Version20,    // This was the key on Linux!!! The default is Version11 (Silly: "In .NET Core 3.0+, the default value was reverted back to 1.1.")
            Headers =
            {
                // { "authority", "api.nasdaq.com" }, // HERE IS HOW TO ADD HEADERS,
                // { "scheme", "https" },
                // { "path", "/api.nasdaq.com/api/calendar/splits?date=2021-02-24" },
                // { "cache-control", "no-cache" },
                { "accept", "application/json, text/plain, */*" }, // needed on Linux for api.nasdaq.com , otherwise it returns with 'no permission'.
                { "user-agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/88.0.4324.182 Safari/537.36" },
                // { "origin", "https://www.nasdaq.com" },
                // { "sec-fetch-site", "same-site" },
                // { "sec-fetch-mode", "cors" },
                // { "sec-fetch-dest", "empty" },
                // { "referer", "https://www.nasdaq.com/" },
                { "accept-encoding", "gzip, deflate, br" },
                { "accept-language", "en-US,en;q=0.9" }
            }
        };

        Console.WriteLine($"HttpRequestMessage:'{request}'");

        HttpClient httpClient = new(g_httpHandler);
        var response = await httpClient.SendAsync(request);
        using HttpContent content = response.Content;
        var contentStr = await content.ReadAsStringAsync();
        Console.WriteLine($"TestDownloadApiNasdaqCom Returned: '{contentStr}'");
    }
}