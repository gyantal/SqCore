using System;
using System.IO;
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
// SignalR connection ready: 180ms-400ms
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
// Benchmarks from SignalR to native Websocket: From London client to Dublin server
// >>Cold (server start first) page load: connection ready: from 380ms SignalR to 207ms
// >>Warm page load: connection ready: from 306ms SignalR to 139ms
// From Bahamas client to Dublin server
// First user data (email) arrived: 1363ms SignalR to 720ms  (saving -600ms = 0.6sec. Woooow!)
// First user data (email) arrived: 1755ms SignalR to 780ms  (saving -1000ms = 1sec. Woooow!)

// After WebSocket implementation only a tiny bit faster than SignalR
// From localhost client to localhost server:
//      First user data (email) arrived: 69ms SignalR to 50ms WebSocket  (saving -19ms = 0.02sec.)
//      First user data (email) arrived: 66ms SignalR to 61ms WebSocket  (saving -5ms = 0.02sec.)
// From London client to Dublin server
//      Cold (server start first), first user data (email) arrived: 390ms SignalR to 180ms WebSocket  (saving -210ms = 0.21sec.),
//      Warm page load, first user data (email) arrived: 294ms SignalR to 135ms WebSocket  (saving -170ms = 0.17sec.)
//      After the first user data (email), the Rt, NonRt data comes 1-2ms after. There is no difference between SignalR and WebSocket.
// From the Bahamas to Dublin server the gain is astronomical.
//      Cold (server start first), first user data (email) arrived: 3647ms SignalR to 1695ms WebSocket  (saving 2.0sec.),
//      Warm page load, first user data (email) arrived: 1594ms SignalR to 735ms WebSocket  (saving 0.8sec.)
// The conclusion is that SignalR does an extra round-trip at handshake, which is unnecessary for us, 
// and using native WebSocket we can save initial 0.2sec for London clients and 0.8-2.0 sec for Bahamas clients. 
// So, use WebSockets instead of SignalR if we can, but it is not cardinal if not.
namespace SqCoreWeb
{
    public class SqWebsocketMiddleware
    {
        private static readonly NLog.Logger gLogger = NLog.LogManager.GetCurrentClassLogger();   // the name of the logger will be the "Namespace.Class"
        readonly RequestDelegate _next;

        private static int g_nSocketsKeptAliveInLoop = 0;

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
                    Interlocked.Increment(ref g_nSocketsKeptAliveInLoop);
                    try
                    {
                        await WebSocketLoopKeptAlive(httpContext, remainingPathStr);
                        // after WebSocket is Closed many minutes later, execution continues here.
                    }
                    finally
                    {
                        Interlocked.Decrement(ref g_nSocketsKeptAliveInLoop);
                    }
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

