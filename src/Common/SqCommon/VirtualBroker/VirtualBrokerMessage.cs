using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

// A common base used both in VBroker and SQLab website. (E.g. for realtime price communication)
namespace SqCommon
{
    public enum VirtualBrokerMessageID      // messages To VirtualBroker (from other programs: e.g. website for realtime price)
    {
        Undefined = 0,      // for security reasons, better to give random numbers in the Tcp Communication. Fake clienst generally try to send: '0', or '1', '2', treat those as Unexpected
        GetVirtualBrokerCurrentState = 1640,   // not used at the moment. HealthMonitor may do active polling to query if VBroker is alive or not.
        GetRealtimePrice = 1641,
        GetAccountsInfo = 1642, // AccountSummary and Positions and MarketValues info
    };

    public enum VirtualBrokerMessageResponseFormat { None = 0, String, JSON };


    public class VirtualBrokerMessage
    {
        public string TcpServerHost { get; set; } = String.Empty;// = There are at least 2 Vbrokers: ManualTradingServer (for GetAccountInfo()), AutoTradingServer
        public int TcpServerPort { get; set; }      // = DefaultVirtualBrokerServerPort;

        public VirtualBrokerMessageID ID { get; set; }
        public string ParamStr { get; set; } = String.Empty;
        public VirtualBrokerMessageResponseFormat ResponseFormat { get; set; }

        public static async Task<string?> Send(string p_msg, VirtualBrokerMessageID p_vbMessageId)
        {
            return await Send(p_msg, p_vbMessageId, ServerIp.AtsVirtualBrokerServerPublicIpForClients, ServerIp.DefaultVirtualBrokerServerPort);
        }

        public static async Task<string?> Send(string p_msg, VirtualBrokerMessageID p_vbMessageId, string p_tcpServerHost, int p_tcpServerPort)
        {
            Utils.Logger.Info($"VirtualBrokerMessage.Send(): Message: '{ p_msg}'");

            var t = (new VirtualBrokerMessage()
            {
                TcpServerHost = p_tcpServerHost,
                TcpServerPort = p_tcpServerPort,
                ID = p_vbMessageId,
                ParamStr = $"{p_msg}",
                ResponseFormat = VirtualBrokerMessageResponseFormat.String
            }.SendMessage());

            string? reply = (await t);
            return reply;
        }



        public void SerializeTo(BinaryWriter p_binaryWriter)
        {
            p_binaryWriter.Write((Int32)ID);
            p_binaryWriter.Write(ParamStr);
            p_binaryWriter.Write((Int32)ResponseFormat);
        }

        public VirtualBrokerMessage DeserializeFrom(BinaryReader p_binaryReader)
        {
            ID = (VirtualBrokerMessageID)p_binaryReader.ReadInt32();
            ParamStr = p_binaryReader.ReadString();
            ResponseFormat = (VirtualBrokerMessageResponseFormat)p_binaryReader.ReadInt32();
            return this;
        }

