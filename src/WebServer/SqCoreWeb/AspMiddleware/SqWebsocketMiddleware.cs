using System;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

// There is a WebSocket without SignalR in AspDotNetCore. Great. We can easily implement that.
// https://docs.microsoft.com/en-us/aspnet/core/fundamentals/websockets?view=aspnetcore-3.1

// With SignalR Production (Linux server) shows it takes 350ms for SignalR connection (start()) to be established, and another 350ms when server sends back data to client
// That is too slow. This implementation does vanilla WebSockets instead of the SignalR package (that also uses WebSockets, but we cannot control its inside working)
// However, see in wwwroot\webapps\ExampleWebSocket\index.html , pure WebSocket connection is opened in 22-25ms (server cold start: 47ms, only once), and first data arrives in just 1ms later. 
// So, the first data of SignalR 2*350ms =700ms can be reduced to 
// Dublin server (98-125ms)(because Dublin has a 25-30ms ping latency), local Windows (25ms)(localhost has a 1ms ping latency) if we send All the data instantly to Client. 
// that 100ms probably cannot be decreased lower, because the localhost 25ms is with an 1ms latency, so the 25ms is a pure CPU cost on the client.ask + server.answer + client.send side again. (assuming 10ms each)
// and if we assume a 30ms latency, let's say twice, then the 30latency+30latency+25ms CPU gives 85ms, that is what we got in the Dublin server environment. This cannot be decreased further.
// It is worth using pure Websockets, although we have to implement timeout logic

// https://docs.microsoft.com/en-us/aspnet/core/fundamentals/websockets?view=aspnetcore-3.1
// "For most applications, we recommend SignalR over raw WebSockets. SignalR provides transport fallback for environments where WebSockets is not available. 
// "It also provides a simple remote procedure call app model. And in most scenarios, SignalR has no significant performance disadvantage compared to using raw WebSockets."
// So, they admit that there is performance disadvantage, but they say it is not big.

// ***** Lesson: Native WebSocket speed advantage over bloated SignalR.
// 1.
// >SignalR implementation https://dashboard.sqcore.net/ on Linux server.
// SignalR connection ready: 180ms-400ms (which is fine-ish, but there is no possibility to send data there)
// SignalR FIRST userdata + nonRealTime data arrives: 440-900ms in Chrome, 400ms in Edge. (it could be 130ms, so a 3-6x faster for first data to arrive.) So SignalR is badly implemented. Probably on the Browser side.
// 2.
// >Pure WebSocket (not SignalR) implementation mockup: https://sqcore.net/webapps/ExampleWebSocket/index.html on Linux server.
// cold start: onopen() : 392ms, on FIRSTmessage() : 399ms
// warm start: onopen() : 136ms, on FIRSTmessage() : 142ms
// warm start2: onopen(): 113ms, on FIRSTmessage() :  114ms
// Ms Edge: in onopen() : 101ms, on FIRSTmessage() : 102ms
// So, if FIRSTmessage() can arrive in 120ms in real Linux server in Dublin, then why the SignalR implementation of dashboard.sqcore.net takes 450-900ms?
// This example suggested that this speed can be improved.
// 3.
// >Native Websocket implementation is a total success. https://dashboard.sqcore.net/ on SqCore.Net Linux server:
// Benchmarks from SignalR to native Websocket:
// >>Cold (server start first) page load: 
// connection ready: from 380ms to 207ms
// First user data (email) arrived: 939ms to 208ms  (saving -730ms = 0.73sec. Woooow!)
// >>Warm page load:
// connection ready: from 306ms to 139ms
// First user data (email) arrived: 747ms to 139ms  (saving -600ms = 0.6sec. Woooow!)
// Reliability of performance: Furthermore, SignalR speed is very volatile even in warm load. 'First user data (email)' arrives in a range of 750-1500ms (yes, sometimes 1.5s), 
// while native Websocket range is 120-140ms. There is no big 1500ms delays ever. 
namespace SqCoreWeb
{
    public class SqWebsocketMiddleware
    {
        private static readonly NLog.Logger gLogger = NLog.LogManager.GetCurrentClassLogger();   // the name of the logger will be the "Namespace.Class"
        readonly RequestDelegate _next;

