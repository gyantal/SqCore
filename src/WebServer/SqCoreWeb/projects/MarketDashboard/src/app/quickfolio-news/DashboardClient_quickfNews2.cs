// using System;
// using System.Threading;
// using SqCommon;
// using System.Collections.Generic;
// using System.Text;
// using System.Net.WebSockets;
// using System.Threading.Tasks;
// using System.Linq;
// using System.Xml.Linq;
// using System.ServiceModel.Syndication;
// using System.Net;
// using System.Diagnostics;

// namespace SqCoreWeb
// {
//     public enum NewsSource
//     {
//         YahooRSS,
//         CnbcRss,
//         Benzinga,
//         TipRanks
//     }
//     public class NewsItem
//     {
//         public string Ticker { get; set; } = string.Empty;
//         public string Title { get; set; } = string.Empty;
//         public string Summary { get; set; } = string.Empty;
//         public string LinkUrl { get; set; } = string.Empty;
//         public DateTime DownloadTime { get; set; }
//         public DateTime PublishDate { get; set; }
//         public string Source { get; set; } = string.Empty;
//         public string DisplayText { get; set; } = string.Empty;
//         public string Sentiment { get; set; } = string.Empty;
//     }

//     public partial class DashboardClient
//     {
//         // static readonly QuickfolioNewsDownloader g_newsDownloader = new(); // only 1 global downloader for all clients
//         // one global static quickfolio News Timer serves all clients. For efficiency.
//         static readonly Timer m_qckflNewsTimer = new(new TimerCallback(QckflNewsTimer_Elapsed), null, TimeSpan.FromMilliseconds(-1.0), TimeSpan.FromMilliseconds(-1.0));
//         static bool isQckflNewsTimerRunning = false;
//         static readonly object m_qckflNewsTimerLock = new();
//         static readonly int m_qckflNewsTimerFrequencyMs = 15 * 60 * 1000; // timer for 15 minutes
//         static readonly TimeSpan c_initialSleepIfNotActiveToolQn2 = TimeSpan.FromMilliseconds(10 * 1000); // 10sec
//         static List<NewsItem> g_commonNews = new();

//         static List<NewsItem> g_stockNews = new();

//         static string[] m_stockTickers2 = Array.Empty<string>();

//         public void OnConnectedWsAsync_QckflNews(bool p_isThisActiveToolAtConnectionInit)
//         {
//             Utils.RunInNewThread(ignored => // running parallel on a ThreadPool thread, FireAndForget: QueueUserWorkItem [26microsec] is 25% faster than Task.Run [35microsec]
//             {
//                 Thread.CurrentThread.IsBackground = true; //  thread will be killed when all foreground threads have died, the thread will not keep the application alive.

//                 // Assuming this tool is not the main Tab page on the client, we delay sending all the data, to avoid making the network and client too busy an unresponsive
//                 if (!p_isThisActiveToolAtConnectionInit)
//                     Thread.Sleep(DashboardClient.c_initialSleepIfNotActiveToolQn2); // 10 sec is quite a long waiting, but we rarely use this tool.
                
//                 if (m_stockTickers2.Length == 0)
//                     m_stockTickers2 = GetQckflStockTickers() ?? Array.Empty<string>();
                
//                 byte[] encodedMsg = Encoding.UTF8.GetBytes("QckfNews.Tickers:" + Utils.CamelCaseSerialize(new List<string> { "All assets" }.Union(m_stockTickers2).ToList()));
//                 if (WsWebSocket!.State == WebSocketState.Open)
//                     WsWebSocket.SendAsync(new ArraySegment<Byte>(encodedMsg, 0, encodedMsg.Length), WebSocketMessageType.Text, true, CancellationToken.None);

//                 if (g_commonNews.Count > 0)
//                 {
//                     Utils.Logger.Info("OnConnectedAsync_QckflNews(). common news already downloaded.");

//                     byte[] encodedMsgCommonNews = Encoding.UTF8.GetBytes("QckfNews.CommonNews:" + Utils.CamelCaseSerialize(g_commonNews));
//                     if (WsWebSocket!.State == WebSocketState.Open)
//                         WsWebSocket.SendAsync(new ArraySegment<Byte>(encodedMsgCommonNews, 0, encodedMsgCommonNews.Length), WebSocketMessageType.Text, true, CancellationToken.None);
//                 }
//                 if (g_stockNews.Count > 0)
//                 {
//                     Utils.Logger.Info("OnConnectedAsync_QckflNews(). stock news already downloaded.");

