using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Azure; // API uses Azure.Response class
using Azure.AI.OpenAI;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SqCommon;

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

// Custom converter is required because Azure.AI.OpenAI.ChatMessage is not directly serializable by System.Text.Json.
public class ChatMessageConverter : JsonConverter<ChatMessage>
{
    public override ChatMessage Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => throw new NotImplementedException(); // Not needed for serialization
    public override void Write(Utf8JsonWriter writer, ChatMessage value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteString("role", value.Role.ToString().ToLowerInvariant());
        writer.WriteString("content", value.Content);
        writer.WriteEndObject();
    }
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
}