            ArraySegment<Byte> buffer = new ArraySegment<byte>(new Byte[8192]);
            string bufferStr = String.Empty;
            WebSocketReceiveResult? result = null;
            // loop until the client closes the connection. The server receives a disconnect message only if the client sends it, which can't be done if the internet connection is lost.
            // If the client isn't always sending messages and you don't want to timeout just because the connection goes idle, have the client use a timer to send a ping message every X seconds. 
            // On the server, if a message hasn't arrived within 2*X seconds after the previous one, terminate the connection and report that the client disconnected.
            while (webSocket.State == WebSocketState.Open && (result?.CloseStatus == null || !result.CloseStatus.HasValue))
            {
                try
                {
                    // convert binary array to string message: https://stackoverflow.com/questions/24450109/how-to-send-receive-messages-through-a-web-socket-on-windows-phone-8-using-the-c
                    bufferStr = String.Empty;
                    using (var ms = new MemoryStream())
                    {
                        do
                        {
                            result = await webSocket.ReceiveAsync(buffer, CancellationToken.None);  // client can send CloseStatus = NormalClosure for initiating close
                            ms.Write(buffer.Array!, buffer.Offset, result.Count);
                        } while (!result.EndOfMessage);

                        ms.Seek(0, SeekOrigin.Begin);
                        using (var reader = new StreamReader(ms, Encoding.UTF8))
                            bufferStr = reader.ReadToEnd();
                    }
                    // gLogger.Trace($"WebSocketLoopKeptAlive(). received msg: '{bufferStr}'"); // logging takes 1ms

                    // if result.CloseStatus = NormalClosure message received, or any other fault, don't pass msg to listeners. We manage complexity in this class.
                    bool isGoodNonClientClosedConnection = webSocket.State == WebSocketState.Open && result != null && (result.CloseStatus == null || !result.CloseStatus.HasValue);
                    if (isGoodNonClientClosedConnection && result != null)
                    {
                        switch (p_requestRemainigPath)
                        {
                            case "/dashboard":
                                DashboardWs.OnReceiveAsync(context, webSocket, result, bufferStr);  // no await. There is no need to Wait until all of its async inner methods are completed
                                break;
                            case "/example-ws1":
                                ExampleWs.OnReceiveAsync(context, webSocket, result, bufferStr);
                                break;
                            default:
                                throw new Exception($"Unexpected websocket connection '{p_requestRemainigPath}' in WebSocketLoopKeptAlive()");
                        }
                    }

                    // If Client never sent any proper data, and closes browser tabpage, ReceiveAsync() returns without Exception and result.CloseStatus = EndpointUnavailable
                    // If Client sent any proper data, and closes browser tabpage, ReceiveAsync() returns with Exception WebSocketError.ConnectionClosedPrematurely
                }
                catch (System.Net.WebSockets.WebSocketException e)
                {
                    if (e.WebSocketErrorCode == WebSocketError.ConnectionClosedPrematurely) // 'The remote party closed the WebSocket connection without completing the close handshake.'
                    {
                        gLogger.Trace($"WebSocketLoopKeptAlive(). Expected exception: '{e.Message}'");
                    }
                    else
                        throw;
                }
            }
            if (result?.CloseStatus != null)    // if client sends Close request then result.CloseStatus = NormalClosure and websocket.State == CloseReceived. In that case, we just answer back that we are closing.
                await webSocket.CloseAsync(result.CloseStatus.Value, result.CloseStatusDescription, CancellationToken.None);
        }

        public static void ServerDiagnostic(StringBuilder p_sb)
        {
            // >Monitor that even though Dashboard clients exits to zero, whether zombie Websockets consume resources. 
            // >If it is a problem, then solve it that the server close the WebSocket forcefully after some inactivity with a timeout.
            // >We don't want to store in server memory too many clients, because it consumes a thread context. So, we have to kick out non-active clients after a while.

            // >study how other people solve Websocket timeout in C#
            // >Do we need that at all? If client closes Tab page, Chrome sends termination (Either Exception, or Close will be received. Loop exits. So, if tab-page is closed, it is not a problem. The only way it is a problem, if tabpage is not closed. Like user laptop sleeps, hibernates, or user goes away. In those cases, after e.g. 1-3 hours inactivity, we can exit the loop. If the user comes back from laptop sleep, it is better that he Reload the page anyway.

            // >A fix 1h limit on server is not good, because what if user just went for lunch, but comes back. He doesn't want to Refresh all the time. 
            // It is possible that user is connected for 3 hours. So the most human way for the user, if the clients are doing a heartbeat. (every 2 minutes). 
            // Connection is closed after 4 minutes of no heartbeat.
            // If user hybernates the PC, then it either refreshes the page or clients realize heartbeat is not giving  back anything, and the client reconnects with a new Websocket.
            // > If the client isn't always sending messages and you don't want to timeout just because the connection goes idle, have the client use a timer 
            // to send a ping message every X seconds. On the server, if a message hasn't arrived within 2*X seconds after the previous one, 
            // terminate the connection and report that the client disconnected.
            p_sb.Append("<H2>Websocket-Middleware</H2>");
            p_sb.Append($"#Websockets kept alive in ASP-middleware loop: {g_nSocketsKeptAliveInLoop}<br>");
        }
    }   // class
}