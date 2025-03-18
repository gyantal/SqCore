using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Azure; // API uses Azure.Response class
using Azure.AI.OpenAI;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

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
            string openAIApiKey = configuration["ConnectionStrings:OpenAIApiKey"] ?? "OpenApiKeyIsMissing";
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

    [Route("[action]")] // By using the "[action]" string as a parameter here, we state that the URI must contain this action’s name in addition to the controller’s name: http[s]://[domain]/[controller]/[action]
    [HttpPost("getchatresponse")] // Complex string cannot be in the Url. Use Post instead of Get. Test with Chrome extension 'Talend API Tester'
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
}