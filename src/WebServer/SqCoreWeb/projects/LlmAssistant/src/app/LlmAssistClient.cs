using System;
using System.Net.WebSockets;
using Fin.MemDb;

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

    public LlmAssistClient(string p_clientIP, string p_userEmail, User p_user, DateTime p_connectionTime)
    {
        ClientIP = p_clientIP;
        UserEmail = p_userEmail;
        User = p_user;
        ConnectionTime = p_connectionTime;
    }
}