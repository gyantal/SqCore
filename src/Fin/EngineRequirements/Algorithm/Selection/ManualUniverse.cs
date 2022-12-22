using System;
using System.Collections.Generic;
using System.Linq;
using QuantConnect.Data;
using QuantConnect.Data.UniverseSelection;
using QuantConnect.Interfaces;
using QuantConnect.Securities;

namespace QuantConnect.Algorithm.Framework.Selection
{
    /// <summary>
    /// Defines a universe as a set of manually set symbols. This differs from <see cref="UserDefinedUniverse"/>
    /// in that these securities were not added via AddSecurity.
    /// </summary>
    /// <remarks>Incompatible with multiple <see cref="Universe"/> selecting the same <see cref="Symbol"/>.
    /// with different <see cref="SubscriptionDataConfig"/>. More information <see cref="GetSubscriptionRequests"/></remarks>
    public class ManualUniverse : UserDefinedUniverse
    {
        /// <summary>
        /// Creates a new instance of the <see cref="ManualUniverse"/>
        /// </summary>
        public ManualUniverse(SubscriptionDataConfig configuration,
            UniverseSettings universeSettings,
            IEnumerable<Symbol> symbols)
            : base(configuration, universeSettings, Time.MaxTimeSpan, symbols)
        {
        }

        /// <summary>
        /// Creates a new instance of the <see cref="ManualUniverse"/>
        /// </summary>
        public ManualUniverse(SubscriptionDataConfig configuration,
            UniverseSettings universeSettings,
            Symbol[] symbols)
            : base(configuration, universeSettings, Time.MaxTimeSpan, symbols)
        {
        }

        /// <summary>
        /// Gets the subscription requests to be added for the specified security
        /// </summary>
        /// <param name="security">The security to get subscriptions for</param>
        /// <param name="currentTimeUtc">The current time in utc. This is the frontier time of the algorithm</param>
        /// <param name="maximumEndTimeUtc">The max end time</param>
        /// <param name="subscriptionService">Instance which implements <see cref="ISubscriptionDataConfigService"/> interface</param>
        /// <returns>All subscriptions required by this security</returns>
        public override IEnumerable<SubscriptionRequest> GetSubscriptionRequests(Security security, DateTime currentTimeUtc, DateTime maximumEndTimeUtc,
            ISubscriptionDataConfigService subscriptionService)
        {
            // ManualUniverse will return any existing SDC for the symbol, else will create new, using universe settings.
            // This is for maintaining existing behavior and preventing breaking changes: Specifically motivated
            // by usages of Algorithm.Securities.Keys as constructor parameter of the ManualUniverseSelectionModel.
            // Symbols at Algorithm.Securities.Keys added by Addxxx() calls will already be added by the UserDefinedUniverse.

            var existingSubscriptionDataConfigs = subscriptionService.GetSubscriptionDataConfigs(security.Symbol);

            if (existingSubscriptionDataConfigs.Any())
            {
                return existingSubscriptionDataConfigs.Select(
                    config => new SubscriptionRequest(isUniverseSubscription: false,
                        universe: this,
                        security: security,
                        configuration: config,
                        startTimeUtc: currentTimeUtc,
                        endTimeUtc: maximumEndTimeUtc));
            }
            return base.GetSubscriptionRequests(security, currentTimeUtc, maximumEndTimeUtc, subscriptionService);
        }
    }
}