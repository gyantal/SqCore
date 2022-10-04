using System;
using System.Threading;
using System.Threading.Tasks;
using SqCommon;
using System.Linq;
using System.Collections.Generic;
using Microsoft.Extensions.Hosting;
using System.Text;
using System.Net;
using FinTechCommon;
using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using System.Net.WebSockets;

namespace SqCoreWeb;

public partial class ExampleWs
{
    public static async Task OnWsConnectedAsync(WebSocket webSocket)
    {
        // https://stackoverflow.com/questions/24450109/how-to-send-receive-messages-through-a-web-socket-on-windows-phone-8-using-the-c
        string msgSendAtConnection = $"Example string sent from Server immediately at WebSocket connection acceptance.";
        var encoded = Encoding.UTF8.GetBytes(msgSendAtConnection);
        var buffer = new ArraySegment<Byte>(encoded, 0, encoded.Length);
        await webSocket.SendAsync(buffer, WebSocketMessageType.Text, true, CancellationToken.None);
    }

    internal static void OnWsReceiveAsync(WebSocket webSocket, string bufferStr)
    {
        // if it is not a Close-message from client, send it back temporarily
        var encoded = Encoding.UTF8.GetBytes(bufferStr);
        var buffer = new ArraySegment<Byte>(encoded, 0, encoded.Length);
        webSocket.SendAsync(buffer, WebSocketMessageType.Text, true, CancellationToken.None);
    }

    public static void OnWsClose()
    {
    }
} // class