//                     byte[] encodedMsgCommonNews = Encoding.UTF8.GetBytes("QckfNews.StockNews:" + Utils.CamelCaseSerialize(g_stockNews));
//                     if (WsWebSocket!.State == WebSocketState.Open)
//                         WsWebSocket.SendAsync(new ArraySegment<Byte>(encodedMsgCommonNews, 0, encodedMsgCommonNews.Length), WebSocketMessageType.Text, true, CancellationToken.None);
//                 }
//                 lock (m_qckflNewsTimerLock)
//                 {
//                     if (!isQckflNewsTimerRunning)
//                     {
//                         Utils.Logger.Info("OnConnectedAsync_QckflNews(). Starting m_qckflNewsTimer.");
//                         isQckflNewsTimerRunning = true;
//                         m_qckflNewsTimer.Change(System.TimeSpan.Zero, TimeSpan.FromMilliseconds(-1.0));    // runs only once. To avoid that it runs parallel, if first one doesn't finish
//                     }
//                 }
//             });
//         }
//         public static string[]? GetQckflStockTickers()
//         {
//             string? valuesFromGSheetStr = "Error. Make sure GoogleApiKeyKey, GoogleApiKeyKey is in SQLab.WebServer.SQLab.NoGitHub.json !";
//             if (!String.IsNullOrEmpty(Utils.Configuration["Google:GoogleApiKeyName"]) && !String.IsNullOrEmpty(Utils.Configuration["Google:GoogleApiKeyKey"]))
//             {
//                 valuesFromGSheetStr = Utils.DownloadStringWithRetryAsync("https://sheets.googleapis.com/v4/spreadsheets/1c5ER22sXDEVzW3uKthclpArlZvYuZd6xUffXhs6rRsM/values/A1%3AA1?key=" + Utils.Configuration["Google:GoogleApiKeyKey"]).TurnAsyncToSyncTask();
//                 if (valuesFromGSheetStr == null)
//                     valuesFromGSheetStr = "Error in DownloadStringWithRetry().";
//             }
//             if (!valuesFromGSheetStr.StartsWith("Error")) 
//             {
//                 int pos = valuesFromGSheetStr.IndexOf(@"""values"":");
//                 if (pos < 0)
//                     return null;
//                 valuesFromGSheetStr = valuesFromGSheetStr[(pos + 9)..]; // cut off until the end of "values":
//                 int posStart = valuesFromGSheetStr.IndexOf(@"""");
//                 if (posStart < 0)
//                     return null;
//                 int posEnd = valuesFromGSheetStr.IndexOf(@"""", posStart + 1);
//                 if (posEnd < 0)
//                     return null;
//                 string cellValue = valuesFromGSheetStr.Substring(posStart + 1, posEnd - posStart - 1);
//                 return cellValue.Split(',').Select(x => x.Trim()).ToArray();
//             }
//             else
//                 return null;
//         }
//         public static List<NewsItem> GetQckflCommonNews()
//         {
//             string rssFeedUrl = string.Format(@"https://www.cnbc.com/id/100003114/device/rss/rss.html");

//             List<NewsItem> foundNewsItems = new(ReadRSSAsync(rssFeedUrl, NewsSource.CnbcRss, string.Empty));
//             return foundNewsItems;
           
//         }

//         public static List<NewsItem> ReadRSSAsync(string p_url, NewsSource p_newsSource, string p_ticker)
//         {
//             try
//             {
//                 string? rssFeedAsString = Utils.DownloadStringWithRetryAsync(p_url, 3, TimeSpan.FromSeconds(5), true).TurnAsyncToSyncTask();
//                 if (String.IsNullOrEmpty(rssFeedAsString))
//                 {
//                     Console.WriteLine($"QuickfolioNewsDownloader.ReadRSS() url download failed.");
//                     return new List<NewsItem>();
//                 }

//                 // convert feed to XML using LINQ to XML and finally create new XmlReader object
//                 var feed = System.ServiceModel.Syndication.SyndicationFeed.Load(XDocument.Parse(rssFeedAsString).CreateReader());

//                 List<NewsItem> foundNews = new();

