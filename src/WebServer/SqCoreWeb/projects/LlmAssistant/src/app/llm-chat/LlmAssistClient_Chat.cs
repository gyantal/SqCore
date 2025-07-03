using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Azure.AI.OpenAI;
using SqCommon;

namespace SqCoreWeb;

public partial class LlmAssistClient
{
    public void GetChatResponseLlm(string p_msg)
    {
        string? responseStr = GenerateStreamChatResponseLlm(p_msg).TurnAsyncToSyncTask();
        if (responseStr != null)
        {
            byte[] encodedMsg = Encoding.UTF8.GetBytes("LlmResponse:" + responseStr);
            if (WsWebSocket!.State == WebSocketState.Open)
                WsWebSocket.SendAsync(new ArraySegment<Byte>(encodedMsg, 0, encodedMsg.Length), WebSocketMessageType.Text, true, CancellationToken.None);
        }
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
            case "ClearLlmResponse":
                Utils.Logger.Info($"OnReceiveWsAsync_Chat(): ClearLlmResponse:");
                m_chatMessages.Clear();
                return true;
            default:
                return false;
        }
    }

    public async Task<string?> GenerateStreamChatResponseLlm(string p_msg)
    {
        LlmUserInput? userInput = JsonSerializer.Deserialize<LlmUserInput>(p_msg, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        if (userInput == null)
            return "Error: Invalid user input";
        string apiKey = string.Empty;
        string apiUrl = string.Empty;
        string apiKeyHeaderName = string.Empty; // headerName for grok/deepseek is "Authorization" and openAi is "api-key"
        string llmModelName = userInput.LlmModelName;
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
            return "Error: API key or URL is not configured.";

        m_chatMessages.Add(new ChatMessage { Role = "user", Content = userInput.Msg });
        // when we supply the m_chatMessages to the ChatRequestBody we are getting error: "Response status code does not indicate success: 422 (Unprocessable Entity)". Because the messages Structure is expecting List<Object> where as m_chatMessages is List<ChatMessage>.
        // The Role is ChatRole in m_chatMessages but in requestbody its a string type. so the structure is not supporting.
        List<object> msgs = new();
        foreach (ChatMessage chat in m_chatMessages)
            msgs.Add(new { role = chat.Role.ToString(), content = chat.Content });
        // Prepare the request body for the chat completion
        var chatRequestBody = new
        {
            model = llmModelName,
            messages = msgs,
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

            StringBuilder responseChunkSb = new StringBuilder(); // partial chunks for every(500ms)
            StringBuilder fullResponseSb = new(); // accumulates full response
            Stopwatch sw = Stopwatch.StartNew();
            while (!reader.EndOfStream)
            {
                string? streamedJsonData = await reader.ReadLineAsync(); // every single word (token) is sent in a separate package, example response: https://docs.x.ai/docs/guides/streaming-response.
                if (string.IsNullOrWhiteSpace(streamedJsonData) || !streamedJsonData.StartsWith("data:"))
                    continue;
                // e.g. data: { "choices": [ { "delta": { "content": "Hello" } } ] }
                string chunkData = streamedJsonData.Substring(5).Trim(); // remove "data:" prefix
                if (chunkData == "[DONE]")
                    break;

                using JsonDocument parsedChunkJson = JsonDocument.Parse(chunkData); // chunkData => { "choices": [ { "delta": { "content": "Hello" } } ] }
                JsonElement deltaElement = parsedChunkJson.RootElement.GetProperty("choices")[0].GetProperty("delta");

                if (!deltaElement.TryGetProperty("content", out JsonElement contentElement))
                    continue;
                string? token = contentElement.GetString(); // e.g. "Hello" is the token
                if (token is null)
                    continue;

                responseChunkSb.Append(token);
                fullResponseSb.Append(token);

                if (sw.ElapsedMilliseconds >= 500) // send the responsechunk for every 500ms
                {
                    byte[] encodedMsg = Encoding.UTF8.GetBytes("LlmResponse:" + responseChunkSb);
                    if (WsWebSocket!.State == WebSocketState.Open)
                        await WsWebSocket.SendAsync(new ArraySegment<Byte>(encodedMsg, 0, encodedMsg.Length), WebSocketMessageType.Text, true, CancellationToken.None);
                    responseChunkSb.Clear();
                    sw.Restart(); // reset the stopwatch
                }
            }
            if (responseChunkSb.Length > 0) // for handling the last responseChunk once the stream ends
            {
                byte[] encodedMsg = Encoding.UTF8.GetBytes("LlmResponse:" + responseChunkSb);
                if (WsWebSocket!.State == WebSocketState.Open)
                    await WsWebSocket.SendAsync(new ArraySegment<Byte>(encodedMsg, 0, encodedMsg.Length), WebSocketMessageType.Text, true, CancellationToken.None);
                responseChunkSb.Clear();
                sw.Restart(); // reset the stopwatch
            }
            m_chatMessages.Add(new ChatMessage { Role = "assistant", Content = fullResponseSb.ToString() });
            return null;
        }
        catch (Exception ex)
        {
            Utils.Logger.Error($"Error in GenerateStreamChatResponseLlm: {ex.Message}");
            return $"Error in GenerateStreamChatResponseLlm: {ex.Message}";
        }
    }
}