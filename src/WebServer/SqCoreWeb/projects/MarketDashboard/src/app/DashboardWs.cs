using Microsoft.AspNetCore.SignalR;
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

namespace SqCoreWeb
{
    public partial class DashboardWs
    {
        public static async Task OnConnectedAsync(HttpContext context, WebSocket webSocket)
        {
            var userEmailClaim = context?.User?.Claims?.FirstOrDefault(p => p.Type == @"http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress");
            var email = userEmailClaim?.Value ?? "unknown@gmail.com";

            // https://stackoverflow.com/questions/24450109/how-to-send-receive-messages-through-a-web-socket-on-windows-phone-8-using-the-c
            string msgSendAtConnection = $"[{{\"email\":\"{email}\",\"anyParam\":55}}]"; // see HandshakeMessage serialization in DashboardPushHub
            var encoded = Encoding.UTF8.GetBytes(msgSendAtConnection);
            var bufferFirst = new ArraySegment<Byte>(encoded, 0, encoded.Length);
            if (webSocket.State == WebSocketState.Open)
                await webSocket.SendAsync(bufferFirst, WebSocketMessageType.Text, true, CancellationToken.None);
        }

    }   // class
}