//                 foreach (SyndicationItem item in feed.Items)
//                 {
//                     NewsItem newsItem = new();
//                     newsItem.Ticker = p_ticker;
//                     newsItem.LinkUrl = item.Links[0].Uri.AbsoluteUri;
//                     newsItem.Title = WebUtility.HtmlDecode(item.Title.Text);
//                     newsItem.Summary = WebUtility.HtmlDecode(item.Summary?.Text ?? string.Empty); // <description> is missing sometimes, so Summary = null
//                     newsItem.PublishDate = item.PublishDate.LocalDateTime;
//                     newsItem.DownloadTime = DateTime.UtcNow;
//                     newsItem.Source = p_newsSource.ToString();
//                     newsItem.DisplayText = string.Empty;
//                     //newsItem.setFiltered();
//                     // we might filter news and bring Laszlo's bool SkipNewsItem(string p_title) here. Later. Not now.
//                     foundNews.Add(newsItem);
//                 }
//                 return foundNews;
//             }
//             catch (Exception exception)
//             {
//                 Console.WriteLine($"QuickfolioNewsDownloader.ReadRSS() exception: '{exception.Message}'");
//                 return new List<NewsItem>();
//             }
//         }
//         public static List<NewsItem> GetQckflStockNews() // with 13 tickers, it can take 13 * 2 = 26seconds
//         {
//             List<NewsItem> foundStockNews = new();
//             foreach (string ticker in m_stockTickers2)
//             {
//                 string rssFeedUrl = string.Format(@"https://feeds.finance.yahoo.com/rss/2.0/headline?s={0}&region=US&lang=en-US", ticker);
//                 var stockNews = ReadRSSAsync(rssFeedUrl, NewsSource.YahooRSS, ticker);
//                 for (int i = 0; i < stockNews.Count; i++)
//                 {
//                     foundStockNews.Add(stockNews[i]);
//                 }
//             }
//             return foundStockNews;
//         }
//         public static void QckflNewsTimer_Elapsed(object? state) // Timer is coming on a ThreadPool thread
//         {
//             try
//             {
//                 Utils.Logger.Debug("QckflNewsTimer_Elapsed(). BEGIN");
//                 if (!isQckflNewsTimerRunning)
//                     return; // if it was disabled by another thread in the meantime, we should not waste resources to execute this.

//                 var g_clientsPtrCpy = DashboardClient.g_clients; // Multithread warning! Lockfree Read | Copy-Modify-Swap Write Pattern
//                 if (g_clientsPtrCpy.Count > 0)
//                 {
//                     g_commonNews = GetQckflCommonNews();
//                     g_stockNews = GetQckflStockNews();
//                     for (int i = 0; i < g_clientsPtrCpy.Count; i++) // don't use LINQ.ForEach(), but use foreach(), or the 25% faster for
//                     {
//                         DashboardClient client = g_clientsPtrCpy[i];
//                         byte[] encodedMsg = Encoding.UTF8.GetBytes("QckfNews.CommonNews:" + Utils.CamelCaseSerialize(g_commonNews));
//                         if (client.WsWebSocket!.State == WebSocketState.Open)
//                             client.WsWebSocket.SendAsync(new ArraySegment<Byte>(encodedMsg, 0, encodedMsg.Length), WebSocketMessageType.Text, true, CancellationToken.None);
                        
//                         byte[] encodedMsgRss = Encoding.UTF8.GetBytes("QckfNews.StockNews:" + Utils.CamelCaseSerialize(g_stockNews));
//                         if (encodedMsgRss != null && client.WsWebSocket!.State == WebSocketState.Open)
//                             client.WsWebSocket.SendAsync(new ArraySegment<Byte>(encodedMsgRss, 0, encodedMsgRss.Length), WebSocketMessageType.Text, true, CancellationToken.None);
//                      }
//                 }
//                 else
//                     isQckflNewsTimerRunning = false;

//                 lock (m_qckflNewsTimerLock)
//                 {
//                     if (isQckflNewsTimerRunning)
//                     {
//                         Utils.Logger.Info("QckflNewsTimer_Elapsed(). We restart timer.");
//                         m_qckflNewsTimer.Change(TimeSpan.FromMilliseconds(m_qckflNewsTimerFrequencyMs), TimeSpan.FromMilliseconds(-1.0)); // runs only once. To avoid that it runs parallel, if first one doesn't finish
//                     }
//                 }
//             }
//             catch (Exception e)
//             {
//                 Utils.Logger.Error(e, "QckflNewsTimer_Elapsed() exception.");
//                 throw;
//             }
//         }
//         public bool OnReceiveWsAsync_QckflNews(string msgCode, string msgObjStr)
//         {
//             switch (msgCode)
//             {
//                 case "QckflNews.ReloadQuickfolio":
//                     Utils.Logger.Info($"OnReceiveWsAsync_QckflNews(): QckflNews.ReloadQuickfolio:{msgObjStr}");
//                     // ReloadQuickfolioMsgArrived();
//                     return true;
//                 default:
//                     return false;
//             }
//         }

//     }
// }