using QuantConnect.Data;
using QuantConnect.Packets;

namespace QuantConnect.Interfaces
{
    /// <summary>
    /// Specifies data channel settings
    /// </summary>
    public interface IDataChannelProvider
    {
        /// <summary>
        /// Initializes the class with an algorithm node packet
        /// </summary>
        /// <param name="packet">Algorithm node packet</param>
        void Initialize(AlgorithmNodePacket packet);

        /// <summary>
        /// True if this subscription configuration should be streamed
        /// </summary>
        bool ShouldStreamSubscription(SubscriptionDataConfig config);
    }
}
