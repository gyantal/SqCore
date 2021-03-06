using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.ServiceModel.Syndication;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using SqCommon;
using System.Text.Json;
using System.Text;
using System.Net.WebSockets;
using System.Threading;

namespace SqCoreWeb
{
    public enum NewsSource
    {
        YahooRSS,
        CnbcRss,
        Benzinga,
        TipRanks
    }
    public class NewsItem
    {
        public string Ticker { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Summary { get; set; } = string.Empty;
        public string LinkUrl { get; set; } = string.Empty;
        public DateTime DownloadTime { get; set; }
        public DateTime PublishDate { get; set; }
        public string Source { get; set; } = string.Empty;
        public string DisplayText { get; set; } = string.Empty;
        public string Sentiment { get; set; } = string.Empty;
    }

    public class QuickfolioNewsDownloader
    {
        Dictionary<string, List<NewsItem>> m_newsMemory = new Dictionary<string, List<NewsItem>>();
        Random m_random = new Random(DateTime.Now.Millisecond);
        KeyValuePair<int, int> m_sleepBetweenDnsMs = new KeyValuePair<int, int>(2000, 1000);     // <fix, random>
        string[] m_stockTickers = { "AAPL", "ADBE", "AMZN", "BABA", "CRM", "FB", "GOOGL", "MA", "MSFT", "NVDA", "PYPL", "QCOM", "V" };

        public QuickfolioNewsDownloader()
        {
        }

        public void UpdateStockTickers()
        {
            string? valuesFromGSheetStr = "Error. Make sure GoogleApiKeyKey, GoogleApiKeyKey is in SQLab.WebServer.SQLab.NoGitHub.json !";
            if (!String.IsNullOrEmpty(Utils.Configuration["Google:GoogleApiKeyName"]) && !String.IsNullOrEmpty(Utils.Configuration["Google:GoogleApiKeyKey"]))
            {
                valuesFromGSheetStr = Utils.DownloadStringWithRetryAsync("https://sheets.googleapis.com/v4/spreadsheets/1c5ER22sXDEVzW3uKthclpArlZvYuZd6xUffXhs6rRsM/values/A1%3AA1?key=" + Utils.Configuration["Google:GoogleApiKeyKey"]).TurnAsyncToSyncTask();
                if (valuesFromGSheetStr == null)
                    valuesFromGSheetStr = "Error in DownloadStringWithRetry().";
            }
            if (!valuesFromGSheetStr.StartsWith("Error"))
            {
                ExtractTickers(valuesFromGSheetStr);
            }
        }

