using QuantConnect.Interfaces;
using QuantConnect.Notifications;
using QuantConnect.Packets;
using QuantConnect.Util;
using SqCommon;
using System;
using System.Collections.Generic;
using System.Linq;

namespace QuantConnect.Messaging
{
    /// <summary>
    /// Local/desktop implementation of messaging system for Lean Engine.
    /// </summary>
    public class Messaging : IMessagingHandler
    {
        /// <summary>
        /// This implementation ignores the <seealso cref="HasSubscribers"/> flag and
        /// instead will always write to the log.
        /// </summary>
        public bool HasSubscribers
        {
            get;
            set;
        }

        /// <summary>
        /// Initialize the messaging system
        /// </summary>
        /// <param name="initializeParameters">The parameters required for initialization</param>
        public void Initialize(MessagingHandlerInitializeParameters initializeParameters)
        {
            //
        }

        /// <summary>
        /// Set the messaging channel
        /// </summary>
        public virtual void SetAuthentication(AlgorithmNodePacket job)
        {
        }

        /// <summary>
        /// Send a generic base packet without processing
        /// </summary>
        public virtual void Send(Packet packet)
        {
            switch (packet.Type)
            {
                case PacketType.Debug:
                    var debug = (DebugPacket)packet;
                    Utils.Logger.Trace("Debug: " + debug.Message);
                    break;

                case PacketType.SystemDebug:
                    var systemDebug = (SystemDebugPacket)packet;
                    Utils.Logger.Trace("Debug: " + systemDebug.Message);
                    break;

                case PacketType.Log:
                    var log = (LogPacket)packet;
                    Utils.Logger.Trace("Log: " + log.Message);
                    break;

                case PacketType.RuntimeError:
                    var runtime = (RuntimeErrorPacket)packet;
                    var rstack = (!string.IsNullOrEmpty(runtime.StackTrace) ? (Environment.NewLine + " " + runtime.StackTrace) : string.Empty);
                    Utils.Logger.Error(runtime.Message + rstack);
                    break;

                case PacketType.HandledError:
                    var handled = (HandledErrorPacket)packet;
                    var hstack = (!string.IsNullOrEmpty(handled.StackTrace) ? (Environment.NewLine + " " + handled.StackTrace) : string.Empty);
                    Utils.Logger.Error(handled.Message + hstack);
                    break;

                case PacketType.AlphaResult:
                    break;

                case PacketType.BacktestResult:
                    var result = (BacktestResultPacket)packet;

                    if (result.Progress == 1)
                    {
                        // inject alpha statistics into backtesting result statistics
                        // this is primarily so we can easily regression test these values
                        var alphaStatistics = result.Results.AlphaRuntimeStatistics?.ToDictionary() ?? Enumerable.Empty<KeyValuePair<string, string>>();
                        foreach (var kvp in alphaStatistics)
                        {
                            result.Results.Statistics.Add(kvp);
                        }

                        var orderHash = result.Results.Orders.GetHash();
                        result.Results.Statistics.Add("OrderListHash", orderHash);

                        var statisticsStr = $"{Environment.NewLine}" +
                            $"{string.Join(Environment.NewLine, result.Results.Statistics.Select(x => $"STATISTICS:: {x.Key} {x.Value}"))}";
                        Utils.Logger.Trace(statisticsStr);
                    }
                    break;
            }
        }

        /// <summary>
        /// Send any notification with a base type of Notification.
        /// </summary>
        public void SendNotification(Notification notification)
        {
            var type = notification.GetType();
            if (type == typeof(NotificationEmail)
             || type == typeof(NotificationWeb)
             || type == typeof(NotificationSms)
             || type == typeof(NotificationTelegram))
            {
                Utils.Logger.Error("Messaging.SendNotification(): Send not implemented for notification of type: " + type.Name);
                return;
            }
            notification.Send();
        }

        /// <summary>
        /// Dispose of any resources
        /// </summary>
        public void Dispose()
        {
        }
    }
}
