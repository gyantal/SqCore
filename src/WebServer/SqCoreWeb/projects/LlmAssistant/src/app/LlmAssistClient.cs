using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using Azure.AI.OpenAI;
using Fin.MemDb;
using SqCommon;

namespace SqCoreWeb;

public partial class LlmAssistClient
{
    public string ClientIP { get; set; } = string.Empty; // Remote Client IP for WebSocket
    public string UserEmail { get; set; } = string.Empty;
    public User? User { get; set; }
    public DateTime ConnectionTime { get; set; } = DateTime.MinValue;
    public WebSocket? WsWebSocket { get; set; } = null; // this pointer uniquely identifies the WebSocket as it is not released until websocket is dead

    public string ConnectionIdStr // calculated field: a debugger friendly way of identifying the same websocket, in case WebSocket pointer is not good enough
    {
        get { return this.ClientIP + "@" + ConnectionTime.ToString("MM'-'dd'T'HH':'mm':'ss"); }
    }

    List<ChatMessage> m_basicChatMessages = new();
    List<ChatMessage> m_chatMessages = new(); // the general 'sophisticated' chat messages

    internal static List<LlmAssistClient> g_llmAssistClients = new(); // Multithread warning! Lockfree Read | Copy-Modify-Swap Write Pattern

    public LlmAssistClient(string p_clientIP, string p_userEmail, User p_user, DateTime p_connectionTime)
    {
        ClientIP = p_clientIP;
        UserEmail = p_userEmail;
        User = p_user;
        ConnectionTime = p_connectionTime;
    }

    public bool OnReceiveWsAsync_LlmAssistClient(string msgCode, string msgObjStr)
    {
        switch (msgCode)
        {
            case "LlmAssist.IsLlmAssistOpenManyTimes":
                Utils.Logger.Info($"OnReceiveWsAsync__LlmAssistClient(): IsLlmAssistOpenManyTimes:{msgObjStr}");
                // SendIsLlmAssistOpenManyTimes(); // yet to implement
                return true;
            default:
                bool isHandled = OnReceiveWsAsync_Chat(msgCode, msgObjStr);
                if (!isHandled)
                    isHandled = OnReceiveWsAsync_BasicChat(msgCode, msgObjStr);
                if (!isHandled)
                    isHandled = OnReceiveWsAsync_Scan(msgCode, msgObjStr);
                if (!isHandled)
                    isHandled = OnReceiveWsAsync_PromptAssist(msgCode, msgObjStr);
                return isHandled;
        }
    }

    public static LlmAssistClient? FindClient(WebSocket? p_webSocket)
    {
        return LlmAssistClient.g_llmAssistClients.Find(r => r.WsWebSocket == p_webSocket);
    }

    public static void AddToClients(LlmAssistClient p_client)
    {
        // !Warning: Multithreaded Warning: The Modifier (Writer) thread should be careful, and Copy and Pointer-Swap when Edit/Remove is done.
        lock (LlmAssistClient.g_llmAssistClients) // lock assures that there are no 2 threads that is Adding at the same time on Cloned g_glients.
        {
            List<LlmAssistClient> clonedClients = new(LlmAssistClient.g_llmAssistClients)
            {
                p_client // equivalent to clonedClients.Add(p_client);
            }; // adding new item to clone assures that no enumerating reader threads will throw exception.
            LlmAssistClient.g_llmAssistClients = clonedClients;
        }
    }

    public static void RemoveFromClients(LlmAssistClient p_client)
    {
        // 'beforeunload' will be fired if the user submits a form, clicks a link, closes the window (or tab), or goes to a new page using the address bar, search box, or a bookmark.
        // server removes this client from LlmAssistClient.g_llmAssistClients list

        // !Warning: Multithreaded Warning: The Modifier (Writer) thread should be careful, and Copy and Pointer-Swap when Edit/Remove is done.
        lock (LlmAssistClient.g_llmAssistClients)
        {
            List<LlmAssistClient> clonedClients = new(LlmAssistClient.g_llmAssistClients);
            clonedClients.Remove(p_client);
            LlmAssistClient.g_llmAssistClients = clonedClients;
        }
    }
}