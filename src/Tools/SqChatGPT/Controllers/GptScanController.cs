using Microsoft.AspNetCore.Mvc;
using Azure; // API uses Azure.Response class
using Azure.AI.OpenAI;
using System.Text.Json;
using SqCommon;
using System.Xml;
using YahooFinanceApi;
using System.Text;
using System.Text.RegularExpressions;
using System.Diagnostics;
using VaderSharp2;

namespace SqChatGPT.Controllers;

public class Rss
{
    public Channel? Channel { get; set; }
    public string Version { get; set; } = string.Empty;
}

public class Channel
{
    public string Title { get; set; } = string.Empty;
    public string Link { get; set; }  = string.Empty;
    public string Description { get; set; }  = string.Empty;
    public string Language { get; set; }  = string.Empty;
    public string LastBuildDate { get; set; }  = string.Empty;
    public string Copyright { get; set; }  = string.Empty;
    public Image? Image { get; set; }
    public List<NewsItem>? NewsItems { get; set; }
}

public class Image
{
    public string Url { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Link { get; set; } = string.Empty;
    public int Width { get; set; }
    public int Height { get; set; }
}

public class NewsItem
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Link { get; set; } = string.Empty;
    public string Guid { get; set; } = string.Empty;
    public string PubDate { get; set; } = string.Empty;
    public string IsGptSummaryLikely { get; set; } = "unknown";
    public float ShortDescriptionSentiment { get; set; } = 0.0f;
}

public class TickerNews // this is returned to browser Client
{
    public string Ticker { get; set; } = string.Empty;
    public List<NewsItem>? NewsItems { get; set; }
}

public class ServerStockNewsResponse
{
    public List<string> Logs { get; set; } = new(); // Contains Logs, Warnings and Errors
    public List<TickerNews> Response { get; set; } = new();
}

public class StockPriceData // this is returned to browser Client
{
    public string Ticker { get; set; } = string.Empty;
    public double PriorClose { get; set; } = 0.0f;
    public double LastPrice { get; set; } = 0.0f;
    public double PercentChange { get; set; } = 0.0f;
}

public class ServerStockPriceDataResponse
{
    public List<string> Logs { get; set; } = new(); // Contains Logs, Warnings and Errors
    public List<StockPriceData> StocksPriceResponse { get; set; } = new();
}

[ApiController]
[Route("[controller]")]
public class GptScanController : ControllerBase
{
    // by default a new Instance of ApiController is created on every HttpRequest. (probably because they have to handle if there are 100 multiple requests at the same time)
    // REST by design is stateless so instantiating for every request by default enforces this on the developers.
    // You may or may not be doing so directly, but the framework is. A controller has a ControllerContext property, an HttpContext property, a Request property, a Response property, a Session property and a User property. These properties contain state about the current HTTP request
    static OpenAIClient? g_openAiClient = null;
    static List<ChatMessage> g_messages = new();

    private readonly ILogger<GptChatController> _logger;
    private static readonly HttpClient g_httpClient = new HttpClient();


//     public static void TestHtmlParse() // Testing purpose
//     {
//         const string testHtmlStr = @"<div class=caas-body><p>By Mike Scarcella</p>
// <p>(Reuters) - Alphabet&#39;s Google on Monday will try to persuade a federal jury in San Francisco to reject antitrust claims by &quot;Fortnite&quot; maker Epic Games in a case that threatens Google&#39;s app store and transaction fees imposed on Android app developers.</p>
// <p>Lawyers for the two companies are set to make their final arguments after more than a month of trial in Epic&#39;s lawsuit, which accuses Google of unlawfully scheming to make its Play store dominant over rival app stores.</p>
// <p>The lawsuit, filed in 2020, also challenges the fee of up to 30% that Google imposes on developers for in-app sales.</p>
// <p>Cary, North Carolina-based Epic, which owns the popular Fortnite multiplayer shooter game, said in the lawsuit that Google &quot;suppresses innovation and choice&quot; through a &quot;web of secretive, anticompetitive agreements.&quot;</p>
// <p>Google has denied wrongdoing, arguing that it competes &quot;intensely on price, quality, and security&quot; against Apple&#39;s App Store.</p>
// <p>Epic is seeking a court order to halt Google&#39;s alleged monopoly over Android app distribution and in-app billing. Google has countersued for damages against Epic for allegedly violating the company&#39;s developer agreement.</p>
// <p>Google settled related claims from dating app maker Match before the trial started. The tech giant also settled related antitrust claims by U.S. states and consumers under terms that have not been made public.</p>
// <p>Epic lodged a similar antitrust case against Apple in 2020, but a U.S. judge largely ruled in favor of Apple in September 2021.</p>
// <p>Epic has asked the U.S. Supreme Court to revive key claims in the Apple case, and Apple is fighting part of a ruling for Epic that would require changes to App Store rules.</p>
// <p>(Reporting by Mike Scarcella; Editing by David Bario and Bernadette Baum)</p></div>";
//         ProcessHtmlContentFast(testHtmlStr);
//         ProcessHtmlContent(testHtmlStr);
//     }

