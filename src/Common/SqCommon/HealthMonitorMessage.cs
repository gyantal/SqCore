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
    }
}