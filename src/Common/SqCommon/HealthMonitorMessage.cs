using System;
using System.Threading.Tasks;

namespace SqCommon
{
    public class HealthMonitorMessage
    {
        static DateTime gLastMessageTime = DateTime.MinValue;   // be warned, this is global for the whole App; better to not use it, because messages can be swallowed silently. HealthMonitor itself should decide if it swallows it or not, and not the SenderApp.

        // A syntactic sugar so callers don't have to give parameters all the time as: ServerIp.HealthMonitorPublicIp, ServerIp.DefaultHealthMonitorServerPort, TcpMessageResponseFormat.None
        public static async Task SendAsync(string p_fullMsg, HealthMonitorMessageID p_healthMonId, TimeSpan? p_globalMinTimeBetweenMessages = null)
        {
            try
            {
                // p_globalMinTimeBetweenMessages: In general try to send All the exceptions and messages to HealthMonitor, even though it is CPU busy. It will be network busy anyway. It is the responsibility of HealthMonitor to decide their fate.
                // VBroker or SqCore website don't use p_globalMinTimeBetweenMessages, but maybe other crawler Apps will use it in the future. So keep the functionality, but without strong reason don't use it.
                Utils.Logger.Warn($"HealthMonitorMessage.SendAsync(): Message: '{p_fullMsg}'");
                TimeSpan globalMinTimeBetweenMessages = p_globalMinTimeBetweenMessages ?? TimeSpan.MinValue;
                if ((DateTime.UtcNow - gLastMessageTime) > globalMinTimeBetweenMessages) // Don't send it in every minute, just after e.g. 30 minutes
                {
                    bool isSendMessage = true;
                    if (Utils.IsDebugRuntimeConfig()) // in Development, we have many error messages. If it is sent to HealthMonitor app, we receive these unnecessary emails. Better to avoid.
                        isSendMessage = false;

                    if (isSendMessage)
                    {
                        Task<string?> tcpMsgTask = TcpMessage.Send(p_fullMsg, (int)p_healthMonId, ServerIp.HealthMonitorPublicIp, ServerIp.DefaultHealthMonitorServerPort, TcpMessageResponseFormat.None);
                        string? tcpMsgResponse = await tcpMsgTask;
                        if (tcpMsgTask.Exception != null || String.IsNullOrEmpty(tcpMsgResponse))
                            Utils.Logger.Error($"Error. HealthMonitorMessage.SendAsync() to {ServerIp.HealthMonitorPublicIp}:{ServerIp.DefaultHealthMonitorServerPort}");
                    }
                }
                gLastMessageTime = DateTime.UtcNow;
            }
            catch (System.Exception e) // Just in case. But probably this code is pointless here, there is no exception to catch, because "await tcpMsgTask;" catches all exceptions into tcpMsgTask.Exception
            {
                // Catch all the exceptions. If HealthMonitor server or our Internet is down we really cannot send HmMessage.
                // TcpMessage.Send() or its subroutines can give: WebSocketException (0x80004005): 'The remote party closed the WebSocket connection without completing the close handshake.'
                // If that is not caught, then it can end in TaskScheduler_UnobservedTaskException(), and that tries to send another HmMessage. And we have an infinite recursive callstack.
                SqConsole.WriteLine($"HealthMonitorMessage.SendAsync() exception. {e.Message}. We stop trying to inform HealthMonitor. We catch all exceptions avoiding infinite recursive calls.");
                Utils.Logger.Error(e, $"HealthMonitorMessage.SendAsync() exception.  We stop trying to inform HealthMonitor. We catch all exceptions avoiding infinite recursive calls.");
            }
        }
    }
}