using System;
using QuantConnect.Data;
using QuantConnect.Interfaces;
using QuantConnect.Securities;

namespace QuantConnect.Orders.Fills
{
    /// <summary>
    /// Defines the parameters for the <see cref="IFillModel"/> method
    /// </summary>
    public class FillModelParameters
    {
        /// <summary>
        /// Gets the <see cref="Security"/>
        /// </summary>
        public Security Security { get; }

        /// <summary>
        /// Gets the <see cref="Order"/>
        /// </summary>
        public Order Order { get; }

        /// <summary>
        /// Gets the <see cref="SubscriptionDataConfig"/> provider
        /// </summary>
        public ISubscriptionDataConfigProvider ConfigProvider { get; }

        /// <summary>
        /// Gets the minimum time span elapsed to consider a market fill price as stale (defaults to one hour)
        /// </summary>
        public TimeSpan StalePriceTimeSpan { get; }

        /// <summary>
        /// Creates a new instance
        /// </summary>
        /// <param name="security">Security asset we're filling</param>
        /// <param name="order">Order packet to model</param>
        /// <param name="configProvider">The <see cref="ISubscriptionDataConfigProvider"/> to use</param>
        /// <param name="stalePriceTimeSpan">The minimum time span elapsed to consider a fill price as stale</param>
        public FillModelParameters(
            Security security,
            Order order,
            ISubscriptionDataConfigProvider configProvider,
            TimeSpan stalePriceTimeSpan)
        {
            Security = security;
            Order = order;
            ConfigProvider = configProvider;
            StalePriceTimeSpan = stalePriceTimeSpan;
        }
    }
}