using System;
using System.IO;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using SqCommon;

// There is a WebSocket without SignalR in AspDotNetCore. Great. We can easily implement that.
// https://docs.microsoft.com/en-us/aspnet/core/fundamentals/websockets?view=aspnetcore-3.1

// https://docs.microsoft.com/en-us/aspnet/core/fundamentals/websockets?view=aspnetcore-3.1
// "For most applications, we recommend SignalR over raw WebSockets. SignalR provides transport fallback for environments where WebSockets is not available. 
// "It also provides a simple remote procedure call app model. And in most scenarios, SignalR has no significant performance disadvantage compared to using raw WebSockets."
// So, they admit that there is performance disadvantage, but they say it is not big.

// ***** Lesson: Native WebSocket speed advantage over bloated SignalR.
// After WebSocket implementation communication is faster than SignalR
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
// 2021-02: removed SignalR code completely, before migrating to .Net 5.0.
namespace SqCoreWeb;

class PingTimerData
{
    public PingTimerData(WebSocket p_webSocket, CancellationTokenSource p_cancelTokenSrc, DateTime p_lastPongReceived)
    {
        WebSocket = p_webSocket;
        CancelTokenSrc = p_cancelTokenSrc;
        LastPongReceived = p_lastPongReceived;
    }
    public Timer? PingTimer { get; set; } = null;
    public WebSocket WebSocket { get; set; }
    public CancellationTokenSource CancelTokenSrc { get; set; }
    public DateTime LastPongReceived { get; set; }
}

public class SqWebsocketMiddleware
{
    private static readonly NLog.Logger gLogger = NLog.LogManager.GetCurrentClassLogger();   // the name of the logger will be the "Namespace.Class"
    readonly RequestDelegate _next;

    private static int g_nSocketsKeptAliveInLoop = 0;

    public SqWebsocketMiddleware(RequestDelegate next)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
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
                catch (Exception e)
                {
                    gLogger.Error(e, $"WebSocketLoopKeptAlive(). Unexpected exception.");
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
                await DashboardWs.OnWsConnectedAsync(context, webSocket);
                break;
            case "/example-ws1":  // client sets this URL: connectionUrl.value = scheme + "://" + document.location.hostname + port + "/ws/example-ws1" ;
                await ExampleWs.OnWsConnectedAsync(webSocket);
                break;
            case "/ExSvPush":
                await ExSvPushWs.OnWsConnectedAsync(webSocket);
                break;
            default:
                throw new Exception($"Unexpected websocket connection '{p_requestRemainigPath}' in WebSocketLoopKeptAlive()");
        }

        // When the computer goes to sleep it behaves the same as if it is disconnected from the internet
        // Start heartbeats, ping-pong messages to check zombie websockets.
        // The WebSocket protocol includes support for a protocol level ping that the JavaScript API doesn't expose. It's a bit lower-level than the user level pinging that is often implemented.
        // Clients must not use pings or unsolicited pongs to aid the server; it is assumed that servers will solicit pongs whenever appropriate for the server’s needs.
        CancellationTokenSource pingCancelToken = new();    // CancelToken is not used, because that leads to a termination with costly OperationCanceledException. However, leave it in the code for future use.
        PingTimerData pingTimerData = new(webSocket, pingCancelToken, DateTime.UtcNow); // assume LastPongReceived = now, as we are opening the connection
        Timer pingTimer = new(new TimerCallback(PingTimer_Elapsed), pingTimerData, TimeSpan.FromSeconds(10), TimeSpan.FromMilliseconds(-1.0));
        pingTimerData.PingTimer = pingTimer;