        private void ExtractTickers(string p_spreadsheetString)
        {
            int pos = p_spreadsheetString.IndexOf(@"""values"":");
            if (pos < 0)
                return;
            p_spreadsheetString = p_spreadsheetString.Substring(pos + 9); // cut off until the end of "values":
            int posStart = p_spreadsheetString.IndexOf(@"""");
            if (posStart < 0)
                return;
            int posEnd = p_spreadsheetString.IndexOf(@"""", posStart + 1);
            if (posEnd < 0)
                return;
            string cellValue = p_spreadsheetString.Substring(posStart + 1, posEnd - posStart - 1);
            m_stockTickers = cellValue.Split(',').Select(x => x.Trim()).ToArray();
        }

        internal void GetCommonNewsAndSendToClient(DashboardClient p_client)
        {
            string rssFeedUrl = string.Format(@"https://www.cnbc.com/id/100003114/device/rss/rss.html");

            List<NewsItem> foundNewsItems = new List<NewsItem>();
            // try max 5 downloads to leave the tread for sure (call this method repeats continuosly)
            int retryCount = 0;
            while ((foundNewsItems.Count < 1) && (retryCount < 5))
            {
                foundNewsItems = ReadRSS(rssFeedUrl, NewsSource.CnbcRss, string.Empty);
                if (foundNewsItems.Count == 0)
                    System.Threading.Thread.Sleep(m_sleepBetweenDnsMs.Key + m_random.Next(m_sleepBetweenDnsMs.Value));
                retryCount++;
            }
            // AddFoundNews(0, foundNewsItems);
            // return NewsToString(m_newsMemory[0]);

            byte[] encodedMsg = Encoding.UTF8.GetBytes("QckfNews.CommonNews:" + Utils.CamelCaseSerialize(foundNewsItems));
            if (p_client.WsWebSocket != null && p_client.WsWebSocket!.State == WebSocketState.Open)
            {
                // to free up resources, send data only if either this is the active tool is this tool or if some seconds has been passed
                // OnConnectedWsAsync() sleeps for a while if not active tool.
                TimeSpan timeSinceConnect = DateTime.UtcNow - p_client.WsConnectionTime;
                if (p_client.ActivePage != ActivePage.QuickfolioNews && timeSinceConnect < DashboardClient.c_initialSleepIfNotActiveToolQn.Add(TimeSpan.FromMilliseconds(100)))
                    return;

                p_client.WsWebSocket.SendAsync(new ArraySegment<Byte>(encodedMsg, 0, encodedMsg.Length), WebSocketMessageType.Text, true, CancellationToken.None);
            }

            // foreach (var client in p_clients)        // List<DashboardClient> p_clients
            // {
            //     if (client.WsWebSocket != null && client.WsWebSocket!.State == WebSocketState.Open)
            //     {
            //         // to free up resources, send data only if either this is the active tool is this tool or if some seconds has been passed
            //         // OnConnectedWsAsync() sleeps for a while if not active tool.
            //         TimeSpan timeSinceConnect = DateTime.UtcNow - client.WsConnectionTime;
            //         if (client.ActivePage != ActivePage.QuickfolioNews && timeSinceConnect < DashboardClient.c_initialSleepIfNotActiveToolQn.Add(TimeSpan.FromMilliseconds(100)))
            //             continue;

            //         client.WsWebSocket.SendAsync(new ArraySegment<Byte>(encodedMsg, 0, encodedMsg.Length), WebSocketMessageType.Text, true, CancellationToken.None);
            //     }
            // }
        }

        internal List<string> GetStockTickers()
        {
            return new List<string> { "All assets" }.Union(m_stockTickers).ToList();
        }
        internal void GetStockNewsAndSendToClient(DashboardClient p_client) // with 13 tickers, it can take 13 * 2 = 26seconds
        {
            foreach (string ticker in m_stockTickers)
            {
                byte[]? encodedMsgRss = null, encodedMsgBenzinga = null, encodedMsgTipranks = null;
                string rssFeedUrl = string.Format(@"https://feeds.finance.yahoo.com/rss/2.0/headline?s={0}&region=US&lang=en-US", ticker);
                var rss = ReadRSS(rssFeedUrl, NewsSource.YahooRSS, ticker);
                if (rss.Count > 0)
                    encodedMsgRss = Encoding.UTF8.GetBytes("QckfNews.StockNews:" + Utils.CamelCaseSerialize(rss));
                var benzinga = ReadBenzingaNews(ticker);
                if (benzinga.Count > 0)
                    encodedMsgBenzinga = Encoding.UTF8.GetBytes("QckfNews.StockNews:" + Utils.CamelCaseSerialize(benzinga));
                var tipranks = ReadTipranksNews(ticker);
                if (tipranks.Count > 0)
                    encodedMsgTipranks = Encoding.UTF8.GetBytes("QckfNews.StockNews:" + Utils.CamelCaseSerialize(tipranks));

                if (p_client.WsWebSocket != null && p_client.WsWebSocket!.State == WebSocketState.Open)
                {
                    // to free up resources, send data only if either this is the active tool is this tool or if some seconds has been passed
                    // OnConnectedWsAsync() sleeps for a while if not active tool.
                    TimeSpan timeSinceConnect = DateTime.UtcNow - p_client.WsConnectionTime;
                    if (p_client.ActivePage != ActivePage.QuickfolioNews && timeSinceConnect < DashboardClient.c_initialSleepIfNotActiveToolQn.Add(TimeSpan.FromMilliseconds(100)))
                        continue;

                    if (encodedMsgRss != null)
                        p_client.WsWebSocket.SendAsync(new ArraySegment<Byte>(encodedMsgRss, 0, encodedMsgRss.Length), WebSocketMessageType.Text, true, CancellationToken.None);
                    if (encodedMsgBenzinga != null)
                        p_client.WsWebSocket.SendAsync(new ArraySegment<Byte>(encodedMsgBenzinga, 0, encodedMsgBenzinga.Length), WebSocketMessageType.Text, true, CancellationToken.None);
                    if (encodedMsgTipranks != null)
                        p_client.WsWebSocket.SendAsync(new ArraySegment<Byte>(encodedMsgTipranks, 0, encodedMsgTipranks.Length), WebSocketMessageType.Text, true, CancellationToken.None);
                }
            }
        }

        private List<NewsItem> ReadTipranksNews(string p_ticker)
        {
            List<NewsItem> foundNewsItems = new List<NewsItem>();
            if (foundNewsItems == null)
                foundNewsItems = new List<NewsItem>();
            //MakeRequests();
            string url = string.Format(@"https://www.tipranks.com/api/stocks/getNews/?ticker={0}", p_ticker);
            string? webpageData = Utils.DownloadStringWithRetryAsync(url).TurnAsyncToSyncTask();
            System.Threading.Thread.Sleep(m_sleepBetweenDnsMs.Key + m_random.Next(m_sleepBetweenDnsMs.Value));  // to avoid that server bans our IP
            if (webpageData != null)
            {
                ReadTipranksNewsItems(foundNewsItems, p_ticker, webpageData);
            }
            return foundNewsItems;
        }
        private void ReadTipranksNewsItems(List<NewsItem> p_foundNewsItems, string p_ticker, string webpageData)
        {
            try
            {
                // var jsonOptions = new System.Text.Json.JsonSerializerOptions
                // {
                //     AllowTrailingCommas = true
                // };
                // NewsItem jsonObject =  System.Text.Json.JsonSerializer.Deserialize<NewsItem>(webpageData, jsonOptions);

                System.Text.Json.JsonDocument document = System.Text.Json.JsonDocument.Parse(webpageData);
                JsonElement root = document.RootElement;
                JsonElement newssElement = root.GetProperty("news");
                foreach (JsonElement news in newssElement.EnumerateArray())
                {
                    NewsItem newsItem = new NewsItem();
                    newsItem.Ticker = p_ticker;
                    newsItem.LinkUrl = news.GetProperty("url").GetRawText().Trim('"');
                    newsItem.Title = news.GetProperty("title").GetRawText().Trim('"'); 
                    newsItem.Summary = "  ";
                    newsItem.Sentiment = news.GetProperty("sentiment").GetRawText().Trim('"'); 
                    DateTime date;
                    if (DateTime.TryParse(news.GetProperty("articleTimestamp").GetRawText().Trim('"'), out date))
                        newsItem.PublishDate = date;
                    newsItem.DownloadTime = DateTime.Now;
                    newsItem.Source = NewsSource.TipRanks.ToString();

                    if (AddNewsToMemory(p_ticker, newsItem))
                        p_foundNewsItems.Add(newsItem);
                }
            }
            catch (Exception)
            {
                DateTime.Today.AddDays(1);
            }
        }
        private List<NewsItem> ReadBenzingaNews(string p_ticker)
        {
            List<NewsItem> foundNewsItems = new List<NewsItem>();
            if (foundNewsItems == null)
                foundNewsItems = new List<NewsItem>();
            string url = string.Format(@"https://www.benzinga.com/stock/{0}", p_ticker);
            string? webpageData = Utils.DownloadStringWithRetryAsync(url).TurnAsyncToSyncTask();
            System.Threading.Thread.Sleep(m_sleepBetweenDnsMs.Key + m_random.Next(m_sleepBetweenDnsMs.Value));  // to avoid that server bans our IP
            if (webpageData != null)
            {
                ReadBenzingaSection(foundNewsItems, p_ticker, webpageData, "headlines");
                ReadBenzingaSection(foundNewsItems, p_ticker, webpageData, "press");
            }
            return foundNewsItems;
        }

        private void ReadBenzingaSection(List<NewsItem> p_foundNewsItems, string p_ticker, string p_webpageData, string p_keyWord)
        {
            Regex regexBenzingaLists = new Regex(@"<div[^>]*?class=""stories""[^>]*?" + p_keyWord + @"(?<CONTENT>(\s|\S)*?)</div>"
                , RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
            Regex regexBenzingaNews = new Regex(@"<li(\s|\S)*?class=""story""(\s|\S)*?<a href=""(?<LINK>[^""]*)"">(?<TITLE>[^<]*)<(\s|\S)*?<span class=""date"">(?<DATE>[^<]*)"
                , RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
            MatchCollection matches = regexBenzingaLists.Matches(p_webpageData);
            if (matches == null)
                return;
            for (int index = 0; index < matches.Count; index++)
            {
                Match match = matches[index];
                MatchCollection matchesNews = regexBenzingaNews.Matches(match.Groups["CONTENT"].Value);
                for (int indexNews = 0; indexNews < matchesNews.Count; indexNews++)
                {
                    Match matchNews = matchesNews[indexNews];
                    NewsItem newsItem = new NewsItem();
                    newsItem.Ticker = p_ticker;
                    newsItem.LinkUrl = matchNews.Groups["LINK"].Value;
                    newsItem.Title = WebUtility.HtmlDecode(matchNews.Groups["TITLE"].Value);
                    newsItem.Summary = "  ";
                    newsItem.PublishDate = GetNewsDate(matchNews.Groups["DATE"].Value);
                    newsItem.DownloadTime = DateTime.Now;
                    newsItem.Source = NewsSource.Benzinga.ToString();

                    if (AddNewsToMemory(p_ticker, newsItem))
                        p_foundNewsItems.Add(newsItem);
                }
            }
        }
        private DateTime GetNewsDate(string p_dateString)
        {
            DateTime date;
            if (DateTime.TryParse(p_dateString, out date))
                return date;
            p_dateString = p_dateString.ToUpper();
            if (p_dateString.Contains("AGO"))
            {
                p_dateString = p_dateString.Replace("AGO", string.Empty).Trim();
                if (p_dateString.Contains("HOUR"))
                {
                    p_dateString = p_dateString.Replace("HOURS", string.Empty).Replace("HOUR", string.Empty).Trim();
                    int hours;
                    if (int.TryParse(p_dateString, out hours))
                        return DateTime.Now.AddHours(-hours);
                }
                else if (p_dateString.Contains("DAY"))
                {
                    p_dateString = p_dateString.Replace("DAYS", string.Empty).Replace("DAY", string.Empty).Trim();
                    int days;
                    if (int.TryParse(p_dateString, out days))
                        return DateTime.Now.AddDays(-days);
                }
                else if (p_dateString.Contains("MIN"))
                {
                    p_dateString = p_dateString.Replace("MINUTES", string.Empty).Replace("MIN", string.Empty).Replace("MINS", string.Empty).Trim();
                    int days;
                    if (int.TryParse(p_dateString, out days))
                        return DateTime.Now.AddDays(-days);
                }
            }
            return DateTime.Now;
        }
        private string NewsToString(List<NewsItem> newsList)
        {
            string finalString = string.Empty;
            foreach (NewsItem news in newsList.OrderBy(x => x.PublishDate))
                finalString += string.Format(@"news_ticker{0}news_title{1}news_summary{2}news_link{3}news_downloadTime{4:yyyy-MM-dd hh:mm}news_publishDate{5:yyyy-MM-dd hh:mm}news_source{6}news_end",
                    news.Ticker, news.Title, news.Summary, news.LinkUrl, news.DownloadTime, news.PublishDate, news.Source);
            return finalString;
        }


        private void AddFoundNews(int p_stockID, List<NewsItem> p_foundNewsItems)
        {
            // List<NewsItem> notYetKnownNews = new List<NewsItem>();
            // if (!m_newsMemory.ContainsKey(p_stockID))
            //     m_newsMemory.Add(p_stockID, new List<NewsItem>());
            // foreach (NewsItem newsItem in p_foundNewsItems)
            //     if (m_newsMemory[p_stockID].Where(x => x.LinkUrl.Equals(newsItem.LinkUrl)).Count() == 0)    // not yet known
            //     {
            //         m_newsMemory[p_stockID].Add(newsItem);
            //         notYetKnownNews.Add(newsItem);
            //     }
        }

        private List<NewsItem> ReadRSS(string p_url, NewsSource p_newsSource, string p_ticker)
        {
            try
            {
                var webClient = new WebClient();
                // hide ;-)
                webClient.Headers.Add("user-agent", "Mozilla/4.0 (compatible; MSIE 6.0; Windows NT 5.2; .NET CLR 1.0.3705;)");
                // fetch feed as string
                var content = webClient.OpenRead(p_url);
                var contentReader = new StreamReader(content);
                var rssFeedAsString = contentReader.ReadToEnd();
                // convert feed to XML using LINQ to XML and finally create new XmlReader object
                var feed = System.ServiceModel.Syndication.SyndicationFeed.Load(XDocument.Parse(rssFeedAsString).CreateReader());

                List<NewsItem> foundNewNews = new List<NewsItem>();

                foreach (SyndicationItem item in feed.Items)
                {
                    NewsItem newsItem = new NewsItem();
                    newsItem.Ticker = p_ticker;
                    newsItem.LinkUrl = item.Links[0].Uri.AbsoluteUri;
                    newsItem.Title = WebUtility.HtmlDecode(item.Title.Text);
                    newsItem.Summary = WebUtility.HtmlDecode(item.Summary?.Text ?? string.Empty);   // <description> is missing sometimes, so Summary = null
                    newsItem.PublishDate = item.PublishDate.LocalDateTime;
                    newsItem.DownloadTime = DateTime.Now;
                    newsItem.Source = p_newsSource.ToString();
                    newsItem.DisplayText = string.Empty;
                    //newsItem.setFiltered();

                    if (AddNewsToMemory(p_ticker, newsItem))
                        foundNewNews.Add(newsItem);
                }
                return foundNewNews;
            }
            catch (Exception exception)
            {
                Console.WriteLine($"QuickfolioNewsDownloader.ReadRSS() exception: '{exception.Message}'");
                return new List<NewsItem>();
            }
        }

        private bool AddNewsToMemory(string p_ticker, NewsItem p_newsItem)
        {
            if (!m_newsMemory.ContainsKey(p_ticker))
                m_newsMemory.Add(p_ticker, new List<NewsItem>());
            if (m_newsMemory[p_ticker].Where(x => x.LinkUrl.Equals(p_newsItem.LinkUrl)).Count() > 0)    // already known
                return false;
            if (SkipNewsItem(p_newsItem.Title))
                return false;
            m_newsMemory[p_ticker].Add(p_newsItem);
            return true;
        }

        private bool SkipNewsItem(string p_title)
        {
            // skip news item if title is like NVIDIA rises 3.1%
            string upperCaseTitle = p_title.ToUpper();
            int indexInTitle = upperCaseTitle.LastIndexOf("RISES");
            if (indexInTitle == -1)
                indexInTitle = upperCaseTitle.LastIndexOf("FALLS");
            if (indexInTitle == -1)
                return false;

            indexInTitle += 5; // the length of "rises" or "falls". They have equal length, no need to separate the cases
            while (indexInTitle < p_title.Length)
            {
                char currentChar = p_title[indexInTitle];
                if ((currentChar >= '0' && currentChar <= '9') || (currentChar == '.') || (currentChar == ' '))
                    indexInTitle++;
                else if ((currentChar == '%') && (indexInTitle == p_title.Length - 1))
                    return true;
                else
                    return false;
            }
            return false;
            // Regex regexRisesFalls = new Regex(@"(rises|falls)([0-9]|.|\s)*%$"
            //    , RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
            // return regexRisesFalls.IsMatch(p_title);
        }
    }
}