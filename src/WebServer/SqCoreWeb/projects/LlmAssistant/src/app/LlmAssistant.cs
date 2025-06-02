using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml;
using Azure; // API uses Azure.Response class
using Azure.AI.OpenAI;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SqCommon;
using YahooFinanceApi;

namespace SqCoreWeb;

public class LlmUserInput
{
    public string LlmModelName { get; set; } = string.Empty; // "auto", "gpt-3.5-turbo" (4K), "gpt-3.5-turbo-16k", "gpt-4" (8K), "gpt-4-32k"
    public string Msg { get; set; } = string.Empty;
}

public class ServerResponse
{
    public List<string> Logs { get; set; } = new();
    public string Response { get; set; } = string.Empty;
}

public class Rss
{
    public Channel? Channel { get; set; }
    public string Version { get; set; } = string.Empty;
}

public class Channel
{
    public string Title { get; set; } = string.Empty;
    public string Link { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Language { get; set; } = string.Empty;
    public string LastBuildDate { get; set; } = string.Empty;
    public string Copyright { get; set; } = string.Empty;
    public Image? Image { get; set; }
    public List<LlmNewsItem>? NewsItems { get; set; }
}

public class Image
{
    public string Url { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Link { get; set; } = string.Empty;
    public int Width { get; set; }
    public int Height { get; set; }
}

public class LlmNewsItem
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
    public List<LlmNewsItem>? NewsItems { get; set; }
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

public class LlmInput
{
    public string LlmModelName { get; set; } = string.Empty; // "auto", "gpt-3.5-turbo" (4K), "gpt-3.5-turbo-16k", "gpt-4" (8K), "gpt-4-32k"
    public string NewsUrl { get; set; } = string.Empty;
    public string LlmQuestion { get; set; } = string.Empty;
}

[Route("[controller]")]
public class LlmAssistantController : Microsoft.AspNetCore.Mvc.Controller
{
    // by default a new Instance of ApiController is created on every HttpRequest. (probably because they have to handle if there are 100 multiple requests at the same time)
    // REST by design is stateless so instantiating for every request by default enforces this on the developers.
    // You may or may not be doing so directly, but the framework is. A controller has a ControllerContext property, an HttpContext property, a Request property, a Response property, a Session property and a User property. These properties contain state about the current HTTP request
    static OpenAIClient? g_openAiClient = null;
    static List<ChatMessage> g_messages = new();

    private readonly ILogger<LlmAssistantController> _logger;

    public LlmAssistantController(ILogger<LlmAssistantController> logger, IConfiguration configuration)
    {
        _logger = logger;
        // string? logLevel = configuration["Logging:LogLevel:Default"];
        if (g_openAiClient == null) // a new LlmAssistantController() instance is created for every request. Initialize openAiClient only once
        {
            string openAIApiKey = Utils.Configuration["ConnectionStrings:OpenAIApiKey"] ?? throw new SqException("OpenApiKeyIsMissing is missing from Config");
            Console.WriteLine($"LlmAssistantController: OpenAIApiKey: '{openAIApiKey}'");
            g_openAiClient = new(openAIApiKey, new OpenAIClientOptions());
            g_messages.Add(new ChatMessage(ChatRole.System, "You are a helpful assistant."));
        }
    }

    [HttpGet]
    public string Get() // localhost:4207/LlmAssistant
    {
        string msg = @"{ ""Response"": ""Response from server""}";
        return msg;
    }

    [HttpGet("Ping")]
    public string Ping() // localhost:4207/LlmAssistant/Ping or https://sqcore.net/LlmAssintant/Ping
    {
        string msg = @"{ ""Response"": ""Pong""}";
        return msg;
    }

    // Temporarily commented out until we test grok(xAi)
    // [Route("[action]")] // By using the "[action]" string as a parameter here, we state that the URI must contain this action’s name in addition to the controller’s name: http[s]://[domain]/[controller]/[action]
    // [HttpPost("getchatresponse")] // Complex string cannot be in the Url. Use Post instead of Get. Test with Chrome extension 'Talend API Tester'
    public IActionResult GetChatResponse([FromBody] LlmUserInput p_inMsg)
    {
        if (p_inMsg == null)
            return BadRequest("Invalid data");

        // Do something with stringModel.ComplexString
        Console.WriteLine(p_inMsg.Msg);

        string responseStr = string.Empty;
        List<string> logs = new();
        try
        {
            responseStr = GenerateChatResponse(p_inMsg).Result;
        }
        catch (System.Exception e)
        {
            responseStr = e.Message;
            logs.Add($"Error: {e.Message}");
        }

        ServerResponse serverResponse = new() { Response = responseStr, Logs = logs };
        string responseJson = JsonSerializer.Serialize(serverResponse); // JsonSerializer handles that a proper JSON cannot contain "\n" Control characters inside the string. We need double escaping ("\n" => "\\n"). Otherwise, the JS:JSON.parse() will fail.
        return Ok(responseJson);
    }

