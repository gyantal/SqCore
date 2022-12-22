using System;
using QuantConnect.Data;
using QuantConnect.Util;
using QuantConnect.Packets;
using QuantConnect.Interfaces;
using SqCommon;

namespace QuantConnect.Lean.Engine.DataFeeds
{
    /// <summary>
    /// Entity in charge of handling data permissions
    /// </summary>
    public class DataPermissionManager : IDataPermissionManager
    {
        /// <summary>
        /// The data channel provider instance
        /// </summary>
        public IDataChannelProvider DataChannelProvider { get; private set; }

        /// <summary>
        /// Initialize the data permission manager
        /// </summary>
        /// <param name="job">The job packet</param>
        public virtual void Initialize(AlgorithmNodePacket job)
        {
            var liveJob = job as LiveNodePacket;
            if (liveJob != null)
            {
                Utils.Logger.Trace($"LiveTradingDataFeed.GetDataChannelProvider(): will use {liveJob.DataChannelProvider}");
                DataChannelProvider = Composer.Instance.GetExportedValueByTypeName<IDataChannelProvider>(liveJob.DataChannelProvider);
                DataChannelProvider.Initialize(liveJob);
            }
        }

        /// <summary>
        /// Will assert the requested configuration is valid for the current job
        /// </summary>
        /// <param name="subscriptionDataConfig">The data subscription configuration to assert</param>
        /// <param name="startTimeLocal">The start time of this request</param>
        /// <param name="endTimeLocal">The end time of this request</param>
        public virtual void AssertConfiguration(SubscriptionDataConfig subscriptionDataConfig, DateTime startTimeLocal, DateTime endTimeLocal)
        {
        }
    }
}