    public GptScanController(ILogger<GptChatController> logger, IConfiguration configuration)
    {
        _logger = logger;
        // string? logLevel = configuration["Logging:LogLevel:Default"];
        if (g_openAiClient == null) // a new ChatGptController() instance is created for every request. Initialize openAiClient only once
        {
            string openAIApiKey = configuration["ConnectionStrings:OpenAIApiKey"] ?? "OpenApiKeyIsMissing";
            g_openAiClient = new(openAIApiKey, new OpenAIClientOptions());
            g_messages.Add(new ChatMessage(ChatRole.System, "You are a helpful assistant."));
        }
    }

    [HttpGet]
    public string Get()
    {
        string msg = @"{ ""Response"": ""Response from server""}";
        return msg;
    }

    [Route("[action]")] // By using the "[action]" string as a parameter here, we state that the URI must contain this action’s name in addition to the controller’s name: http[s]://[domain]/[controller]/[action]
    [HttpPost("getstockprice")] // Complex string cannot be in the Url. Use Post instead of Get. Test with Chrome extension 'Talend API Tester'
    public async Task<IActionResult> GetStockPrice([FromBody] UserInput p_inMsg)
    {
        if (p_inMsg == null)
            return BadRequest("Invalid data");

        // Do something with stringModel.ComplexString
        Console.WriteLine(p_inMsg.Msg);

        List<StockPriceData> response = new();
        List<string> logs = new();
        try
        {
            response = await DownloadStockPriceData(p_inMsg);
        }
        catch (System.Exception e)
        {
            logs.Add($"Error: {e.Message}");
        }

        ServerStockPriceDataResponse serverResponse = new() { StocksPriceResponse = response, Logs = logs };
        string responseJson = JsonSerializer.Serialize(serverResponse); // JsonSerializer handles that a proper JSON cannot contain "\n" Control characters inside the string. We need double escaping ("\n" => "\\n"). Otherwise, the JS:JSON.parse() will fail.
        return Ok(responseJson);
    }

