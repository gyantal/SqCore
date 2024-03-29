using System;
using System.Collections.Generic;
using QuantConnect.Securities;

namespace QuantConnect.Data.UniverseSelection
{
    /// <summary>
    /// Provides a universe decoration that replaces the implementation of <see cref="GetSubscriptionRequests"/>
    /// </summary>
    public class GetSubscriptionRequestsUniverseDecorator : UniverseDecorator
    {
        private readonly GetSubscriptionRequestsDelegate _getRequests;

        /// <summary>
        /// Delegate type for the <see cref="GetSubscriptionRequests"/> method
        /// </summary>
        /// <param name="security">The security to get subscription requests for</param>
        /// <param name="currentTimeUtc">The current utc frontier time</param>
        /// <param name="maximumEndTimeUtc"></param>
        /// <returns>The subscription requests for the security to be given to the data feed</returns>
        public delegate IEnumerable<SubscriptionRequest> GetSubscriptionRequestsDelegate(Security security, DateTime currentTimeUtc, DateTime maximumEndTimeUtc);

        /// <summary>
        /// Initializes a new instance of the <see cref="GetSubscriptionRequestsUniverseDecorator"/> class
        /// </summary>
        /// <param name="universe">The universe to be decorated</param>
        /// <param name="getRequests"></param>
        public GetSubscriptionRequestsUniverseDecorator(Universe universe, GetSubscriptionRequestsDelegate getRequests)
            : base(universe)
        {
            _getRequests = getRequests;
        }

        /// <summary>
        /// Gets the subscription requests to be added for the specified security
        /// </summary>
        /// <param name="security">The security to get subscriptions for</param>
        /// <param name="currentTimeUtc">The current time in utc. This is the frontier time of the algorithm</param>
        /// <param name="maximumEndTimeUtc">The max end time</param>
        /// <returns>All subscriptions required by this security</returns>
        public override IEnumerable<SubscriptionRequest> GetSubscriptionRequests(Security security, DateTime currentTimeUtc, DateTime maximumEndTimeUtc)
        {
            return _getRequests(security, currentTimeUtc, maximumEndTimeUtc);
        }
    }
}