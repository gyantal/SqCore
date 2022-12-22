using System;
using System.Collections.Generic;
using System.Threading;

namespace QuantConnect.Lean.Engine.DataFeeds
{
    /// <summary>
    /// Provides the ability to synchronize subscriptions into time slices
    /// </summary>
    public interface ISubscriptionSynchronizer
    {
        /// <summary>
        /// Event fired when a subscription is finished
        /// </summary>
        event EventHandler<Subscription> SubscriptionFinished;

        /// <summary>
        /// Syncs the specified subscriptions. The frontier time used for synchronization is
        /// managed internally and dependent upon previous synchronization operations.
        /// </summary>
        /// <param name="subscriptions">The subscriptions to sync</param>
        /// <param name="cancellationToken">The cancellation token to stop enumeration</param>
        IEnumerable<TimeSlice> Sync(IEnumerable<Subscription> subscriptions, CancellationToken cancellationToken);
    }
}