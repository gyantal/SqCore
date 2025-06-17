using System;
using System.Net.Http;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using SqCommon;

namespace SqCoreWeb;

public partial class LlmAssistClient
{
    public void GetChatResponseLlmBasic(string p_msg)
    {
        string responseStr;
        LlmUserInput? userInput = JsonSerializer.Deserialize<LlmUserInput>(p_msg, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        if (userInput == null)
            responseStr = "Invalid data";
        else
            responseStr = GenerateChatResponseLlmBasic(userInput).Result;

        byte[] encodedMsg = Encoding.UTF8.GetBytes("LlmResponseBasicChat:" + responseStr);
        if (WsWebSocket!.State == WebSocketState.Open)
            WsWebSocket.SendAsync(new ArraySegment<Byte>(encodedMsg, 0, encodedMsg.Length), WebSocketMessageType.Text, true, CancellationToken.None);
    }

    public static async Task<string> GenerateChatResponseLlmBasic(LlmUserInput p_userInput)
    {
        string apiKey = string.Empty;
        string apiUrl = string.Empty;
        string llmModelName = p_userInput.LlmModelName;
        if (llmModelName == "grok")
        {
            llmModelName = "grok-3-mini-latest";
            apiUrl = "https://api.x.ai/v1/chat/completions";
            apiKey = Utils.Configuration["ConnectionStrings:GrokAIApiKey"] ?? throw new SqException("GrokApiKey is missing from Config");
        }
        else if (llmModelName == "deepseek")
        {
            llmModelName = "deepseek-chat";
            apiUrl = "https://api.deepseek.com/v1/chat/completions";
            apiKey = Utils.Configuration["ConnectionStrings:DeepseekAIApiKey"] ?? throw new SqException("DeepseekAIApiKey is missing from Config");
        }

        if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(apiUrl))
            return "API key or URL is not configured.";

        using (HttpClient client = new HttpClient())
        {
            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
            var requestBody = new
            {
                model = llmModelName,
                messages = new[]
                {
                    new { role = "user", content = p_userInput.Msg }
                }
            };
            string json = JsonSerializer.Serialize(requestBody);
            StringContent content = new StringContent(json, Encoding.UTF8, "application/json");
            HttpResponseMessage response = await client.PostAsync(apiUrl, content);
            string result = await response.Content.ReadAsStringAsync();
            // Extract the response
            using JsonDocument jsonDoc = JsonDocument.Parse(result);
            string? responseStr = jsonDoc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();
            return responseStr ?? "Failed to get Llm response";
        }
    }

    public bool OnReceiveWsAsync_BasicChat(string msgCode, string msgObjStr)
    {
        switch (msgCode)
        {
            case "GetBasicChatResponseLlm":
                Utils.Logger.Info($"OnReceiveWsAsync_BasicChat(): GetChatResponseLlmBasic: '{msgObjStr}'");
                GetChatResponseLlmBasic(msgObjStr);
                return true;
            default:
                return false;
        }
    }
}