    public async Task<List<StockPriceData>> DownloadStockPriceData(UserInput p_inMsg)
    {
        string[] tickers = p_inMsg.Msg.Split(',', StringSplitOptions.RemoveEmptyEntries);
        List<StockPriceData> tickerPos = new(tickers.Length);
        try
        {
            Console.WriteLine("Calling YF for price... (2023-12: The first time to get the cookie takes 20sec)");
            IReadOnlyDictionary<string, Security> quotes = await Yahoo.Symbols(tickers).Fields(new Field[] { Field.Symbol, Field.RegularMarketPreviousClose, Field.RegularMarketPrice, Field.RegularMarketChange, Field.RegularMarketChangePercent }).QueryAsync();
            foreach (var val in quotes.Values)
            {
                tickerPos.Add(new StockPriceData() { Ticker = val.Symbol, PriorClose = val.RegularMarketPreviousClose, LastPrice = val.RegularMarketPrice, PercentChange = val.RegularMarketChangePercent });
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error while downloading price data {tickers}: {ex.Message}");
        }
        return tickerPos;
    }

    [Route("[action]")] // By using the "[action]" string as a parameter here, we state that the URI must contain this action’s name in addition to the controller’s name: http[s]://[domain]/[controller]/[action]
    [HttpPost("getnews")] // Complex string cannot be in the Url. Use Post instead of Get. Test with Chrome extension 'Talend API Tester'
    public IActionResult GetNews([FromBody] UserInput p_inMsg)
    {
        if (p_inMsg == null)
            return BadRequest("Invalid data");

        // Do something with stringModel.ComplexString
        Console.WriteLine(p_inMsg.Msg);

        List<TickerNews> response = new();
        List<string> logs = new();
        try
        {
            response = GetTickersNews(p_inMsg.Msg).Result;
        }
        catch (System.Exception e)
        {
            logs.Add($"Error: {e.Message}");
        }

        ServerStockNewsResponse serverResponse = new() { Response = response, Logs = logs };
        string responseJson = JsonSerializer.Serialize(serverResponse); // JsonSerializer handles that a proper JSON cannot contain "\n" Control characters inside the string. We need double escaping ("\n" => "\\n"). Otherwise, the JS:JSON.parse() will fail.
        return Ok(responseJson);
    }

    // https://github.com/Azure/azure-sdk-for-net/tree/main/sdk/openai/Azure.AI.OpenAI
    // dotnet add package Azure.AI.OpenAI --prerelease
    // Stream Chat Messages with non-Azure OpenAI
    async Task<List<TickerNews>> GetTickersNews(string p_tickerLstStr)
    {
        string[] tickers = p_tickerLstStr.Split(',', StringSplitOptions.RemoveEmptyEntries);
        List<TickerNews> tickerNewss = new(tickers.Length);
        var analyzer = new SentimentIntensityAnalyzer(); // Sentiment analysis
        foreach (var ticker in tickers) // we can't do it in a batch mode by asking YF many tickers at the same time. Tested it: YF gives back only the news for the first ticker.
        {
            try
            {
                string url = $"https://feeds.finance.yahoo.com/rss/2.0/headline?s={ticker}";
                string xmlContent = await g_httpClient.GetStringAsync(url);
                Rss rss = ParseXMLString(xmlContent);

                List<NewsItem> newsItems = new(); // local newsItems list
                if (rss?.Channel?.NewsItems != null)
                {
                    DateTime utcNow = DateTime.UtcNow;
                    // Iterate through NewsItems and add items within one week
                    foreach (var newsItem in rss.Channel.NewsItems)
                    {
                        DateTime pubDate = Utils.Str2DateTimeUtc(newsItem.PubDate);
                        int nDays = (int)(utcNow - pubDate).TotalDays; // Calculate the number of days
                        if (nDays <= 7)// Add the news item to the local list if within one week
                        {
                            var result = analyzer.PolarityScores(newsItem.Description);
                            newsItem.ShortDescriptionSentiment = (float)result.Compound;
                            newsItems.Add(newsItem);
                        }
                    }
                }
                tickerNewss.Add(new TickerNews() { Ticker = ticker, NewsItems = newsItems });
                // Here you would do something with the xmlContent, e.g., save to a file or process it.
                Console.WriteLine($"Downloaded XML content for {ticker}");
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"Error downloading XML for {ticker}: {ex.Message}");
            }
        }

        return tickerNewss;
    }

    // XmlReader is the fastest way to walk-forward an XML. A general reader of strings. A walk-forward one-way.
    // XmlDocument is the slowest, because it keeps everything in RAM.
    // XmlSerializer<MyClass> suffers from long initial cost (30-70ms), because at first run it creates a virtual DLL with the generated classes for that MyClass
    public Rss ParseXMLString(string xmlString)
    {
        Rss rss = new Rss();
        rss.Channel = new Channel();
        rss.Channel.NewsItems = new List<NewsItem>();
        NewsItem? currentItem = null;

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
                                    if (reader.Depth == 3) // Detecting if this belongs to channel or image
                                        rss.Channel.Title = reader.Value.Trim();
                                    else if (reader.Depth == 4)
                                        rss.Channel.Image!.Title = reader.Value.Trim();
                                    else
                                        throw new Exception("Unexpected title depth");
                                else
                                    currentItem.Title = reader.Value.Trim();
                            }
                            break;
                        case "link":
                            if (reader.Read())
                            {
                                if (currentItem == null)
                                    if (reader.Depth == 3) // Detecting if this belongs to channel or image
                                        rss.Channel.Link = reader.Value.Trim();
                                    else if (reader.Depth == 4)
                                        rss.Channel.Image!.Link = reader.Value.Trim();
                                    else
                                        throw new Exception("Unexpected link depth");
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
                            currentItem = new NewsItem();
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
                } else // so, it is an EndElement, such as </item>
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

