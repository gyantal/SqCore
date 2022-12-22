using System;
using System.ComponentModel.Composition;
using QuantConnect.Notifications;
using QuantConnect.Packets;

namespace QuantConnect.Interfaces
{
    /// <summary>
    /// Messaging System Plugin Interface. 
    /// Provides a common messaging pattern between desktop and cloud implementations of QuantConnect.
    /// </summary>
    [InheritedExport(typeof(IMessagingHandler))]
    public interface IMessagingHandler : IDisposable
    {
        /// <summary>
        /// Gets or sets whether this messaging handler has any current subscribers.
        /// When set to false, messages won't be sent.
        /// </summary>
        bool HasSubscribers { get; set; }

        /// <summary>
        /// Initialize the Messaging System Plugin. 
        /// </summary>
        /// <param name="initializeParameters">The parameters required for initialization</param>
        void Initialize(MessagingHandlerInitializeParameters initializeParameters);

        /// <summary>
        /// Set the user communication channel
        /// </summary>
        /// <param name="job">The job packet</param>
        void SetAuthentication(AlgorithmNodePacket job);

        /// <summary>
        /// Send any message with a base type of Packet.
        /// </summary>
        /// <param name="packet">Packet of data to send via the messaging system plugin</param>
        void Send(Packet packet);

        /// <summary>
        /// Send any notification with a base type of Notification.
        /// </summary>
        /// <param name="notification">The notification to be sent.</param>
        void SendNotification(Notification notification);
    }
}
