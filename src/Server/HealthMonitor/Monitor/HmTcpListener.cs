using SqCommon;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace HealthMonitor
{
    public partial class HealthMonitor
    {
        ParallelTcpListener? m_tcpListener;

        void ProcessTcpClient(TcpClient p_tcpClient)
        {
            Utils.Logger.Info($"ProcessTcpClient() START");
            TcpMessage? message = null;
            try
            {
                BinaryReader br = new BinaryReader(p_tcpClient.GetStream());
                message = (new TcpMessage()).DeserializeFrom(br);
                if (message == null)
                {
                    Console.WriteLine("<Tcp:>" + DateTime.UtcNow.ToString("MM-dd HH:mm:ss") + $" Msg: NULL");  // user can quickly check from Console the messages
                    Utils.Logger.Info($"<Tcp:>ProcessTcpClient: Message: NULL");
                    return;
                }
                Console.WriteLine("<Tcp:>" + DateTime.UtcNow.ToString("MM-dd HH:mm:ss") + $" Msg.ID:{message.ID}, Param:{(String.IsNullOrEmpty(message.ParamStr)?"NULL": message.ParamStr)}");  // user can quickly check from Console the messages
                Utils.Logger.Info($"<Tcp:>ProcessTcpClient: Message ID:\"{ message.ID}\", ParamStr: \"{(String.IsNullOrEmpty(message.ParamStr)?"NULL": message.ParamStr)}\", ResponseFormat: \"{message.ResponseFormat}\"");
            }
            catch (Exception e) // Background thread can crash application. A background thread does not keep the managed execution environment running.
            {
                Console.WriteLine($"<Tcp:>Expected Exception. We don't rethrow it. Occurs daily when client VBroker VM server reboots or it is a second message when VBroker crashes dead. ReadTcpClientStream(BckgTh:{Thread.CurrentThread.IsBackground}). {e.Message}, InnerException: " + ((e.InnerException != null) ? e.InnerException.Message : "null"));
                Utils.Logger.Info($"<Tcp:>Expected Exception. We don't rethrow it. Occurs daily when client VBroker VM server reboots or it is a second message when VBroker crashes dead. ReadTcpClientStream(BckgTh:{Thread.CurrentThread.IsBackground}). {e.Message}, InnerException: " + ((e.InnerException != null) ? e.InnerException.Message : "null"));

                if (e is System.IO.EndOfStreamException)        // in this case, there is no message data.
                {
                    // If VBroker crashes totally, it sends a proper Message ID:"ReportErrorFromVirtualBroker" msg. once, 
                    // but next it may initiate a second message, but it cannot pump the data through, because it is already crashed and all its threads are stopped.
                    // However, don't worry, because the first VBroker message is already under processing. So, second message can be ignored.
                    // the BinaryReader couldn't read the stream, so there is no message, so we dont'n know whether message.ID = VBroker or not. It is unknown. In that case, swallow the error and return, but don't crash HealthMonitor.
                    Utils.Logger.Info($"ProcessTcpClient: System.IO.EndOfStreamException was detected. Return without crashing HealthMonitor thread.");
                    return; // there is no point processing as we don't know the data. However, we still don't want to Crash Healthmonitor. So, just swallow the error.
                }
                else
                {
                    // we may have message.ID and data and we may process it.
                }
            }

            if (message == null)
            {
                Console.WriteLine("<Tcp:>" + DateTime.UtcNow.ToString("MM-dd HH:mm:ss") + $" Msg: NULL");  // user can quickly check from Console the messages
                Utils.Logger.Info($"<Tcp:>ProcessTcpClient: Message: NULL");
                return;
            }

            if (message.ResponseFormat == TcpMessageResponseFormat.None)  // if not required to answer message, then dispose tcpClient quickly to release resources
                Utils.TcpClientDispose(p_tcpClient);

            Utils.Logger.Info($"ProcessTcpClient. Processing messageID {message.ID}.");
            switch ((HealthMonitorMessageID)message.ID)
            {
                case HealthMonitorMessageID.Ping:
                    ServePingRequest(p_tcpClient, message);
                    break;
                case HealthMonitorMessageID.TestHardCrash:
                    throw new Exception("Testing Hard Crash by Throwing this Exception");
                case HealthMonitorMessageID.ReportErrorFromVirtualBroker:
                case HealthMonitorMessageID.ReportWarningFromVirtualBroker:
                case HealthMonitorMessageID.ReportOkFromVirtualBroker:
                    MessageFromVirtualBroker(p_tcpClient, message);
                    break;
                case HealthMonitorMessageID.GetHealthMonitorCurrentStateToHealthMonitorWebsite:
                    CurrentStateToHealthMonitorWebsite(p_tcpClient, message);
                    break;
                case HealthMonitorMessageID.ReportErrorFromSQLabWebsite:
                case HealthMonitorMessageID.SqCoreWebCsError:
                case HealthMonitorMessageID.SqCoreWebJsError:
                    ErrorFromWebsite(p_tcpClient, message);
                    break;
                default:
                    StrongAssert.Fail(Severity.NoException, $"<Tcp:>ProcessTcpClient: Message ID:'{ message.ID}' is unexpected, unhandled. This probably means a serious error.");
                    break;
            }

            if (message.ResponseFormat != TcpMessageResponseFormat.None)    // if Processing needed Response to Client, we dispose here. otherwise, it was disposed before putting into processing queue
            {
                Utils.TcpClientDispose(p_tcpClient);
            }

            Utils.Logger.Info($"ProcessTcpClient() END");
        }


        internal void ServePingRequest(TcpClient p_tcpClient, TcpMessage p_message)
        {
            if (p_message.ResponseFormat == TcpMessageResponseFormat.String)
            {
                string responseStr = "Ping. Healthmonitor UtcNow: " + DateTime.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss.fff", CultureInfo.InvariantCulture);
                BinaryWriter bw = new BinaryWriter(p_tcpClient.GetStream());
                bw.Write(responseStr);                
            }
        }

    }
}
