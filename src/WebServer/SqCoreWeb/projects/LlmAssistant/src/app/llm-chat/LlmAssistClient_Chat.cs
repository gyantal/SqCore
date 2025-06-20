using System;
using System.IO;
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
    public void GetChatResponseLlm(string p_msg)
    {
        string responseStr;
        LlmUserInput? userInput = JsonSerializer.Deserialize<LlmUserInput>(p_msg, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        if (userInput == null)
            responseStr = "Invalid data";
        else
            responseStr = GenerateStreamChatResponseLlm(userInput).TurnAsyncToSyncTask();

        byte[] encodedMsg = Encoding.UTF8.GetBytes("LlmResponse:" + responseStr);
        if (WsWebSocket!.State == WebSocketState.Open)
            WsWebSocket.SendAsync(new ArraySegment<Byte>(encodedMsg, 0, encodedMsg.Length), WebSocketMessageType.Text, true, CancellationToken.None);
    }

    public static async Task<string> GenerateChatResponseLlm(LlmUserInput p_userInput)
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

    public bool OnReceiveWsAsync_Chat(string msgCode, string msgObjStr)
    {
        switch (msgCode)
        {
            case "GetChatResponseLlm":
                Utils.Logger.Info($"OnReceiveWsAsync_Chat(): GetChatResponseLlm: '{msgObjStr}'");
                GetChatResponseLlm(msgObjStr);
                return true;
            default:
                return false;
        }
    }

    public static async Task<string> GenerateStreamChatResponseLlm(LlmUserInput p_userInput)
    {
        string apiKey = string.Empty;
        string apiUrl = string.Empty;
        string apiKeyHeaderName = string.Empty; // headerName for grok/deepseek is "Authorization" and openAi is "api-key"
        string llmModelName = p_userInput.LlmModelName;
        if (llmModelName == "grok")
        {
            llmModelName = "grok-3-mini-latest";
            apiUrl = "https://api.x.ai/v1/chat/completions";
            apiKeyHeaderName = "Authorization";
            apiKey = $"Bearer {Utils.Configuration["ConnectionStrings:GrokAIApiKey"] ?? throw new SqException("GrokApiKey is missing from Config")}";
        }
        else if (llmModelName == "deepseek")
        {
            llmModelName = "deepseek-chat";
            apiUrl = "https://api.deepseek.com/v1/chat/completions";
            apiKeyHeaderName = "Authorization";
            apiKey = $"Bearer {Utils.Configuration["ConnectionStrings:DeepseekAIApiKey"] ?? throw new SqException("DeepseekAIApiKey is missing from Config")}";
        }

        if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(apiUrl))
            return "API key or URL is not configured.";
        // Prepare the request body for the chat completion
        var chatRequestBody = new
        {
            model = llmModelName,
            messages = new[]
            {
                new { role = "user", content = p_userInput.Msg }
            },
            stream = true
        };
        try
        {
            HttpClient httpClient = new HttpClient();
            // Attach API key header
            httpClient.DefaultRequestHeaders.Add(apiKeyHeaderName, apiKey);
            httpClient.DefaultRequestHeaders.Accept.ParseAdd("text/event-stream");

            using HttpRequestMessage httpRequest = new HttpRequestMessage(HttpMethod.Post, apiUrl)
            {
                Content = new StringContent(JsonSerializer.Serialize(chatRequestBody), Encoding.UTF8, "application/json")
            };

            using HttpResponseMessage httpResponse = await httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead);

            httpResponse.EnsureSuccessStatusCode();

            await using Stream stream = await httpResponse.Content.ReadAsStreamAsync();
            using StreamReader reader = new StreamReader(stream);

            StringBuilder responseSb = new StringBuilder();
            while (!reader.EndOfStream)
            {
                string? line = await reader.ReadLineAsync();
                if (string.IsNullOrWhiteSpace(line) || !line.StartsWith("data:"))
                    continue;

                string chunkData = line.Substring(5).Trim(); // e.g, chunkData => data: { "choices": [ { "delta": { "content": "Hello" } } ] }
                if (chunkData == "[DONE]")
                    break;

                using JsonDocument jsonDoc = JsonDocument.Parse(chunkData);
                JsonElement delta = jsonDoc.RootElement.GetProperty("choices")[0].GetProperty("delta");

                if (delta.TryGetProperty("content", out JsonElement contentElement))
                    responseSb.Append(contentElement.GetString());
            }
            return responseSb.ToString();
        }
        catch (Exception ex)
        {
            Utils.Logger.Error($"Error in GenerateStreamChatResponseLlm: {ex.Message}");
            return $"Error: {ex.Message}";
        }
    }
}