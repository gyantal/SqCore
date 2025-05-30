using System;
using System.Linq;
using System.Net.Http;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Fin.MemDb;
using Microsoft.AspNetCore.Http;
using SqCommon;

namespace SqCoreWeb;
class HandshakeMessageLlmAssist
{
    public string Email { get; set; } = string.Empty;
    public int UserId { get; set; } = 0;
}
public class LlmAssistWs
{
    public static async Task OnWsConnectedAsync(HttpContext context, WebSocket webSocket)
    {
        Utils.Logger.Debug($"LlmAssistWs.OnConnectedAsync()) BEGIN");
        var userEmailClaim = context?.User?.Claims?.FirstOrDefault(p => p.Type == @"http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress");
        string? email = userEmailClaim?.Value ?? "unknown@gmail.com";
        User[] users = MemDb.gMemDb.Users; // get the user data
        User? user = Array.Find(users, r => r.Email == email); // find the user

        // https://stackoverflow.com/questions/24450109/how-to-send-receive-messages-through-a-web-socket-on-windows-phone-8-using-the-c
        HandshakeMessageLlmAssist? msgObj = new HandshakeMessageLlmAssist() { Email = email, UserId = user!.Id };
        byte[] encodedMsg = Encoding.UTF8.GetBytes("OnConnected:" + Utils.CamelCaseSerialize(msgObj));
        if (webSocket.State == WebSocketState.Open)
            await webSocket.SendAsync(new ArraySegment<Byte>(encodedMsg, 0, encodedMsg.Length), WebSocketMessageType.Text, true, CancellationToken.None);
    }

    public static void OnWsClose(WebSocket webSocket)
    {
        _ = webSocket; // StyleCop SA1313 ParameterNamesMustBeginWithLowerCaseLetter. They won't fix. Recommended solution for unused parameters, instead of the discard (_1) parameters
    }

    public static void OnWsReceiveAsync(/* HttpContext context, WebSocketReceiveResult? result, */ WebSocket webSocket, string bufferStr)
    {
        int semicolonInd = bufferStr.IndexOf(':');
        string msgCode = bufferStr[..semicolonInd];
        string msgObjStr = bufferStr[(semicolonInd + 1)..];
        switch (msgCode)
        {
            case "GetChatResponseLlm":
                Utils.Logger.Info($"LlmAssistWs.OnWsReceiveAsync(): GetChatResponseLlm: '{msgObjStr}'");
                GetChatResponseLlm(msgObjStr, webSocket);
                break;
            default:
                Utils.Logger.Info($"LlmAssistWs.OnWsReceiveAsync(): Unrecognized message from client, {msgCode},{msgObjStr}");
                break;
        }
    }

    public static void GetChatResponseLlm(string p_msg, WebSocket webSocket)
    {
        string responseStr;
        LlmUserInput? userInput = JsonSerializer.Deserialize<LlmUserInput>(p_msg, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        if (userInput == null)
            responseStr = "Invalid data";
        else
            responseStr = GenerateChatResponseLlm(userInput).Result;

        byte[] encodedMsg = Encoding.UTF8.GetBytes("LlmResponse:" + responseStr);
        if (webSocket!.State == WebSocketState.Open)
            webSocket.SendAsync(new ArraySegment<Byte>(encodedMsg, 0, encodedMsg.Length), WebSocketMessageType.Text, true, CancellationToken.None);
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
}