        ArraySegment<Byte> buffer = new(new Byte[8192]);
        string bufferStr = string.Empty;
        WebSocketReceiveResult? result = null;
        try
        {
            // loop until the client closes the connection. The server receives a disconnect message only if the client sends it, which can't be done if the internet connection is lost.
            // If the client isn't always sending messages and you don't want to timeout just because the connection goes idle, have the client use a timer to send a ping message every X seconds. 
            // On the server, if a message hasn't arrived within 2*X seconds after the previous one, terminate the connection and report that the client disconnected.
            while (webSocket.State == WebSocketState.Open && !pingCancelToken.IsCancellationRequested && (result?.CloseStatus == null || !result.CloseStatus.HasValue))
            {

                // convert binary array to string message: https://stackoverflow.com/questions/24450109/how-to-send-receive-messages-through-a-web-socket-on-windows-phone-8-using-the-c
                bufferStr = string.Empty;
                using (var ms = new MemoryStream())
                {
                    do
                    {
                        // If client closes the window, we receive the "Dshbrd.BrowserWindowUnload" message first. Because we called _socket.send('Dshbrd.BrowserWindowUnload:') in TS. We process it.
                        // Then when we try to call webSocket.ReceiveAsync() in the next loop, it returns immediately with webSocket.State = CloseReceived, because in TS we called _socket.close()
                        result = await webSocket.ReceiveAsync(buffer, pingCancelToken.Token);  // client can send CloseStatus = NormalClosure for initiating close
                        ms.Write(buffer.Array!, buffer.Offset, result.Count);
                    } while (!result.EndOfMessage);

                    ms.Seek(0, SeekOrigin.Begin);
                    using var reader = new StreamReader(ms, Encoding.UTF8);
                    bufferStr = reader.ReadToEnd();
                }
                // gLogger.Trace($"WebSocketLoopKeptAlive(). received msg: '{bufferStr}'"); // logging takes 1ms

                // if result.CloseStatus = NormalClosure message received, or any other fault, don't pass msg to listeners. We manage complexity in this class.
                bool isGoodNonClientClosedConnection = webSocket.State == WebSocketState.Open && !pingCancelToken.IsCancellationRequested && result != null && (result.CloseStatus == null || !result.CloseStatus.HasValue);
                if (isGoodNonClientClosedConnection && result != null)
                {
                    try // processing of the message should not crash the websocket message receiving loop. Even if it throws an exception.
                    {
                        if (bufferStr.StartsWith("Pong:"))
                            pingTimerData.LastPongReceived = DateTime.UtcNow;
                        else
                            switch (p_requestRemainigPath)
                            {
                                // Run any long process (1+ sec) in separate than the WebSocket-processing thread. Otherwise any later message the client sends is queued 
                                // on the server for seconds and not processed immediately. Resulting in UI unresponsiveness at the client.
                                // The other option would be to force all messages to be run in side-thread by the WebSocketMiddleware controller, but 
                                // 1. that would be inefficient, to initiate a new Threadpool thread for 'Every' little things as most of these messages only takes 1ms processing time. 
                                // 2. that might sometimes create problems of multithreading order. The processing order of different messages is not sequential any more,
                                // they could run totally parallel, which could cause chaos in the execution logic if the algorithms assumes there is an order.
                                case "/dashboard":
                                    DashboardWs.OnWsReceiveAsync(context, webSocket, result, bufferStr);  // no await. There is no need to Wait until all of its async inner methods are completed
                                    break;
                                case "/example-ws1":
                                    ExampleWs.OnWsReceiveAsync(webSocket, bufferStr);
                                    break;
                                case "/ExSvPush":
                                    ExSvPushWs.OnWsReceiveAsync(webSocket, bufferStr);
                                    break;
                                default:
                                    throw new Exception($"Unexpected websocket connection '{p_requestRemainigPath}' in WebSocketLoopKeptAlive()");
                            }
                    }
                    catch (System.Exception e)
                    {
                        Utils.Logger.Error(e, "WebSocketLoopKeptAlive() Exception");
                        HealthMonitorMessage.SendAsync($"Exception in WebSocketLoopKeptAlive() in processing msg. Exception: '{e.ToStringWithShortenedStackTrace(1600)}'", HealthMonitorMessageID.SqCoreWebCsError).TurnAsyncToSyncTask();
                    }
                }
                // If Client never sent any proper data, and closes browser tabpage, ReceiveAsync() returns without Exception and result.CloseStatus = EndpointUnavailable
                // If Client sent any proper data, and closes browser tabpage, ReceiveAsync() returns with Exception WebSocketError.ConnectionClosedPrematurely
            } //  While
        } // try
        catch (Exception e)
        {
            if (e is OperationCanceledException oce && oce.CancellationToken == pingCancelToken.Token)
                gLogger.Trace($"WebSocketLoopKeptAlive(). Expected exception because not receiving the PONG, we cancelled the webSocket.ReceiveAsync(): '{e.Message}'");
            else if (e is WebSocketException wse && wse.WebSocketErrorCode == WebSocketError.ConnectionClosedPrematurely) // 'The remote party closed the WebSocket connection without completing the close handshake.'
                gLogger.Info($"WebSocketLoopKeptAlive(). Expected exception: '{e.Message}'");
            else
                gLogger.Error(e, $"WebSocketLoopKeptAlive(). Unexpected exception.");
        }
        finally // webSocket.Close() should be called in Finally. Otherwise: "A Task's exception(s) were not observed either by Waiting on the Task or accessing its Exception property. As a result, the unobserved exception was rethrown by the finalizer thread. ---> System.Net.WebSockets.WebSocketException: The remote party closed the WebSocket connection without completing the close handshake.
        {
            if ((webSocket.State == WebSocketState.Open || webSocket.State == WebSocketState.CloseReceived || webSocket.State == WebSocketState.CloseSent) && result?.CloseStatus != null)    // if client sends Close request then result.CloseStatus = NormalClosure and websocket.State == CloseReceived. In that case, we just answer back that we are closing.
                await webSocket.CloseAsync(result.CloseStatus.Value, result.CloseStatusDescription, CancellationToken.None); // The graceful way is CloseAsync which when initiated sends a message to the connected party, and waits for acknowledgement
            webSocket.Dispose();
            pingTimer.Dispose();
        }