    // https://github.com/Azure/azure-sdk-for-net/tree/main/sdk/openai/Azure.AI.OpenAI
    // dotnet add package Azure.AI.OpenAI --prerelease
    // Stream Chat Messages with non-Azure OpenAI
    public async Task<string> GenerateChatResponse(LlmUserInput p_inMsg)
    {
        g_messages.Add(new ChatMessage(ChatRole.User, p_inMsg.Msg));

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

        var chatCompletionsOptions = new ChatCompletionsOptions(g_messages);

        Response<StreamingChatCompletions> response = await g_openAiClient!.GetChatCompletionsStreamingAsync(deploymentOrModelName: llmModelName, chatCompletionsOptions);
        using StreamingChatCompletions streamingChatCompletions = response.Value;

        StringBuilder sb = new();
        await foreach (StreamingChatChoice choice in streamingChatCompletions.GetChoicesStreaming())
        {
            await foreach (ChatMessage message in choice.GetMessageStreaming())
            {
                Console.Write(message.Content);
                sb.Append(message.Content);
            }
            Console.WriteLine();
            sb.AppendLine();
        }

        // >https://github.com/Azure/azure-sdk-for-net/issues/38491
        // "From what I can see, there is no way to get the CompletionsUsage of a request when using StreamingChatCompletions. It has private readonly IList<ChatCompletions> _baseChatCompletions; but I don't see anywhere this is exposed.
        // It would be nice if there was a way to check the token usage after streaming is complete.
        // "Tracking usage is trivially easy for the non-streaming version but seems impossible for streaming."
        // <2023-10-15:> any chance to have this feature?

        return sb.ToString();
    }

    [Route("[action]")] // By using the "[action]" string as a parameter here, we state that the URI must contain this action’s name in addition to the controller’s name: http[s]://[domain]/[controller]/[action]
    [HttpPost("getstockprice")] // Complex string cannot be in the Url. Use Post instead of Get. Test with Chrome extension 'Talend API Tester'
    public async Task<IActionResult> GetStockPrice([FromBody] LlmUserInput p_inMsg)
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

