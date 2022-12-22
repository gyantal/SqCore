using System.Collections.Generic;
using QuantConnect.Data;

namespace QuantConnect.Interfaces
{
    /// <summary>
    ///     AlgorithmSubscriptionManager interface will manage the subscriptions for the SubscriptionManager
    /// </summary>
    public interface IAlgorithmSubscriptionManager : ISubscriptionDataConfigService
    {
        /// <summary>
        ///     Gets all the current data config subscriptions that are being processed for the SubscriptionManager
        /// </summary>
        IEnumerable<SubscriptionDataConfig> SubscriptionManagerSubscriptions { get; }

        /// <summary>
        ///     Returns the amount of data config subscriptions processed for the SubscriptionManager
        /// </summary>
        int SubscriptionManagerCount();
    }
}
