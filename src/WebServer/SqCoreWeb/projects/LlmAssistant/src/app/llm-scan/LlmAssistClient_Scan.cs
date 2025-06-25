using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using Azure; // API uses Azure.Response class
using Azure.AI.OpenAI;
using SqCommon;
using YahooFinanceApi;

namespace SqCoreWeb;
public class TickerEarningsDate
{
    public string Ticker { get; set; } = string.Empty;
    public string EarningsDateStr { get; set; } = string.Empty;
}
public partial class LlmAssistClient
{
    public void GetStockPrice(string p_tickersStr)
    {
        List<string> logs = new();
        if (p_tickersStr == null)
        {
            logs.Add("Error: Invalid data");
            return;
        }

        List<StockPriceData> response = new();
        string[] tickers = p_tickersStr.Split(',', StringSplitOptions.RemoveEmptyEntries);
        try
        {
            response = DownloadStockPriceData(tickers).TurnAsyncToSyncTask();
        }
        catch (System.Exception e)
        {
            logs.Add($"Error: {e.Message}");
        }

        ServerStockPriceDataResponse serverResponse = new() { StocksPriceResponse = response, Logs = logs };
        byte[] encodedMsg = Encoding.UTF8.GetBytes("StockPrice:" + JsonSerializer.Serialize(serverResponse));
        if (WsWebSocket!.State == WebSocketState.Open)
            WsWebSocket.SendAsync(new ArraySegment<Byte>(encodedMsg, 0, encodedMsg.Length), WebSocketMessageType.Text, true, CancellationToken.None);
        GetEarningsDate(tickers);
    }

