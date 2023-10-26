using Microsoft.AspNetCore.Mvc;
using Azure; // API uses Azure.Response class
using Azure.AI.OpenAI;
using System.Text.Json;
using System.Text;

namespace SqChatGPT.Controllers;

public class UserInput
{
    public string Msg { get; set; } = string.Empty;
}

public class ServerResponse
{
    public string Response { get; set; } = string.Empty;
}

[ApiController]
[Route("[controller]")]
public class ChatGptController : ControllerBase
{
    // by default a new Instance of ApiController is created on every HttpRequest. (probably because they have to handle if there are 100 multiple requests at the same time)
    // REST by design is stateless so instantiating for every request by default enforces this on the developers.
    // You may or may not be doing so directly, but the framework is. A controller has a ControllerContext property, an HttpContext property, a Request property, a Response property, a Session property and a User property. These properties contain state about the current HTTP request
    static OpenAIClient? g_openAiClient = null;
    static List<ChatMessage> g_messages = new();

    private readonly ILogger<ChatGptController> _logger;

    public ChatGptController(ILogger<ChatGptController> logger, IConfiguration configuration)
    {
        _logger = logger;
        // string? logLevel = configuration["Logging:LogLevel:Default"];
        
        string openAIApiKey = configuration["ConnectionStrings:OpenAIApiKey"] ?? "OpenApiKeyIsMissing";
        g_openAiClient = new(openAIApiKey, new OpenAIClientOptions());
        g_messages.Add(new ChatMessage(ChatRole.System, "You are a helpful assistant."));
    }

    [HttpGet]
    public string Get()
    {
        string msg = @"{ ""Response"": ""Response from server""}";
        return msg;
    }

    [Route("[action]")] // By using the "[action]" string as a parameter here, we state that the URI must contain this action’s name in addition to the controller’s name: http[s]://[domain]/[controller]/[action]
    [HttpPost("sendUserInput")] // Complex string cannot be in the Url. Use Post instead of Get. Test with Chrome extension 'Talend API Tester'
    public IActionResult SendUserInput([FromBody] UserInput p_inMsg)
    {
        if (p_inMsg == null) 
            return BadRequest("Invalid data");

        // Do something with stringModel.ComplexString
        Console.WriteLine(p_inMsg.Msg);

        string predictedText = string.Empty;
        try
        {
            predictedText = GenerateText(p_inMsg).Result;
        }
        catch (System.Exception e)
        {
            predictedText = e.Message; 
        }

        ServerResponse serverResponse = new() { Response = predictedText };
        string responseJson = JsonSerializer.Serialize(serverResponse); // JsonSerializer handles that a proper JSON cannot contain "\n" Control characters inside the string. We need double escaping ("\n" => "\\n"). Otherwise, the JS:JSON.parse() will fail.
        return Ok(responseJson);
    }

    // https://github.com/Azure/azure-sdk-for-net/tree/main/sdk/openai/Azure.AI.OpenAI
    // dotnet add package Azure.AI.OpenAI --prerelease
    // Stream Chat Messages with non-Azure OpenAI
    async Task<string> GenerateText(UserInput p_inMsg)
    {
        g_messages.Add(new ChatMessage(ChatRole.User, p_inMsg.Msg));
        var chatCompletionsOptions = new ChatCompletionsOptions(g_messages);

        Response<StreamingChatCompletions> response = await g_openAiClient!.GetChatCompletionsStreamingAsync(deploymentOrModelName: "gpt-3.5-turbo", chatCompletionsOptions);
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

        return sb.ToString();
    }

}
