using System.Collections.Generic;
using QuantConnect.Data;
using QuantConnect.Securities;

namespace QuantConnect.Interfaces
{
    /// <summary>
    /// This interface exposes methods for creating a new <see cref="Security" />
    /// </summary>
    public interface ISecurityService
    {
        /// <summary>
        /// Creates a new security
        /// </summary>
        /// <remarks>Following the obsoletion of Security.Subscriptions,
        /// both overloads will be merged removing <see cref="SubscriptionDataConfig"/> arguments</remarks>
        Security CreateSecurity(Symbol symbol,
            List<SubscriptionDataConfig> subscriptionDataConfigList,
            decimal leverage = 0,
            bool addToSymbolCache = true,
            Security underlying = null);

        /// <summary>
        /// Creates a new security
        /// </summary>
        /// <remarks>Following the obsoletion of Security.Subscriptions,
        /// both overloads will be merged removing <see cref="SubscriptionDataConfig"/> arguments</remarks>
        Security CreateSecurity(Symbol symbol,
            SubscriptionDataConfig subscriptionDataConfig,
            decimal leverage = 0,
            bool addToSymbolCache = true,
            Security underlying = null);
    }
}
