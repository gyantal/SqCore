using System.Collections.Generic;
using QuantConnect.Data;
using QuantConnect.Interfaces;
using QuantConnect.Util;

namespace QuantConnect.Lean.Engine.DataFeeds
{
    /// <summary>
    /// Defines a container type to hold data produced by a data feed subscription
    /// </summary>
    public class DataFeedPacket
    {
        private static readonly IReadOnlyRef<bool> _false = Ref.CreateReadOnly(() => false);
        private readonly IReadOnlyRef<bool> _isRemoved;

        /// <summary>
        /// The security
        /// </summary>
        public ISecurityPrice Security
        {
            get; private set;
        }

        /// <summary>
        /// The subscription configuration that produced this data
        /// </summary>
        public SubscriptionDataConfig Configuration
        {
            get; private set;
        }

        /// <summary>
        /// Gets the number of data points held within this packet
        /// </summary>
        public int Count => Data.Count;

        /// <summary>
        /// The data for the security
        /// </summary>
        public List<BaseData> Data { get; }

        /// <summary>
        /// Gets whether or not this packet should be filtered out due to the subscription being removed
        /// </summary>
        public bool IsSubscriptionRemoved => _isRemoved.Value;

        /// <summary>
        /// Initializes a new instance of the <see cref="DataFeedPacket"/> class
        /// </summary>
        /// <param name="security">The security whose data is held in this packet</param>
        /// <param name="configuration">The subscription configuration that produced this data</param>
        /// <param name="isSubscriptionRemoved">Reference to whether or not the subscription has since been removed, defaults to false</param>
        public DataFeedPacket(ISecurityPrice security, SubscriptionDataConfig configuration, IReadOnlyRef<bool> isSubscriptionRemoved = null)
            : this(security,
                configuration,
                new List<BaseData>(4), // performance: by default the list has 0 capacity, so lets initialize it with at least 4 (which is the default)
                isSubscriptionRemoved)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DataFeedPacket"/> class
        /// </summary>
        /// <param name="security">The security whose data is held in this packet</param>
        /// <param name="configuration">The subscription configuration that produced this data</param>
        /// <param name="data">The data to add to this packet. The list reference is reused
        /// internally and NOT copied.</param>
        /// <param name="isSubscriptionRemoved">Reference to whether or not the subscription has since been removed, defaults to false</param>
        public DataFeedPacket(ISecurityPrice security, SubscriptionDataConfig configuration, List<BaseData> data, IReadOnlyRef<bool> isSubscriptionRemoved = null)
        {
            Security = security;
            Configuration = configuration;
            Data = data;
            _isRemoved = isSubscriptionRemoved ?? _false;
        }

        /// <summary>
        /// Adds the specified data to this packet
        /// </summary>
        /// <param name="data">The data to be added to this packet</param>
        public void Add(BaseData data)
        {
            Data.Add(data);
        }
    }
}
