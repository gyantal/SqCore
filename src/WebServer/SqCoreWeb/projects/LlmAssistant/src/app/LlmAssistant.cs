using Microsoft.AspNetCore.Mvc;

namespace SqCoreWeb;

public class LlmAssistant : Microsoft.AspNetCore.Mvc.Controller
{
    [HttpGet]
    public string Get() // localhost:4207/LlmAssistant
    {
        string msg = @"{ ""Response"": ""Response from server""}";
        return msg;
    }

    [HttpGet]
    public string Ping() // localhost:4207/LlmAssistant/Ping or https://sqcore.net/LlmAssintant/Ping
    {
        string msg = @"{ ""Response"": ""Pong""}";
        return msg;
    }
}