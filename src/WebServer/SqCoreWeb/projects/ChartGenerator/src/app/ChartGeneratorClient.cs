// using System;
// using System.Collections.Generic;
// using System.Net.WebSockets;
// using System.Text;
// using System.Text.Json.Serialization;
// using System.Threading;
// using Fin.MemDb;
// using QuantConnect;
// using SqCommon;

    // Yet to Develop - Daya;
// namespace SqCoreWeb;

// class HandshakeChartGenerator // Initial params: keept it small
// {
//     public string UserName { get; set; } = string.Empty;
// }

// public partial class DashboardClient
// {
//     // Return from this function very quickly. Do not call any Clients.Caller.SendAsync(), because client will not notice that connection is Connected, and therefore cannot send extra messages until we return here
//     public void OnConnectedWsAsync_ChartGenerator(bool p_isThisActiveToolAtConnectionInit)
//     {
//         Utils.RunInNewThread(ignored => // running parallel on a ThreadPool thread, FireAndForget: QueueUserWorkItem [26microsec] is 25% faster than Task.Run [35microsec]
//         {
//             Utils.Logger.Debug($"OnConnectedWsAsync_ChartGenerator BEGIN, Connection from IP: {this.ClientIP} with email '{this.UserEmail}'");
//             Thread.CurrentThread.IsBackground = true;  // thread will be killed when all foreground threads have died, the thread will not keep the application alive.
//             HandshakeChartGenerator handshake = GetHandshakeChartGenerator();
//             byte[] encodedMsg = Encoding.UTF8.GetBytes("ChartGenerator.Handshake:" + Utils.CamelCaseSerialize(handshake));
//             if (WsWebSocket!.State == WebSocketState.Open)
//                 WsWebSocket.SendAsync(new ArraySegment<Byte>(encodedMsg, 0, encodedMsg.Length), WebSocketMessageType.Text, true, CancellationToken.None);
//             // Assuming this tool is not the main Tab page on the client, we delay sending all the data, to avoid making the network and client too busy an unresponsive
//             if (!p_isThisActiveToolAtConnectionInit)
//                 Thread.Sleep(TimeSpan.FromMilliseconds(5000));
//         });
//     }
//     private HandshakeChartGenerator GetHandshakeChartGenerator()
//     {
//         return new HandshakeChartGenerator() { UserName = User.Username };
//     }
// }