        // 2020-06-07: After HTTP GET '/rtp' as real-time price query message sent to Vbroker, the server slowed down with 100% CPU. 
        // 1 minute later Kestrel logged: #Warn: Heartbeat took longer than "00:00:01"
        // https://github.com/dotnet/aspnetcore/issues/17321    https://github.com/dotnet/aspnetcore/issues/4760
        // this is usually happens if thread pool is starved, because it cannot find any free threads in threadpool, because each of them is blocked and waiting.
        // It is very suspicios that somehow this Tcp connection/write/read left or the DelayTask was left on, and didn't finish properly.
        // next time it happens check Top again, showing nTH (Number of Threads). In normal cases, there are 22-23 threads in the Threadpool waiting for connections from clients.
        // https://www.golinuxcloud.com/check-threads-per-process-count-processes/
        // If it goes to 50, then it shows it is really the problem that there are no free threads. Then we have to rewrite this Tcp communication code.
        // Alternatively, it might be possible ASP.Net Core 2.0 has some bug, which was later corrected in ASP.NET core 3.0, and as we migrate from SqLab to SqCore, we don't really have to worry about this.
        // 2020-06-09: same thing. Number of threads: 25-28, it is not excessive.
        public async Task<string?> SendMessage()
        {
            // https://stackoverflow.com/questions/17118632/how-to-set-the-timeout-for-a-tcpclient/43237063#43237063
            string? reply = null;
            TcpClient? client = null;
            Task? connectTask = null;
            bool wasTimeout = false;
            try
            {
                client = new TcpClient();
                connectTask = client.ConnectAsync(TcpServerHost, TcpServerPort);      // usually, we create a task with a CancellationToken. However, this task is not cancellable. I cannot cancel it. I have to wait for its finish.
                
                // Problem: if the timeout cancellation completes first we return to the caller the empty string. Fine.
                // And THEN maybe 10 minutes later the connectTask really terminates with an Exception, 
                // then, we should observe that exception, otherwise TaskScheduler.UnobservedTaskException will be raised
                // We should ALWAYS observe the connectTask.Exception (both in timeout, and no timeout cases)
                Task connectContinueTask = connectTask.ContinueWith(connTask =>
                {
                    // we should observe that exception, otherwise TaskScheduler.UnobservedTaskException will be raised
                    Utils.Logger.Info("VirtualBrokerMessage.SendMessage(). connectContinueTask BEGIN.");
                    if (connTask.Exception != null)
                        Utils.Logger.Error(connTask.Exception, "Error:VirtualBrokerMessage.SendMessage(). Exception in ConnectAsync() task.");

                    // If there was a timeout cancellation, we try to dispose it here, because we couldn't do it in the main thread.
                    if (wasTimeout && connTask.IsCompleted)
                        connTask.Dispose();
                });

                var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));        // timout 30sec. After which cts.Cancel() is called. That will trigger cancellation of the task
                var taskWithTimeoutCancellation = connectContinueTask.WithCancellation(cts.Token);
                await taskWithTimeoutCancellation;

                if (connectTask.Exception != null)
                {
                    Utils.Logger.Debug("VirtualBrokerMessage.SendMessage(). client.ConnectAsync() completed without timeout, but Exception occured.");
                }
                else
                {
                    Utils.Logger.Debug("VirtualBrokerMessage.SendMessage(). client.ConnectAsync() completed without timeout and no Exception occured");
                    BinaryWriter bw = new BinaryWriter(client.GetStream()); // sometimes "System.InvalidOperationException: The operation is not allowed on non-connected sockets." at TcpClient.GetStream()
                    SerializeTo(bw);
                    BinaryReader br = new BinaryReader(client.GetStream());
                    reply = br.ReadString(); // sometimes "System.IO.EndOfStreamException: Unable to read beyond the end of the stream." at ReadString()
                }
            }
            catch (Exception e) // in local Win development, Exception: 'No connection could be made because the target machine actively refused it' comes here.
            {
                Utils.Logger.Error(e, "Error:VirtualBrokerMessage.SendMessage exception. Check both AWS and Linux firewalls!");
                
                if (e is OperationCanceledException) {
                    wasTimeout = true;
                    Utils.Logger.Error(e, "Error:VirtualBrokerMessage.SendMessage exception. connectTask was cancelled by our timeout");
                }
                
                // we should observe that exception, otherwise TaskScheduler.UnobservedTaskException will be raised
                if (connectTask != null && connectTask.Exception != null)
                    Utils.Logger.Error(connectTask.Exception, "Error:VirtualBrokerMessage.SendMessage(). Exception in ConnectAsync() task.");
            }
            finally
            {
                // 'A task may only be disposed if it is in a completion state (RanToCompletion, Faulted or Canceled).'
                // If there was a timeout cancellation, we cannot dispose it. It is still running and it may finish 10min later and GC will dispose it. 
                if (connectTask != null && connectTask.IsCompleted)
                    connectTask.Dispose();
                Utils.TcpClientDispose(client);
            }
            return reply;  // in case of timeout, return null string to the caller.
        }

    }
}