        switch (p_requestRemainigPath)
        {
            case "/dashboard":
                DashboardWs.OnWsClose(webSocket);
                break;
            case "/example-ws1":
                ExampleWs.OnWsClose();
                break;
            case "/ExSvPush":
                ExSvPushWs.OnWsClose();
                break;
            default:
                throw new Exception($"Unexpected websocket connection '{p_requestRemainigPath}' in WebSocketLoopKeptAlive()");
        }
    }

    public void PingTimer_Elapsed(object? p_state) // Timer is coming on a ThreadPool thread
    {
        try
        {
            if (p_state is not PingTimerData pingTimerData)
                return;

            // We send PING, but don't wait for PONG. We check that PONG arrived only the next time this timer runs.
            // First: Check that last PONG arrived. If it hasn't arrived a long-long time ago, there is no point sending the new PING
            TimeSpan timeSinceLastPongArrived = DateTime.UtcNow - pingTimerData.LastPongReceived;
            if (timeSinceLastPongArrived > TimeSpan.FromSeconds(11 * 60))   // Close connection after 11 minutes not receivig PONG. Client should skip 2x PONG, then we are sure it is not a temporary Internet outage. 
            {
                // pingTimerData.CancelTokenSrc.Cancel();  // Option 1: ugly and costly Exception is raised. After Cancel() pingTimerData.WebSocket.State = Aborted, and webSocket.ReceiveAsync() ends with OperationCanceledException
                if (pingTimerData.WebSocket.State == WebSocketState.Open)  // Option 2: No need of Exception. CloseOutputAsync() sets WebSocket.State = CloseSent immediately. In the other thread, webSocket.ReceiveAsync() ends with State = Closed properly, and even result.CloseStatus = EndpointUnavailable.
                    pingTimerData.WebSocket.CloseOutputAsync(WebSocketCloseStatus.EndpointUnavailable, "PING failed over a long time", CancellationToken.None);  // “fire-and-forget” way to close. Don't send client the handshake and  don't wait
                return;
            }

            byte[] encodedMsg = Encoding.UTF8.GetBytes("Ping:");
            if (pingTimerData.WebSocket.State == WebSocketState.Open)
                pingTimerData.WebSocket.SendAsync(new ArraySegment<Byte>(encodedMsg, 0, encodedMsg.Length), WebSocketMessageType.Text, true, CancellationToken.None);

            if (pingTimerData.PingTimer != null)
                pingTimerData.PingTimer.Change(TimeSpan.FromSeconds(5 * 60), TimeSpan.FromMilliseconds(-1.0));     // runs every 5 minutes
        }
        catch (System.Exception)
        {
            throw;    // we can choose to swallow the exception or crash the app. If we swallow it, we might risk that error will go undetected forever.
        }

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