    public static async Task<List<StockPriceData>> DownloadStockPriceData(string[] p_tickers)
    {
        List<StockPriceData> tickerPos = new(p_tickers.Length);
        try
        {
            Console.WriteLine("Calling YF for price... (2023-12: The first time to get the cookie takes 20sec)");
            IReadOnlyDictionary<string, Security> quotes = await Yahoo.Symbols(p_tickers).Fields(new Field[] { Field.Symbol, Field.RegularMarketPreviousClose, Field.RegularMarketPrice, Field.RegularMarketChange, Field.RegularMarketChangePercent }).QueryAsync();
            foreach (var val in quotes.Values)
            {
                tickerPos.Add(new StockPriceData() { Ticker = val.Symbol, PriorClose = val.RegularMarketPreviousClose, LastPrice = val.RegularMarketPrice, PercentChange = val.RegularMarketChangePercent });
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error while downloading price data {p_tickers}: {ex.Message}");
        }
        return tickerPos;
    }

    public void GetEarningsDate(string[] p_tickers) // e.g, p_tickers : [AAPL, TSLA]
    {
        List<TickerEarningsDate> tickerEarningDateList = new();
        foreach (string ticker in p_tickers)
        {
            string url = $"https://finance.yahoo.com/quote/{ticker}"; // p_inMsg.Msg is ticker(AAPL), https://finance.yahoo.com/quote/AAPL.
            string? htmlContent = Utils.DownloadStringWithRetryAsync(url).TurnAsyncToSyncTask();
            if (htmlContent == null)
                throw new SqException($"DownloadStringWithRetryAsync failed for ticker {ticker}");
            string responseDateStr = ProcessHtmlContentForEarningsDate(htmlContent);
            tickerEarningDateList.Add(new TickerEarningsDate { Ticker = ticker, EarningsDateStr = responseDateStr });
        }
        byte[] encodedMsg = Encoding.UTF8.GetBytes("EarningsDate:" + JsonSerializer.Serialize(tickerEarningDateList));
        if (WsWebSocket!.State == WebSocketState.Open)
            WsWebSocket.SendAsync(new ArraySegment<Byte>(encodedMsg, 0, encodedMsg.Length), WebSocketMessageType.Text, true, CancellationToken.None);
    }

    public static string ProcessHtmlContentForEarningsDate(string p_html)
    {
        string earningsDate = string.Empty;
        int earningsDateStartPos = p_html.IndexOf(">Earnings Date<"); // Find the position of ">Earnings Date<"
        if (earningsDateStartPos == -1)
        {
            Console.WriteLine("Cannot find Earnings Date. Stop processing.");
            return earningsDate;
        }

        ReadOnlySpan<char> htmlSpan = p_html.AsSpan(earningsDateStartPos); // Extract the substring starting from the position of "Earnings Date"
        // As of 2025-03-11 , it was observed that the Earnings Date was not displayed on the UI.
        // The HTML structure for finding the Earnings Date, previously using "<span class=\"value svelte-tx3nkj\">", has changed to "<span class=\"value yf-1jj98ts\">".
        int spanEarningsDateStartPos = htmlSpan.IndexOf("<span class=\"value yf-1jj98ts\">"); // Find the start position of the value span after "Earnings Date"
        if (spanEarningsDateStartPos == -1)
        {
            Console.WriteLine("Cannot find value <span class=\"value yf-1jj98ts\"> after Earnings Date. Stop processing.");
            return earningsDate;
        }

        spanEarningsDateStartPos += "<span class=\"value yf-1jj98ts\">".Length; // Calculate the start position of the actual earnings date text
        ReadOnlySpan<char> htmlBodySpan = htmlSpan.Slice(spanEarningsDateStartPos);
        int spanEarningsDateEndPos = htmlBodySpan.IndexOf("</span> </li>"); // Find the end position of the earnings date text
        if (spanEarningsDateEndPos == -1)
        {
            Console.WriteLine("Cannot find end of value </span> </li>. Stop processing.");
            return earningsDate;
        }

        ReadOnlySpan<char> earningsDateSpan = htmlBodySpan.Slice(0, spanEarningsDateEndPos); // Extract the earnings date text
        earningsDate = earningsDateSpan.ToString();

        return earningsDate;
    }

    public void GetTickerNews(string p_tickerStr)
    {
        List<TickerNews> tickerNewss = new();
        // var analyzer = new SentimentIntensityAnalyzer(); // Sentiment analysis
        try
        {
            string url = $"https://feeds.finance.yahoo.com/rss/2.0/headline?s={p_tickerStr}";
            string? xmlContent = Utils.DownloadStringWithRetryAsync(url).TurnAsyncToSyncTask();
            if (xmlContent == null)
                throw new SqException($"DownloadStringWithRetryAsync failed for ticker {p_tickerStr}");
            Rss rss = ParseXMLString(xmlContent);

            List<LlmNewsItem> newsItems = new(); // local newsItems list
            if (rss?.Channel?.NewsItems != null)
            {
                DateTime utcNow = DateTime.UtcNow;
                // Iterate through NewsItems and add items within one week
                foreach (var newsItem in rss.Channel.NewsItems)
                {
                    DateTime pubDate = Utils.Str2DateTimeUtc(newsItem.PubDate);
                    int nDays = (int)(utcNow - pubDate).TotalDays; // Calculate the number of days
                    if (nDays <= 7) // Add the news item to the local list if within one week
                    {
                        // var result = analyzer.PolarityScores(newsItem.Description);
                        // newsItem.ShortDescriptionSentiment = (float)result.Compound;
                        newsItems.Add(newsItem);
                    }
                }
            }
            tickerNewss.Add(new TickerNews() { Ticker = p_tickerStr, NewsItems = newsItems });
            // Here you would do something with the xmlContent, e.g., save to a file or process it.
            Console.WriteLine($"Downloaded XML content for {p_tickerStr}");
        }
        catch (HttpRequestException ex)
        {
            Console.WriteLine($"Error downloading XML for {p_tickerStr}: {ex.Message}");
        }

        // Check if the news item can be summarized using LLM tools
        foreach (var tickerNews in tickerNewss)
        {
            foreach (var newsItem in tickerNews.NewsItems!)
            {
                string isLikely = GetIsLlmSummaryLikely(newsItem.Link);
                newsItem.IsGptSummaryLikely = isLikely;
            }
        }
        byte[] encodedMsg = Encoding.UTF8.GetBytes("TickersNews:" + JsonSerializer.Serialize(tickerNewss));
        if (WsWebSocket!.State == WebSocketState.Open)
            WsWebSocket.SendAsync(new ArraySegment<Byte>(encodedMsg, 0, encodedMsg.Length), WebSocketMessageType.Text, true, CancellationToken.None);
    }

    // XmlReader is the fastest way to walk-forward an XML. A general reader of strings. A walk-forward one-way.
    // XmlDocument is the slowest, because it keeps everything in RAM.
    // XmlSerializer<MyClass> suffers from long initial cost (30-70ms), because at first run it creates a virtual DLL with the generated classes for that MyClass
    public static Rss ParseXMLString(string xmlString)
    {
        Rss rss = new Rss();
        rss.Channel = new Channel();
        rss.Channel.NewsItems = new List<LlmNewsItem>();
        LlmNewsItem? currentItem = null;

        using (StringReader stringReader = new StringReader(xmlString))
        using (XmlReader reader = XmlReader.Create(stringReader))
        {
            while (reader.Read())
            {
                if (reader.IsStartElement())
                {
                    switch (reader.Name)
                    {
                        case "rss":
                            rss.Version = reader["version"] ?? "Unknown";
                            break;
                        case "channel":
                            // Assuming channel is only once at the start
                            break;
                        case "title":
                            if (reader.Read())
                            {
                                if (currentItem == null)
                                {
                                    if (reader.Depth == 3) // Detecting if this belongs to channel or image
                                        rss.Channel.Title = reader.Value.Trim();
                                    else if (reader.Depth == 4)
                                        rss.Channel.Image!.Title = reader.Value.Trim();
                                    else
                                        throw new Exception("Unexpected title depth");
                                }
                                else
                                    currentItem.Title = reader.Value.Trim();
                            }
                            break;
                        case "link":
                            if (reader.Read())
                            {
                                if (currentItem == null)
                                {
                                    if (reader.Depth == 3) // Detecting if this belongs to channel or image
                                        rss.Channel.Link = reader.Value.Trim();
                                    else if (reader.Depth == 4)
                                        rss.Channel.Image!.Link = reader.Value.Trim();
                                    else
                                        throw new Exception("Unexpected link depth");
                                }
                                else
                                    currentItem.Link = reader.Value.Trim();
                            }
                            break;
                        case "description":
                            if (reader.Read())
                            {
                                if (currentItem == null)
                                    rss.Channel.Description = reader.Value.Trim();
                                else
                                    currentItem.Description = reader.Value.Trim();
                            }
                            break;
                        case "language":
                            if (reader.Read())
                            {
                                rss.Channel.Language = reader.Value.Trim();
                            }
                            break;
                        case "lastBuildDate":
                            if (reader.Read())
                            {
                                rss.Channel.LastBuildDate = reader.Value.Trim();
                            }
                            break;
                        case "copyright":
                            if (reader.Read())
                            {
                                rss.Channel.Copyright = reader.Value.Trim();
                            }
                            break;
                        case "image":
                            rss.Channel.Image = new Image();
                            break;
                        case "url":
                            if (reader.Read())
                            {
                                rss.Channel.Image!.Url = reader.Value.Trim();
                            }
                            break;
                        case "width":
                            if (reader.Read())
                            {
                                rss.Channel.Image!.Width = int.Parse(reader.Value.Trim());
                            }
                            break;
                        case "height":
                            if (reader.Read())
                            {
                                rss.Channel.Image!.Height = int.Parse(reader.Value.Trim());
                            }
                            break;
                        case "item":
                            currentItem = new LlmNewsItem();
                            rss.Channel.NewsItems.Add(currentItem);
                            break;
                        case "guid":
                            // Item currentItem = rss.Channel.Items[^1];  // Get the last item
                            // currentItem.Guid = new GuidElement();
                            // currentItem.Guid.IsPermaLink = reader["isPermaLink"];
                            if (reader.Read())
                            {
                                currentItem!.Guid = reader.Value.Trim();
                            }
                            break;
                        case "pubDate":
                            if (reader.Read())
                            {
                                currentItem!.PubDate = reader.Value.Trim();
                            }
                            break;
                            // Add cases for other elements like pubDate, item/title, etc.
                    }
                }
                else // so, it is an EndElement, such as </item>
                {
                    switch (reader.Name)
                    {
                        case "item":
                            currentItem = null;
                            break;
                    }
                }
            }
        }

        return rss;
    }

    public static string GetIsLlmSummaryLikely(string p_newsUrl)
    {
        (string newsStr, string userMsg) = DownloadCompleteNews(p_newsUrl);
        string responseStr;
        if (!String.IsNullOrEmpty(newsStr) && String.IsNullOrEmpty(userMsg)) // when newsStr is given correctly, we expect there is no userMsg with warnings.
            responseStr = "yes";
        else
            responseStr = "no";
        return responseStr;
    }

    public static (string ResponseStr, string UserMsg) DownloadCompleteNews(string p_newsUrl) // returns newsStr , UserMsg. When newsStr is given correctly, we expect there is no userMsg with warnings.
    {
        string responseStr;
        string? htmlContent = Utils.DownloadStringWithRetryAsync(p_newsUrl).TurnAsyncToSyncTask();
        if (htmlContent == null)
            throw new SqException($"DownloadStringWithRetryAsync failed for Url {p_newsUrl}");

        if ((p_newsUrl.StartsWith("https://finance.yahoo.com") || p_newsUrl.StartsWith("https://uk.finance.yahoo.com") || p_newsUrl.StartsWith("https://ca.finance.yahoo.com/")) && !htmlContent.Contains("Continue reading", StringComparison.OrdinalIgnoreCase)) // if the YF news on YF website has "Continue reading" then a link will lead to another website (Bloomberg, Fools), in that case we don't process it.
        {
            // responseStr = ProcessHtmlContentRegex(htmlContent);
            string responseHtmlStr = ProcessHtmlContentFast(htmlContent);
            // Postprocessing HTML for cleaning up the string to get the 'clean' news text
            responseStr = responseHtmlStr.Replace("&#39;", "'").Replace("&quot;", "\""); // Native HTML formatting converted back to text. "Europe&#39;s" => "Europe's", "&quot;" => "
            // Future postprocessing maybe. But at the moment, ChatGpt can summarize well even though text contains rubbish parts.
            // E.g. responseStr still have some rubbish: "Most Read from Bloomberg" text twice, and there are non-relevant 5 <A> tags with random news.
            // The problem is that any cleaning is a moving target, as YF changes the layout every once in a while
        }
        else
            return (string.Empty, $"The full news isn't accessible on Yahoo Finance. I recommend visiting this <a href={p_newsUrl}>link</a> to directly retrieve the summary from ChatGPT.");

        return (responseStr, string.Empty);
    }

    // As of 2024-06-10, the method to find the index was based on "</p><div>", but it stopped working because the HTML format changed to "</p><div id="view-cmts-cta-d9f6eab7-b4bb-3acb-b8d4-6b234d1fc821" class="view-cmts-cta-wrapper">". Interestingly, the id is unique for each news article so using "class=view-cmts-cta-wrapper>" to find the index.
    // As of 2025-04-28, method for finding the news content using the "class=view-cmts-cta-wrapper>" has stopped.
    // Previously, content was extracted using "class=view-cmts-cta-wrapper" has changed to "class=atoms-wrapper", and multiple "atoms-wrapper" blocks can exist in a single URL.
    // The method is modified to iterate through each "atoms-wrapper" block and extract the content within the <p class="yf-1090901"> tags inside each block.
    // e.g, the news is wrapped as <div class=atoms-wrapper><p class=yf-1090901>Some news </p></div>
    // On 2025-06-24, it was noticed that yahoo finance has changed the structure of there news articles. Earlier the content was clean and wrapped only in <p> tags, with no interfering tags like <figure> or <div>.
    // This change broke our parsing logic, so we now extract content by targeting <p> tags using a class selector (<p class="yf-1090901">) for news items.
    static string ProcessHtmlContentFast(string p_html)
    {
        Stopwatch sw = new();
        sw.Start();
        StringBuilder sb = new();
        ReadOnlySpan<char> htmlSpan = p_html.AsSpan();
        string pTagClassSelectorStr = "<p class=\"yf-1090901\"";
        string pTagClose = "</p>";
        int currentIdx = 0;
        while (true)
        {
            int pTagStartIdx = htmlSpan.Slice(currentIdx).IndexOf(pTagClassSelectorStr, StringComparison.OrdinalIgnoreCase);
            if (pTagStartIdx == -1)
                break;
            currentIdx += pTagStartIdx;

            int pTagStartEndIdx = htmlSpan.Slice(currentIdx).IndexOf('>');
            if (pTagStartEndIdx == -1)
                break;

            int contentStartIdx = currentIdx + pTagStartEndIdx + 1;
            int contentEndIdx = htmlSpan.Slice(contentStartIdx).IndexOf(pTagClose, StringComparison.OrdinalIgnoreCase);
            if (contentEndIdx == -1)
                break;

            string contentStr = htmlSpan.Slice(contentStartIdx, contentEndIdx).ToString()
                .Replace("<!-- HTML_TAG_START -->", string.Empty) // Remove <!-- HTML_TAG_START --> and <!-- HTML_TAG_END --> inside the content
                .Replace("<!-- HTML_TAG_END -->", string.Empty).Trim();

            if (!string.IsNullOrWhiteSpace(contentStr))
                sb.AppendLine(contentStr);

            currentIdx = contentStartIdx + contentEndIdx + pTagClose.Length; // Update currentIdx to continue scanning after the current </p> tag
        }
        sw.Stop();

        Console.WriteLine($"Elapsed Time ProcessHtmlContentFast(): {sw.Elapsed.TotalMilliseconds * 1000} microseconds");
        return sb.ToString();
    }

    public void GetLlmAnswer(string p_msg)
    {
        if (p_msg == null)
        {
            Debug.WriteLine("Invalid data");
            return;
        }
        LlmInput? userInput = JsonSerializer.Deserialize<LlmInput>(p_msg, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        (string newsStr, string userMsg) = DownloadCompleteNews(userInput!.NewsUrl);
        string responseStr;
        if (String.IsNullOrEmpty(newsStr))
        {
            if (String.IsNullOrEmpty(userMsg))
                responseStr = "News cannot be downloaded.";
            else
                responseStr = userMsg;
        }
        else
        {
            LlmUserInput p_userInp = new()
            {
                LlmModelName = userInput.LlmModelName,
                Msg = userInput.LlmQuestion + newsStr
            };
            responseStr = LlmAssistClient.GenerateChatResponseLlm(p_userInp).TurnAsyncToSyncTask();
        }
        string outputMsgCode = "LlmAnswer";
        if (userInput.LlmQuestion.Contains("summarize"))
            outputMsgCode = "LlmSummary";
        if (userInput.LlmQuestion.Contains("future"))
            outputMsgCode = "LlmFutureOrGrowth";

        byte[] encodedMsg = Encoding.UTF8.GetBytes($"{outputMsgCode}:" + responseStr);
        if (WsWebSocket!.State == WebSocketState.Open)
            WsWebSocket.SendAsync(new ArraySegment<Byte>(encodedMsg, 0, encodedMsg.Length), WebSocketMessageType.Text, true, CancellationToken.None);
    }

    public bool OnReceiveWsAsync_Scan(string msgCode, string msgObjStr)
    {
        switch (msgCode)
        {
            case "GetStockPrice":
                Utils.Logger.Info($"OnReceiveWsAsync_Scan(): GetStockPrice: '{msgObjStr}'");
                GetStockPrice(msgObjStr);
                return true;
            case "GetTickerNews":
                Utils.Logger.Info($"OnReceiveWsAsync_Scan(): GetTickerNews: '{msgObjStr}'");
                GetTickerNews(msgObjStr);
                return true;
            case "GetLlmAnswer":
                Utils.Logger.Info($"OnReceiveWsAsync_Scan(): GetLlmAnswer: '{msgObjStr}'");
                GetLlmAnswer(msgObjStr);
                return true;
            default:
                return false;
        }
    }
}