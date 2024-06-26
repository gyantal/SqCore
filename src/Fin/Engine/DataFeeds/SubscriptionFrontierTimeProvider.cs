﻿using System;

namespace QuantConnect.Lean.Engine.DataFeeds
{
    /// <summary>
    /// A time provider which updates 'now' time based on the current data emit time of all subscriptions
    /// </summary>
    /// <remarks>This class is not thread safe but there is no need for it to be since it's only consumed by the
    /// <see cref="SubscriptionSynchronizer"/></remarks>
    public class SubscriptionFrontierTimeProvider : ITimeProvider
    {
        private static readonly long MaxDateTimeTicks = DateTime.MaxValue.Ticks;
        private DateTime _utcNow;
        private readonly IDataFeedSubscriptionManager _subscriptionManager;

        /// <summary>
        /// Creates a new instance of the SubscriptionFrontierTimeProvider
        /// </summary>
        /// <param name="utcNow">Initial UTC now time</param>
        /// <param name="subscriptionManager">Subscription manager. Will be used to obtain current subscriptions</param>
        public SubscriptionFrontierTimeProvider(DateTime utcNow, IDataFeedSubscriptionManager subscriptionManager)
        {
            _utcNow = utcNow;
            _subscriptionManager = subscriptionManager;
        }

        /// <summary>
        /// Gets the current time in UTC
        /// </summary>
        /// <returns>The current time in UTC</returns>
        public DateTime GetUtcNow()
        {
            UpdateCurrentTime();
            return _utcNow;
        }

        /// <summary>
        /// Sets the current time calculated as the minimum current data emit time of all the subscriptions.
        /// If there are no subscriptions current time will remain unchanged
        /// </summary>
        private void UpdateCurrentTime()
        {
            long earlyBirdTicks = MaxDateTimeTicks;
            foreach (var subscription in _subscriptionManager.DataFeedSubscriptions)
            {
                // this if should just be 'subscription.Current == null' but its affected by GH issue 3914
                if (// this is a data subscription we just added
                    // lets move it next to find the initial emit time
                    subscription.Current == null
                    && !subscription.IsUniverseSelectionSubscription
                    && subscription.UtcStartTime == _utcNow
                    ||
                    // UserDefinedUniverse, through the AddData calls
                    // will add new universe selection data points when is has too
                    // so lets move it next to check if there is any
                    subscription.Current == null
                    && subscription.IsUniverseSelectionSubscription
                    && subscription.UtcStartTime != _utcNow)
                {
                    subscription.MoveNext();
                }

                if (subscription.Current != null)
                {
                    if (earlyBirdTicks == MaxDateTimeTicks)
                    {
                        earlyBirdTicks = subscription.Current.EmitTimeUtc.Ticks;
                    }
                    else
                    {
                        // take the earliest between the next piece of data or the current earliest bird
                        earlyBirdTicks = Math.Min(earlyBirdTicks, subscription.Current.EmitTimeUtc.Ticks);
                    }
                }
            }

            if (earlyBirdTicks != MaxDateTimeTicks)
            {
                _utcNow = new DateTime(Math.Max(earlyBirdTicks, _utcNow.Ticks), DateTimeKind.Utc);
            }
        }
    }
}
