using System;
using QuantConnect.Data;
using QuantConnect.Data.UniverseSelection;

namespace QuantConnect.Lean.Engine.DataFeeds
{
    /// <summary>
    /// DataFeedSubscriptionManager interface will manage the subscriptions for the Data Feed
    /// </summary>
    public interface IDataFeedSubscriptionManager
    {
        /// <summary>
        /// Event fired when a new subscription is added
        /// </summary>
        event EventHandler<Subscription> SubscriptionAdded;

        /// <summary>
        /// Event fired when an existing subscription is removed
        /// </summary>
        event EventHandler<Subscription> SubscriptionRemoved;

        /// <summary>
        /// Gets the data feed subscription collection
        /// </summary>
        SubscriptionCollection DataFeedSubscriptions { get; }

        /// <summary>
        /// Get the universe selection instance
        /// </summary>
        UniverseSelection UniverseSelection { get; }

        /// <summary>
        /// Removes the <see cref="Subscription"/>, if it exists
        /// </summary>
        /// <param name="configuration">The <see cref="SubscriptionDataConfig"/> of the subscription to remove</param>
        /// <param name="universe">Universe requesting to remove <see cref="Subscription"/>.
        /// Default value, null, will remove all universes</param>
        /// <returns>True if the subscription was successfully removed, false otherwise</returns>
        bool RemoveSubscription(SubscriptionDataConfig configuration, Universe universe = null);

        /// <summary>
        /// Adds a new <see cref="Subscription"/> to provide data for the specified security.
        /// </summary>
        /// <param name="request">Defines the <see cref="SubscriptionRequest"/> to be added</param>
        /// <returns>True if the subscription was created and added successfully, false otherwise</returns>
        bool AddSubscription(SubscriptionRequest request);
    }
}