        public SqWebsocketMiddleware(RequestDelegate next)
        {
            if (next == null)
                throw new ArgumentNullException(nameof(next));
            _next = next;

        }

        public async Task Invoke(HttpContext httpContext)
        {
            if (httpContext == null)
                throw new ArgumentNullException(nameof(httpContext));

            // https://github.com/dotnet/aspnetcore/issues/2713  search "/ws" instead of  the intended "/ws/", otherwise it will be not found
            if (httpContext.Request.Path.StartsWithSegments("/ws", StringComparison.OrdinalIgnoreCase, out PathString remainingPathStr))
            {
                if (httpContext.WebSockets.IsWebSocketRequest)
                {
                    await WebSocketLoopKeptAlive(httpContext, remainingPathStr);
                    // after WebSocket is Closed many minutes later, execution continues here.
                    return; // it is handled, don't allow execution further, because 'StatusCode cannot be set because the response has already started.'
                }
                else
                {
                    httpContext.Response.StatusCode = 400;  // 400 Bad Request
                }
            }
            await _next(httpContext);
        }

        // When using a WebSocket, you must keep this middleware pipeline running for the duration of the connection. 
        // The code receives a message and immediately sends back the same message. Messages are sent and received in a loop until the client closes the connection.
        private async Task WebSocketLoopKeptAlive(HttpContext context, string p_requestRemainigPath)
        {
            WebSocket webSocket = await context.WebSockets.AcceptWebSocketAsync();  // this accept immediately send back the client that connection is accepted.

            switch (p_requestRemainigPath)
            {
                case "/dashboard":
                    await DashboardWs.OnConnectedAsync(context, webSocket);
                    break;
                case "/example-ws1":
                    await ExampleWs.OnConnectedAsync(context, webSocket);
                    break;
                default:
                    throw new Exception($"Unexpected websocket connection '{p_requestRemainigPath}' in WebSocketLoopKeptAlive()");
            }

            var buffer = new byte[1024 * 4];
            WebSocketReceiveResult? result = null;
            // loop until the client closes the connection. The server receives a disconnect message only if the client sends it, which can't be done if the internet connection is lost.
            // If the client isn't always sending messages and you don't want to timeout just because the connection goes idle, have the client use a timer to send a ping message every X seconds. 
            // On the server, if a message hasn't arrived within 2*X seconds after the previous one, terminate the connection and report that the client disconnected.
            while (webSocket.State == WebSocketState.Open && (result?.CloseStatus == null || !result.CloseStatus.HasValue))
            {
                try
                {
                    result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None); // waiting for any received message or a Close message
                    // If Client never sent any proper data, and closes browser tabpage, ReceiveAsync() returns without Exception and result.CloseStatus = EndpointUnavailable
                    // If Client sent any proper data, and closes browser tabpage, ReceiveAsync() returns with Exception WebSocketError.ConnectionClosedPrematurely
                }
                catch (System.Net.WebSockets.WebSocketException e)
                {
                    if (e.WebSocketErrorCode == WebSocketError.ConnectionClosedPrematurely) // 'The remote party closed the WebSocket connection without completing the close handshake.'
                    {
                        gLogger.Trace($"Expected exception: {e.Message}");
                    }
                    else
                        throw;
                }

                bool isGoodNonClientClosedConnection = webSocket.State == WebSocketState.Open && result != null && (result.CloseStatus == null || !result.CloseStatus.HasValue);
                
                if (isGoodNonClientClosedConnection && result != null) { // if it is not a Close-message from client, send it back temporarily
                    await webSocket.SendAsync(new ArraySegment<byte>(buffer, 0, result.Count), result.MessageType, result.EndOfMessage, CancellationToken.None);
                }
            }
            if (result?.CloseStatus != null)    // if client sends Close request then result.CloseStatus = NormalClosure and websocket.State == CloseReceived. In that case, we just answer back that we are closing.
                await webSocket.CloseAsync(result.CloseStatus.Value, result.CloseStatusDescription, CancellationToken.None);
        }
    }   // class
}