using Microsoft.AspNetCore.Mvc;
using Azure; // API uses Azure.Response class
using Azure.AI.OpenAI;
using System.Text.Json;
using SqCommon;
using YahooFinanceApi;

namespace SqChatGPT.Controllers;
public class StockPriceData // this is returned to browser Client
{
    public string Ticker { get; set; } = string.Empty;
    public double PriorClose { get; set; } = 0.0f;
    public double ClosePrice { get; set; } = 0.0f;
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
    [HttpPost("sendStockPriceData")] // Complex string cannot be in the Url. Use Post instead of Get. Test with Chrome extension 'Talend API Tester'
    public async Task<IActionResult> SendStockPriceData([FromBody] UserInput p_inMsg)
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
                tickerPos.Add(new StockPriceData() { Ticker = val.Symbol, PriorClose = val.RegularMarketPreviousClose, ClosePrice = val.RegularMarketPrice, PercentChange = val.RegularMarketChangePercent });
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error while downloading price data {tickers}: {ex.Message}");
        }
        return tickerPos;
    }
}