    public async Task<List<StockPriceData>> DownloadStockPriceData(LlmUserInput p_inMsg)
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
    public IActionResult GetNews([FromBody] LlmUserInput p_inMsg)
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
    public async Task<List<TickerNews>> GetTickersNews(string p_tickerLstStr)
    {
        string[] tickers = p_tickerLstStr.Split(',', StringSplitOptions.RemoveEmptyEntries);
        List<TickerNews> tickerNewss = new(tickers.Length);
        // var analyzer = new SentimentIntensityAnalyzer(); // Sentiment analysis
        foreach (var ticker in tickers) // we can't do it in a batch mode by asking YF many tickers at the same time. Tested it: YF gives back only the news for the first ticker.
        {
            try
            {
                string url = $"https://feeds.finance.yahoo.com/rss/2.0/headline?s={ticker}";
                string? xmlContent = await Utils.DownloadStringWithRetryAsync(url);
                if (xmlContent == null)
                    throw new SqException($"DownloadStringWithRetryAsync failed for ticker {ticker}");
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

    [Route("[action]")] // By using the "[action]" string as a parameter here, we state that the URI must contain this action’s name in addition to the controller’s name: http[s]://[domain]/[controller]/[action]
    [HttpPost("getllmAnswer")] // Complex string cannot be in the Url. Use Post instead of Get. Test with Chrome extension 'Talend API Tester'
    public async Task<IActionResult> GetLlmAnswer([FromBody] LlmInput p_inMsg)
    {
        if (p_inMsg == null)
            return BadRequest("Invalid data");
        (string newsStr, string userMsg) = await DownloadCompleteNews(p_inMsg.NewsUrl);
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
                LlmModelName = p_inMsg.LlmModelName,
                Msg = p_inMsg.LlmQuestion + newsStr
            };
            responseStr = await LlmChat.GenerateChatResponseLlm(p_userInp);
        }
        string responseJson = JsonSerializer.Serialize(responseStr); // JsonSerializer handles that a proper JSON cannot contain "\n" Control characters inside the string. We need double escaping ("\n" => "\\n"). Otherwise, the JS:JSON.parse() will fail.
        return Ok(responseJson);
    }

    [Route("[action]")] // By using the "[action]" string as a parameter here, we state that the URI must contain this action’s name in addition to the controller’s name: http[s]://[domain]/[controller]/[action]
    [HttpPost("getisllmsummarylikely")] // Complex string cannot be in the Url. Use Post instead of Get. Test with Chrome extension 'Talend API Tester'
    public async Task<IActionResult> GetIsLlmSummaryLikely([FromBody] LlmUserInput p_inMsg)
    {
        if (p_inMsg == null)
            return BadRequest("Invalid data");

        (string newsStr, string userMsg) = await DownloadCompleteNews(p_inMsg.Msg);
        string responseStr;
        if (!String.IsNullOrEmpty(newsStr) && String.IsNullOrEmpty(userMsg)) // when newsStr is given correctly, we expect there is no userMsg with warnings.
            responseStr = "yes";
        else
            responseStr = "no";
        string responseJson = JsonSerializer.Serialize(responseStr); // JsonSerializer handles that a proper JSON cannot contain "\n" Control characters inside the string. We need double escaping ("\n" => "\\n"). Otherwise, the JS:JSON.parse() will fail.
        return Ok(responseJson);
    }

    public async Task<(string ResponseStr, string UserMsg)> DownloadCompleteNews(string p_newsUrl) // returns newsStr , UserMsg. When newsStr is given correctly, we expect there is no userMsg with warnings.
    {
        string responseStr;
        string? htmlContent = await Utils.DownloadStringWithRetryAsync(p_newsUrl);
        if (htmlContent == null)
            throw new SqException($"DownloadStringWithRetryAsync failed for Url {p_newsUrl}");

        if ((p_newsUrl.StartsWith("https://finance.yahoo.com") || p_newsUrl.StartsWith("https://ca.finance.yahoo.com/")) && !htmlContent.Contains("Continue reading")) // if the YF news on YF website has "Continue reading" then a link will lead to another website (Bloomberg, Fools), in that case we don't process it.
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
    static string ProcessHtmlContentFast(string p_html)
    {
        Stopwatch sw = new();
        sw.Start();
        StringBuilder sb = new();
        ReadOnlySpan<char> htmlSpan = p_html.AsSpan();

        while (true)
        {
            int divWithClassAtomsWrapperStart = htmlSpan.IndexOf("class=\"atoms-wrapper\"");
            if (divWithClassAtomsWrapperStart == -1)
                break;

            divWithClassAtomsWrapperStart += "class=\"atoms-wrapper\"".Length;
            ReadOnlySpan<char> divWithClassAtomsWrapperSpan = htmlSpan.Slice(divWithClassAtomsWrapperStart);

            int divWithClassAtomsWrapperEnd = divWithClassAtomsWrapperSpan.IndexOf("</p></div>");
            if (divWithClassAtomsWrapperEnd == -1)
                break;

            ReadOnlySpan<char> atomsWrapperSpan = divWithClassAtomsWrapperSpan.Slice(0, divWithClassAtomsWrapperEnd + "</p></div>".Length);

            // extract all <p class="yf-1090901">some news</p>
            while (true)
            {
                int pTagStart = atomsWrapperSpan.IndexOf("<p class=\"yf-1090901\"");
                if (pTagStart == -1)
                    break;

                int pTagStartClose = atomsWrapperSpan.Slice(pTagStart).IndexOf('>');
                if (pTagStartClose == -1)
                    break;

                int pContentStart = pTagStart + pTagStartClose + 1;
                int pTagEnd = atomsWrapperSpan.Slice(pContentStart).IndexOf("</p>");
                if (pTagEnd == -1)
                    break;

                string contentStr = atomsWrapperSpan.Slice(pContentStart, pTagEnd).ToString();
                contentStr = contentStr.Replace("<!-- HTML_TAG_START -->", string.Empty).Replace("<!-- HTML_TAG_END -->", string.Empty).Trim(); // Remove <!-- HTML_TAG_START --> and <!-- HTML_TAG_END --> inside the content

                sb.Append(contentStr);
                sb.AppendLine();

                atomsWrapperSpan = atomsWrapperSpan.Slice(pContentStart + pTagEnd + "</p>".Length); // Move the atomsWrapperSpan position to the end of the </p>
            }

            htmlSpan = divWithClassAtomsWrapperSpan.Slice(divWithClassAtomsWrapperEnd + "</p></div>".Length); // Move the htmlSpan position to the end of the </p></div> to find next "atoms-wrapper"
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

    static async Task<string> GenerateChatResponseScan(LlmUserInput p_inMsg)
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

    [Route("[action]")] // By using the "[action]" string as a parameter here, we state that the URI must contain this action’s name in addition to the controller’s name: http[s]://[domain]/[controller]/[action]
    [HttpPost("earningsdate")] // Complex string cannot be in the Url. Use Post instead of Get. Test with Chrome extension 'Talend API Tester'
    public async Task<string> GetEarningsDate([FromBody] LlmUserInput p_inMsg)
    {
        if (p_inMsg == null)
            return "Invalid data";

        string url = $"https://finance.yahoo.com/quote/{p_inMsg.Msg}"; // p_inMsg.Msg is ticker(AAPL), https://finance.yahoo.com/quote/AAPL.
        string? htmlContent = await Utils.DownloadStringWithRetryAsync(url);
        if (htmlContent == null)
            throw new SqException($"DownloadStringWithRetryAsync failed for ticker {p_inMsg.Msg}");
        string responseDateStr = ProcessHtmlContentForEarningsDate(htmlContent);
        return JsonSerializer.Serialize(responseDateStr); // JsonSerializer handles that a proper JSON cannot contain "\n" Control characters inside the string. We need double escaping ("\n" => "\\n"). Otherwise, the JS:JSON.parse() will fail.
    }

    // As of 2024-06-10, it was observed that the Earnings Date was not displayed on the UI.
    // The HTML structure for finding the Earnings Date, previously using <span> and <td> elements, has changed to using <span> with class and <li> tags.
    // Refer to the method ProcessHtmlContentForEarningsDate() for further details.
    static string ProcessHtmlContentForEarningsDate_old(string p_html)
    {
        StringBuilder sb = new();
        int earningsDateStartPos = p_html.IndexOf("Earnings Date");
        if (earningsDateStartPos == -1)
        {
            Console.WriteLine("Cannot find Earnings Date. Stop processing.");
            return string.Empty;
        }

        // Extract the substring starting from the position of "Earnings Date" to htmlSpan
        ReadOnlySpan<char> htmlSpan = p_html.AsSpan(earningsDateStartPos);

        int spanEarningsDateStartPos = htmlSpan.IndexOf("<span>");
        if (spanEarningsDateStartPos == -1)
        {
            Console.WriteLine("Cannot find <span>. Stop processing.");
            return string.Empty;
        }
        ReadOnlySpan<char> htmlBodySpan = htmlSpan.Slice(spanEarningsDateStartPos);
        int spanEarningsDateEndPos = htmlBodySpan.IndexOf("</span></td>");
        if (spanEarningsDateEndPos == -1)
        {
            Console.WriteLine("Cannot find </span></td>. Stop processing.");
            return string.Empty;
        }
        spanEarningsDateEndPos += "</span>".Length; // keeping the end paragraph tag "</span>", so that we can iterate between span opening and ending tags
        ReadOnlySpan<char> span = htmlBodySpan.Slice(start: 0, length: spanEarningsDateEndPos);

        bool isFirstSpan = true; // Flag to check if it is the first span
        while (true)
        {
            // Find the next occurrence of <span> and </span> html tags
            int spanTagStartPos = span.IndexOf("<span>");
            int spanTagEndPos = span.IndexOf("</span>");

            if (spanTagStartPos == -1 || spanTagEndPos == -1) // If no more <span> tags are found, exit the loop
                break;

            ReadOnlySpan<char> dateStr = span.Slice(spanTagStartPos + "<span>".Length, spanTagEndPos - (spanTagStartPos + "<span>".Length)); // Extract the content between <span> and </span> and append to StringBuilder.

            if (!isFirstSpan)
                sb.Append(" - "); // Add a separator only if it's not the first span

            sb.Append(dateStr);
            isFirstSpan = false;
            span = span.Slice(spanTagEndPos + "</span>".Length); // Move the span position to the end of the </span> tag
        }
        return sb.ToString();
    }

    static string ProcessHtmlContentForEarningsDate(string p_html)
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
}