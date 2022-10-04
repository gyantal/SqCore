using System;
using System.Threading;
using SqCommon;
using System.Collections.Generic;
using System.Text;
using System.Net.WebSockets;
using System.Linq;
using System.Xml.Linq;
using System.ServiceModel.Syndication;
using System.Net;

namespace SqCoreWeb;

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

public partial class DashboardClient
{
    // static readonly QuickfolioNewsDownloader g_newsDownloader = new(); // only 1 global downloader for all clients
    // one global static quickfolio News Timer serves all clients. For efficiency.
    static readonly Timer m_qckflNewsTimer = new(new TimerCallback(QckflNewsTimer_Elapsed), null, TimeSpan.FromMilliseconds(-1.0), TimeSpan.FromMilliseconds(-1.0));
    static readonly object m_qckflNewsTimerLock = new();
    static bool isQckflNewsTimerRunning = false;
    static readonly int m_qckflNewsTimerFrequencyMs = 15 * 60 * 1000; // timer for 15 minutes
    static readonly TimeSpan c_initialSleepIfNotActiveToolQn = TimeSpan.FromMilliseconds(10 * 1000); // 10sec
    static List<NewsItem> g_commonNews = new();
    static List<NewsItem> g_stockNews = new();
    // string[] m_stockTickers = { "AAPL", "ADBE", "AMZN", "BABA", "CRM", "FB", "GOOGL", "MA", "MSFT", "NVDA", "PYPL", "QCOM", "V" };
    static string[] m_stockTickers = Array.Empty<string>();

