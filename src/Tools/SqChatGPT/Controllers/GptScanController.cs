using Microsoft.AspNetCore.Mvc;
using Azure; // API uses Azure.Response class
using Azure.AI.OpenAI;
using System.Text.Json;
using SqCommon;
using System.Xml;
using YahooFinanceApi;

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
                            newsItems.Add(newsItem);
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
}