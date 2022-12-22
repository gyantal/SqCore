using System.Collections.Generic;
using QuantConnect.Data;

namespace QuantConnect.Interfaces
{
    /// <summary>
    /// Reduced interface which provides access to registered <see cref="SubscriptionDataConfig"/>
    /// </summary>
    public interface ISubscriptionDataConfigProvider
    {
        /// <summary>
        /// Gets a list of all registered <see cref="SubscriptionDataConfig"/> for a given <see cref="Symbol"/>
        /// </summary>
        /// <remarks>Will not return internal subscriptions by default</remarks>
        List<SubscriptionDataConfig> GetSubscriptionDataConfigs(Symbol symbol, bool includeInternalConfigs = false);
    }
}
