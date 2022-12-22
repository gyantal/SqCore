using System;
using QuantConnect.Data;
using QuantConnect.Packets;

namespace QuantConnect.Interfaces
{
    /// <summary>
    /// Entity in charge of handling data permissions
    /// </summary>
    public interface IDataPermissionManager
    {
        /// <summary>
        /// The data channel provider instance
        /// </summary>
        IDataChannelProvider DataChannelProvider { get; }

        /// <summary>
        /// Initialize the data permission manager
        /// </summary>
        /// <param name="job">The job packet</param>
        void Initialize(AlgorithmNodePacket job);

        /// <summary>
        /// Will assert the requested configuration is valid for the current job
        /// </summary>
        /// <param name="subscriptionRequest">The data subscription configuration to assert</param>
        /// <param name="startTimeLocal">The start time of this request</param>
        /// <param name="endTimeLocal">The end time of this request</param>
        void AssertConfiguration(SubscriptionDataConfig subscriptionRequest, DateTime startTimeLocal, DateTime endTimeLocal);
    }
}