    [Route("[action]")] // By using the "[action]" string as a parameter here, we state that the URI must contain this action’s name in addition to the controller’s name: http[s]://[domain]/[controller]/[action]
    [HttpPost("summarizenews")] // Complex string cannot be in the Url. Use Post instead of Get. Test with Chrome extension 'Talend API Tester'
    public async Task<IActionResult> GetNewsAndSummarize([FromBody] UserInput p_inMsg)
    {
        if (p_inMsg == null)
            return BadRequest("Invalid data");

        string newsStr = await DownloadCompleteNews(p_inMsg.Msg);
        string responseStr;
        if (!newsStr.Contains("recommend visiting")) // checking for the condition if newsStr has the complete story or it is directing to another link
        {
            p_inMsg.Msg = "Summarize this:\n" + newsStr;
            responseStr = await GenerateChatResponse(p_inMsg);
        }
        else
            responseStr = newsStr;
        string responseJson = JsonSerializer.Serialize(responseStr); // JsonSerializer handles that a proper JSON cannot contain "\n" Control characters inside the string. We need double escaping ("\n" => "\\n"). Otherwise, the JS:JSON.parse() will fail.
        return Ok(responseJson);
    }

    [Route("[action]")] // By using the "[action]" string as a parameter here, we state that the URI must contain this action’s name in addition to the controller’s name: http[s]://[domain]/[controller]/[action]
    [HttpPost("getisgptsummarylikely")] // Complex string cannot be in the Url. Use Post instead of Get. Test with Chrome extension 'Talend API Tester'
    public async Task<IActionResult> GetIsGptSummaryLikely([FromBody] UserInput p_inMsg)
    {
        if (p_inMsg == null)
            return BadRequest("Invalid data");

        string newsStr = await DownloadCompleteNews(p_inMsg.Msg);
        string responseStr;
        if (!newsStr.Contains("recommend visiting")) // checking for the condition if newsStr has the complete story or it is directing to another link
            responseStr = "yes";
        else
            responseStr = "no";
        string responseJson = JsonSerializer.Serialize(responseStr); // JsonSerializer handles that a proper JSON cannot contain "\n" Control characters inside the string. We need double escaping ("\n" => "\\n"). Otherwise, the JS:JSON.parse() will fail.
        return Ok(responseJson);
    }

    [Route("[action]")] // By using the "[action]" string as a parameter here, we state that the URI must contain this action’s name in addition to the controller’s name: http[s]://[domain]/[controller]/[action]
    [HttpPost("newssentiment")] // Complex string cannot be in the Url. Use Post instead of Get. Test with Chrome extension 'Talend API Tester'
    public async Task<IActionResult> GetNewsSentiment([FromBody] UserInput p_inMsg)
    {
        if (p_inMsg == null)
            return BadRequest("Invalid data");

        string newsStr = await DownloadCompleteNews(p_inMsg.Msg);
        string responseStr = string.Empty;
        if (!newsStr.Contains("recommend visiting")) // checking for the condition if newsStr has the complete story or it is directing to another link
        {
            var analyzer = new SentimentIntensityAnalyzer();
            var result = analyzer.PolarityScores(newsStr);
            responseStr = result.Compound.ToString();
        }
        string responseJson = JsonSerializer.Serialize(responseStr); // JsonSerializer handles that a proper JSON cannot contain "\n" Control characters inside the string. We need double escaping ("\n" => "\\n"). Otherwise, the JS:JSON.parse() will fail.
        return Ok(responseJson);
    }

    public async Task<string> DownloadCompleteNews(string p_newsUrlLink)
    {
        string responseStr;
        string htmlContent = await g_httpClient.GetStringAsync(p_newsUrlLink);

        if ((p_newsUrlLink.StartsWith("https://finance.yahoo.com") || p_newsUrlLink.StartsWith("https://ca.finance.yahoo.com/")) && !htmlContent.Contains("Continue reading")) // if the YF news on YF website has "Continue reading" then a link will lead to another website (Bloomberg, Fools), in that case we don't process it.
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
            responseStr = $"The full news isn't accessible on Yahoo Finance. I recommend visiting this <a href={p_newsUrlLink}>link</a> to directly retrieve the summary from ChatGPT.";

        return responseStr;
    }

