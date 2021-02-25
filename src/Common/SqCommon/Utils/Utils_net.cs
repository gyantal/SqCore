using System;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading;

namespace SqCommon
{
    public static partial class Utils
    {
        public static void TcpClientDispose(TcpClient p_tcpClient)
        {
            if (p_tcpClient == null)
                return;
            p_tcpClient.Dispose();
        }

        static HttpClientHandler g_httpHandler = new HttpClientHandler()  // without this, returned stream is the raw binary if compressed
        {
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate   // otherwise if content is gzipped, we got binary in GetString()
        };

        // HttpWebRequest vs. HttpClient. Never use HttpWebRequest. HttpClient is preferred over HttpWebRequest. https://www.diogonunes.com/blog/webclient-vs-httpclient-vs-httpwebrequest/
        static HttpClient g_httpClient = new HttpClient(g_httpHandler); // for efficiency, we can make it global, because it can handle multiple queries in multithread


        public static bool DownloadStringWithRetry(string p_url, out string p_webpage)
        {
            return DownloadStringWithRetry(p_url, out p_webpage, 3, TimeSpan.FromSeconds(2), true);
        }

        public static bool DownloadStringWithRetry(string p_url, out string p_webpage, int p_nRetry, TimeSpan p_sleepBetweenRetries, bool p_throwExceptionIfUnsuccesfull = true)
        {
            p_webpage = String.Empty;
            int nDownload = 0;
            var request = new HttpRequestMessage
            {
                RequestUri = new Uri(p_url),
                Method = HttpMethod.Get,
                Version = HttpVersion.Version20,    // This was the key on Linux!!! The default is Version11 (Silly: "In .NET Core 3.0+, the default value was reverted back to 1.1.")
                Headers = {
                            { "accept", "application/json, text/plain, */*" }, // needed on Linux for api.nasdaq.com , otherwise it returns with 'no permission'.
                            { "user-agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/88.0.4324.182 Safari/537.36" },
                            { "accept-encoding", "gzip, deflate, br" },
                            { "accept-language", "en-US,en;q=0.9" }
                        }
            };

            do
            {
                try
                {
                    nDownload++;
                    var response = g_httpClient.SendAsync(request).Result;
                    using (HttpContent content = response.Content)
                    {
                        p_webpage = content.ReadAsStringAsync().Result;
                    }

                    // httpClient.DefaultRequestHeaders.AcceptEncoding.ParseAdd("gzip, deflate, br");
                    // httpClient.DefaultRequestHeaders.Connection.ParseAdd("keep-alive"); // for https://www.nasdaq.com/api keep-alive is needed, otherwise Timeout
                    // p_webpage = httpClient.GetStringAsync(p_url).Result;

                    Utils.Logger.Debug(String.Format("DownloadStringWithRetry() OK:{0}, nDownload-{1}, Length of reply:{2}", p_url, nDownload, p_webpage.Length));
                    return true;
                }
                catch (Exception ex)
                {
                    // it is quite expected that sometimes (once per month), there is a problem:
                    // "The operation has timed out " or "Unable to connect to the remote server" exceptions
                    // Don't raise Logger.Error() after the first attempt, because it is not really Exceptional, and an Error email will be sent
                    Utils.Logger.Info(ex, "Exception in DownloadStringWithRetry()" + p_url + ":" + nDownload + ": " + ex.Message);
                    Thread.Sleep(p_sleepBetweenRetries);
                    if ((nDownload >= p_nRetry) && p_throwExceptionIfUnsuccesfull)
                        throw;  // if exception still persist after many tries, rethrow it to caller
                }
            } while (nDownload < p_nRetry);

            return false;
        }

        public static bool TestDownloadApiNasdaqCom()
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
            var response = httpClient.SendAsync(request).Result;
            using (HttpContent content = response.Content)
            {
                var contentStr = content.ReadAsStringAsync().Result;
                Console.WriteLine($"TestDownloadApiNasdaqCom Returned: '{contentStr}'");
            }

            return true;
        }

    }
}