    public void OnConnectedWsAsync_QckflNews(bool p_isThisActiveToolAtConnectionInit)
    {
        Utils.RunInNewThread(ignored => // running parallel on a ThreadPool thread, FireAndForget: QueueUserWorkItem [26microsec] is 25% faster than Task.Run [35microsec]
        {
            Thread.CurrentThread.IsBackground = true; // thread will be killed when all foreground threads have died, the thread will not keep the application alive.

            // Assuming this tool is not the main Tab page on the client, we delay sending all the data, to avoid making the network and client too busy an unresponsive
            if (!p_isThisActiveToolAtConnectionInit)
                Thread.Sleep(DashboardClient.c_initialSleepIfNotActiveToolQn); // 10 sec is quite a long waiting, but we rarely use this tool.

            if (m_stockTickers.Length == 0)
                m_stockTickers = GetQckflStockTickers() ?? Array.Empty<string>();

            byte[] encodedMsg = Encoding.UTF8.GetBytes("QckfNews.Tickers:" + Utils.CamelCaseSerialize(new List<string> { "All assets" }.Union(m_stockTickers).ToList()));
            if (WsWebSocket!.State == WebSocketState.Open)
                WsWebSocket.SendAsync(new ArraySegment<Byte>(encodedMsg, 0, encodedMsg.Length), WebSocketMessageType.Text, true, CancellationToken.None);

            if (g_commonNews.Count > 0)
            {
                Utils.Logger.Info("OnConnectedAsync_QckflNews(). common news already downloaded.");

                byte[] encodedMsgCommonNews = Encoding.UTF8.GetBytes("QckfNews.CommonNews:" + Utils.CamelCaseSerialize(g_commonNews));
                if (WsWebSocket!.State == WebSocketState.Open)
                    WsWebSocket.SendAsync(new ArraySegment<Byte>(encodedMsgCommonNews, 0, encodedMsgCommonNews.Length), WebSocketMessageType.Text, true, CancellationToken.None);
            }
            if (g_stockNews.Count > 0)
            {
                Utils.Logger.Info("OnConnectedAsync_QckflNews(). stock news already downloaded.");

                byte[] encodedMsgCommonNews = Encoding.UTF8.GetBytes("QckfNews.StockNews:" + Utils.CamelCaseSerialize(g_stockNews));
                if (WsWebSocket!.State == WebSocketState.Open)
                    WsWebSocket.SendAsync(new ArraySegment<Byte>(encodedMsgCommonNews, 0, encodedMsgCommonNews.Length), WebSocketMessageType.Text, true, CancellationToken.None);
            }
            lock (m_qckflNewsTimerLock)
            {
                if (!isQckflNewsTimerRunning)
                {
                    Utils.Logger.Info("OnConnectedAsync_QckflNews(). Starting m_qckflNewsTimer.");
                    isQckflNewsTimerRunning = true;
                    m_qckflNewsTimer.Change(System.TimeSpan.Zero, TimeSpan.FromMilliseconds(-1.0));    // runs only once. To avoid that it runs parallel, if first one doesn't finish
                }
            }
        });
    }
    public static string[]? GetQckflStockTickers()
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
            int pos = valuesFromGSheetStr.IndexOf(@"""values"":");
            if (pos < 0)
                return null;
            valuesFromGSheetStr = valuesFromGSheetStr[(pos + 9)..]; // cut off until the end of "values":
            int posStart = valuesFromGSheetStr.IndexOf(@"""");
            if (posStart < 0)
                return null;
            int posEnd = valuesFromGSheetStr.IndexOf(@"""", posStart + 1);
            if (posEnd < 0)
                return null;
            string cellValue = valuesFromGSheetStr.Substring(posStart + 1, posEnd - posStart - 1);
            return cellValue.Split(',').Select(x => x.Trim()).ToArray();
        }
        else
            return null;
    }
    public static List<NewsItem> GetQckflCommonNews()
    {
        string rssFeedUrl = string.Format(@"https://www.cnbc.com/id/100003114/device/rss/rss.html");
        return ReadRSSAsync(rssFeedUrl, NewsSource.CnbcRss, string.Empty);
    }

    public static List<NewsItem> ReadRSSAsync(string p_url, NewsSource p_newsSource, string p_ticker)
    {
        try
        {
            string? rssFeedAsString = Utils.DownloadStringWithRetryAsync(p_url, 3, TimeSpan.FromSeconds(5), true).TurnAsyncToSyncTask();
            if (String.IsNullOrEmpty(rssFeedAsString))
            {
                Console.WriteLine($"QuickfolioNewsDownloader.ReadRSS() url download failed.");
                return new List<NewsItem>();
            }

            // convert feed to XML using LINQ to XML and finally create new XmlReader object
            var feed = System.ServiceModel.Syndication.SyndicationFeed.Load(XDocument.Parse(rssFeedAsString).CreateReader());
            List<NewsItem> foundNews = new();
            foreach (SyndicationItem item in feed.Items)
            {
                NewsItem newsItem = new();
                newsItem.Ticker = p_ticker;
                newsItem.LinkUrl = item.Links[0].Uri.AbsoluteUri;
                newsItem.Title = WebUtility.HtmlDecode(item.Title.Text);
                newsItem.Summary = WebUtility.HtmlDecode(item.Summary?.Text ?? string.Empty); // <description> is missing sometimes, so Summary = null
                newsItem.PublishDate = item.PublishDate.LocalDateTime;
                newsItem.DownloadTime = DateTime.UtcNow;
                newsItem.Source = p_newsSource.ToString();
                newsItem.DisplayText = string.Empty;
                // newsItem.setFiltered();
                // we might filter news and bring Laszlo's bool SkipNewsItem(string p_title) here. Later. Not now.
                foundNews.Add(newsItem);
            }
            return foundNews;
        }
        catch (Exception exception)
        {
            Console.WriteLine($"QuickfolioNewsDownloader.ReadRSS() exception: '{exception.Message}'");
            return new List<NewsItem>();
        }
    }
    public static List<NewsItem> GetQckflStockNews()
    {
        List<NewsItem> foundStockNews = new();
        foreach (string ticker in m_stockTickers)
        {
            string rssFeedUrl = string.Format(@"https://feeds.finance.yahoo.com/rss/2.0/headline?s={0}&region=US&lang=en-US", ticker);
            var stockNews = ReadRSSAsync(rssFeedUrl, NewsSource.YahooRSS, ticker);
            for (int i = 0; i < stockNews.Count; i++)
            {
                foundStockNews.Add(stockNews[i]);
            }

            // Tipranks news: 2021-12: Disabled it temporarily. https://www.tipranks.com/api/stocks/getNews/?ticker=ISRG returns a <HTML> in C# (first char: '<'), while it returns a proper JSON in Chrome (first char: '{')

            // Benzinga news: 2021-10-01: Benzinga banned  the IP of the server. Even in Chrome, even the simple www.benzinga.com doesn't work. They have a Varnish cache server, that refuses to give the page.
            // There is nothing to do. They didn't ban all AWS servers, because it works from our other Linux servers.
            // They only banned the SqCore server, because they noticed that there were too many queries. This is why we have to be cautious.
            // Laszlo's crawler only queries Benzinga once per day. And they didn't ban him. However, we queried Benzinga at every NewClientConnection. About 20-30x per day. (No timer was set), so it wasn't excessive.
            // However, in the future (after they release the ban) we might implement that it only crawles Benzinga news max 1-2x per day.
        }
        return foundStockNews;
    }
    public static void QckflNewsTimer_Elapsed(object? state) // Timer is coming on a ThreadPool thread
    {
        try
        {
            Utils.Logger.Debug("QckflNewsTimer_Elapsed(). BEGIN");
            if (!isQckflNewsTimerRunning)
                return; // if it was disabled by another thread in the meantime, we should not waste resources to execute this.

            var g_clientsPtrCpy = DashboardClient.g_clients; // Multithread warning! Lockfree Read | Copy-Modify-Swap Write Pattern
            if (g_clientsPtrCpy.Count > 0)
            {
                g_commonNews = GetQckflCommonNews();
                g_stockNews = GetQckflStockNews();
                for (int i = 0; i < g_clientsPtrCpy.Count; i++) // don't use LINQ.ForEach(), but use foreach(), or the 25% faster for
                {
                    DashboardClient client = g_clientsPtrCpy[i];
                    byte[] encodedMsg = Encoding.UTF8.GetBytes("QckfNews.CommonNews:" + Utils.CamelCaseSerialize(g_commonNews));
                    if (client.WsWebSocket!.State == WebSocketState.Open)
                        client.WsWebSocket.SendAsync(new ArraySegment<Byte>(encodedMsg, 0, encodedMsg.Length), WebSocketMessageType.Text, true, CancellationToken.None);

                    byte[] encodedMsgRss = Encoding.UTF8.GetBytes("QckfNews.StockNews:" + Utils.CamelCaseSerialize(g_stockNews));
                    if (encodedMsgRss != null && client.WsWebSocket!.State == WebSocketState.Open)
                        client.WsWebSocket.SendAsync(new ArraySegment<Byte>(encodedMsgRss, 0, encodedMsgRss.Length), WebSocketMessageType.Text, true, CancellationToken.None);
                    }
            }
            else
                isQckflNewsTimerRunning = false;

            lock (m_qckflNewsTimerLock)
            {
                if (isQckflNewsTimerRunning)
                {
                    Utils.Logger.Info("QckflNewsTimer_Elapsed(). We restart timer.");
                    m_qckflNewsTimer.Change(TimeSpan.FromMilliseconds(m_qckflNewsTimerFrequencyMs), TimeSpan.FromMilliseconds(-1.0)); // runs only once. To avoid that it runs parallel, if first one doesn't finish
                }
            }
        }
        catch (Exception e)
        {
            Utils.Logger.Error(e, "QckflNewsTimer_Elapsed() exception.");
            throw;
        }
    }
    public bool OnReceiveWsAsync_QckflNews(string msgCode, string msgObjStr)
    {
        switch (msgCode)
        {
            case "QckflNews.ReloadQuickfolio":
                Utils.Logger.Info($"OnReceiveWsAsync_QckflNews(): QckflNews.ReloadQuickfolio:{msgObjStr}");
                m_stockTickers = GetQckflStockTickers() ?? Array.Empty<string>();
                return true;
            default:
                return false;
        }
    }

    // private List<NewsItem> GetQckflStockNewsTipranks()
    // {
    // static readonly Random g_random = new(DateTime.Now.Millisecond);
    // static readonly KeyValuePair<int, int> g_sleepBetweenDnsMs = new(2000, 1000); // <fix, random>
    //     List<NewsItem> foundNewsItems = new();
    //     if (foundNewsItems == null)
    //         foundNewsItems = new List<NewsItem>();
    //     //MakeRequests();
    //     foreach (string ticker in m_stockTickers)
    //     {
    //         string url = string.Format(@"https://www.tipranks.com/api/stocks/getNews/?ticker={0}", ticker);
    //         string? webpageData = Utils.DownloadStringWithRetryAsync(url).TurnAsyncToSyncTask();
    //         System.Threading.Thread.Sleep(m_sleepBetweenDnsMs.Key + m_random.Next(m_sleepBetweenDnsMs.Value));  // to avoid that server bans our IP
    //         if (webpageData != null)
    //         {
    //             ReadTipranksNewsItems(foundNewsItems, ticker, webpageData);
    //         }
    //     }
    //     return foundNewsItems;
    // }
    // private void ReadTipranksNewsItems(List<NewsItem> p_foundNewsItems, string p_ticker, string webpageData)
    // {
    //     try
    //     {
    //         // var jsonOptions = new System.Text.Json.JsonSerializerOptions
    //         // {
    //         //     AllowTrailingCommas = true
    //         // };
    //         // NewsItem jsonObject =  System.Text.Json.JsonSerializer.Deserialize<NewsItem>(webpageData, jsonOptions);

    // System.Text.Json.JsonDocument document = System.Text.Json.JsonDocument.Parse(webpageData);
    //         JsonElement root = document.RootElement;
    //         JsonElement newssElement = root.GetProperty("news");
    //         foreach (JsonElement news in newssElement.EnumerateArray())
    //         {
    //             NewsItem newsItem = new();
    //             newsItem.Ticker = p_ticker;
    //             newsItem.LinkUrl = news.GetProperty("url").GetRawText().Trim('"');
    //             newsItem.Title = news.GetProperty("title").GetRawText().Trim('"');
    //             newsItem.Summary = "  ";
    //             newsItem.Sentiment = news.GetProperty("sentiment").GetRawText().Trim('"');
    //             if (DateTime.TryParse(news.GetProperty("articleTimestamp").GetRawText().Trim('"'), out DateTime date))
    //                 newsItem.PublishDate = date;
    //             newsItem.DownloadTime = DateTime.Now;
    //             newsItem.Source = NewsSource.TipRanks.ToString();

    // // if (AddNewsToMemory(p_ticker, newsItem))
    //             p_foundNewsItems.Add(newsItem);
    //         }
    //     }
    //     catch (Exception)
    //     {
    //         DateTime.Today.AddDays(1);
    //     }
    // }
    //  private List<NewsItem> GetQckflStockNewsBenzinga()
    // {
    //     List<NewsItem> foundNewsItems = new();
    //     foreach (string ticker in m_stockTickers)
    //     {
    //         string url = string.Format(@"https://www.benzinga.com/stock/{0}", ticker);
    //         string? webpageData = Utils.DownloadStringWithRetryAsync(url).TurnAsyncToSyncTask();
    //         System.Threading.Thread.Sleep(m_sleepBetweenDnsMs.Key + m_random.Next(m_sleepBetweenDnsMs.Value));  // to avoid that server bans our IP
    //         if (webpageData != null)
    //         {
    //             ReadBenzingaSection(foundNewsItems, ticker, webpageData, "headlines");
    //             ReadBenzingaSection(foundNewsItems, ticker, webpageData, "press");
    //         }
    //     }
    //     return foundNewsItems;
    // }

    // private void ReadBenzingaSection(List<NewsItem> p_foundNewsItems, string p_ticker, string p_webpageData, string p_keyWord)
    // {
    //     Regex regexBenzingaLists = new(@"<div[^>]*?class=""stories""[^>]*?" + p_keyWord + @"(?<CONTENT>(\s|\S)*?)</div>"
    //         , RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
    //     Regex regexBenzingaNews = new(@"<li(\s|\S)*?class=""story""(\s|\S)*?<a href=""(?<LINK>[^""]*)"">(?<TITLE>[^<]*)<(\s|\S)*?<span class=""date"">(?<DATE>[^<]*)"
    //         , RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
    //     MatchCollection matches = regexBenzingaLists.Matches(p_webpageData);
    //     if (matches == null)
    //         return;
    //     for (int index = 0; index < matches.Count; index++)
    //     {
    //         Match match = matches[index];
    //         MatchCollection matchesNews = regexBenzingaNews.Matches(match.Groups["CONTENT"].Value);
    //         for (int indexNews = 0; indexNews < matchesNews.Count; indexNews++)
    //         {
    //             Match matchNews = matchesNews[indexNews];
    //             NewsItem newsItem = new();
    //             newsItem.Ticker = p_ticker;
    //             newsItem.LinkUrl = matchNews.Groups["LINK"].Value;
    //             newsItem.Title = WebUtility.HtmlDecode(matchNews.Groups["TITLE"].Value);
    //             newsItem.Summary = "  ";
    //             newsItem.PublishDate = GetNewsDate(matchNews.Groups["DATE"].Value);
    //             newsItem.DownloadTime = DateTime.Now;
    //             newsItem.Source = NewsSource.Benzinga.ToString();

    // if (AddNewsToMemory(p_ticker, newsItem))
    //                 p_foundNewsItems.Add(newsItem);
    //         }
    //     }
    // }
    // private DateTime GetNewsDate(string p_dateString)
    // {
    //     if (DateTime.TryParse(p_dateString, out DateTime date))
    //         return date;
    //     p_dateString = p_dateString.ToUpper();
    //     if (p_dateString.Contains("AGO"))
    //     {
    //         p_dateString = p_dateString.Replace("AGO", string.Empty).Trim();
    //         if (p_dateString.Contains("HOUR"))
    //         {
    //             p_dateString = p_dateString.Replace("HOURS", string.Empty).Replace("HOUR", string.Empty).Trim();
    //             if (int.TryParse(p_dateString, out int hours))
    //                 return DateTime.Now.AddHours(-hours);
    //         }
    //         else if (p_dateString.Contains("DAY"))
    //         {
    //             p_dateString = p_dateString.Replace("DAYS", string.Empty).Replace("DAY", string.Empty).Trim();
    //             if (int.TryParse(p_dateString, out int days))
    //                 return DateTime.Now.AddDays(-days);
    //         }
    //         else if (p_dateString.Contains("MIN"))
    //         {
    //             p_dateString = p_dateString.Replace("MINUTES", string.Empty).Replace("MIN", string.Empty).Replace("MINS", string.Empty).Trim();
    //             if (int.TryParse(p_dateString, out int days))
    //                 return DateTime.Now.AddDays(-days);
    //         }
    //     }
    //     return DateTime.Now;
    // }
    // private string NewsToString(List<NewsItem> newsList)
    // {
    //     string finalString = string.Empty;
    //     foreach (NewsItem news in newsList.OrderBy(x => x.PublishDate))
    //         finalString += string.Format(@"news_ticker{0}news_title{1}news_summary{2}news_link{3}news_downloadTime{4:yyyy-MM-dd hh:mm}news_publishDate{5:yyyy-MM-dd hh:mm}news_source{6}news_end",
    //             news.Ticker, news.Title, news.Summary, news.LinkUrl, news.DownloadTime, news.PublishDate, news.Source);
    //     return finalString;
    // }

    // private void AddFoundNews(int p_stockID, List<NewsItem> p_foundNewsItems)
    // {
    //     // List<NewsItem> notYetKnownNews = new List<NewsItem>();
    //     // if (!m_newsMemory.ContainsKey(p_stockID))
    //     //     m_newsMemory.Add(p_stockID, new List<NewsItem>());
    //     // foreach (NewsItem newsItem in p_foundNewsItems)
    //     //     if (m_newsMemory[p_stockID].Where(x => x.LinkUrl.Equals(newsItem.LinkUrl)).Count() == 0)    // not yet known
    //     //     {
    //     //         m_newsMemory[p_stockID].Add(newsItem);
    //     //         notYetKnownNews.Add(newsItem);
    //     //     }
    // }
}