    static string ProcessHtmlContentFast(string p_html) // Elapsed Time: 91 microseconds
    {
        Stopwatch sw = new();
        sw.Start();
        StringBuilder sb = new();
        ReadOnlySpan<char> htmlSpan = p_html.AsSpan();

        int divWithCaasbodyStartPos = htmlSpan.IndexOf("caas-body>");
        if (divWithCaasbodyStartPos == -1)
        {
            Console.WriteLine("Cannot find caas-body. Stop processing.");
            return string.Empty;
        }
        divWithCaasbodyStartPos += "caas-body>".Length;
        ReadOnlySpan<char> htmlBodySpan = htmlSpan.Slice(divWithCaasbodyStartPos);
        int divWithCaasbodyEndPos = htmlBodySpan.IndexOf("</p></div>");
        if (divWithCaasbodyEndPos == -1)
        {
            Console.WriteLine("Cannot find </p></div>. Stop processing.");
            return string.Empty;
        }
        divWithCaasbodyEndPos -= "</p>".Length; // keeping the end paragraph tag "</p>", so that we can iterate between paragraph opening and ending tags
        ReadOnlySpan<char> span = htmlBodySpan.Slice(start: 0, length: divWithCaasbodyEndPos);

        while (true)
        {
            // Find the next occurrence of <p> and </p> html tags
            int pTagStartPos = span.IndexOf("<p>");
            int pTagEndPos = span.IndexOf("</p>");

            if (pTagStartPos == -1 || pTagEndPos == -1) // If no more <p> tags are found, exit the loop
                break;

            ReadOnlySpan<char> content = span.Slice(pTagStartPos + 3, pTagEndPos - (pTagStartPos + 3)); // Extract the content between <p> and </p> and append to StringBuilder
            sb.Append(content);

            span = span.Slice(pTagEndPos + 4); // Move the span position to the end of the </p> tag
        }
        sw.Stop();

        Console.WriteLine($"Elapsed Time ProcessHtmlContentFast(): {sw.Elapsed.TotalMilliseconds * 1000} microseconds");
        return sb.ToString();
    }
    
    static string ProcessHtmlContentRegex(string p_html) // Elapsed Time for RegEx first run: 28,198 microseconds
    {
        Stopwatch sw = new();
        sw.Start();
        // Option1: Hard coded method which might not work for all the cases, we need to keep on adding the regular expressions updating for each link.
        int divWithCaasbodyStartPos = p_html.IndexOf("caas-body>"); // tetx contains only '<div class=caas-body>', but Chrome DevTools/Elements show with quotes: '<div class="caas-body">'
        if (divWithCaasbodyStartPos == -1)
        {
            Console.WriteLine("Cannot find caas-body. Stop processing.");
            return string.Empty;
        }
        divWithCaasbodyStartPos += "caas-body>".Length;
        int divWithCaasbodyEndPos = p_html.IndexOf("</p></div>");
        if (divWithCaasbodyEndPos == -1)
        {
            Console.WriteLine("Cannot find </p></div>. Stop processing.");
            return string.Empty;
        }
        divWithCaasbodyEndPos -= "</p>".Length;
        string parasWithTags = p_html[divWithCaasbodyStartPos..divWithCaasbodyEndPos];

        string responseStr = Regex.Replace(parasWithTags, "<.*?>|<a[^>]*>(.*?)</a>", string.Empty); // Cleaning the tags
        sw.Stop();

        Console.WriteLine($"Elapsed Time ProcessHtmlContent(): {sw.Elapsed.TotalMilliseconds * 1000} microseconds");
        return responseStr;
    }

    static async Task<string> GenerateChatResponse(UserInput p_inMsg)
    {
        // >To check whether you use GPT 3.5 or 4 use this prompt: "If there are 10 books in a room and I read 2, how many books are still in the room"
        string llmModelName = p_inMsg.LlmModelName; // "auto", "gpt-3.5-turbo" (4K), "gpt-3.5-turbo-16k", "gpt-4" (8K), "gpt-4-32k"
        if (llmModelName == "auto")
        {
            // GPT-4 base(8K) is 50x more expensive than GPT-3.5-turbo(4K)
            // In theory we should estimate the number of tokens based on previous messages in g_messages
            // Cost efficiently, under 4K: gpt-3.5-turbo, between 4K and 16K: gpt-3.5-turbo-16k, over 16K: gpt-4
            llmModelName = "gpt-3.5-turbo";
        }

        if (llmModelName != "gpt-3.5-turbo" && llmModelName != "gpt-3.5-turbo-16k" && llmModelName != "gpt-4" && llmModelName != "gpt-4-32k")
            throw new System.Exception("Invalid AI model name");

        var chatCompletionsOptions = new ChatCompletionsOptions(new List<ChatMessage>
        {
            new ChatMessage
            {
                Role = ChatRole.User,
                Content = p_inMsg.Msg,
            },
        });

        Response<ChatCompletions> response = await g_openAiClient!.GetChatCompletionsAsync(deploymentOrModelName: llmModelName, chatCompletionsOptions);
        ChatCompletions chatCompletion = response.Value;

        return chatCompletion.Choices[0].Message.Content;
    }
}