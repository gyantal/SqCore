using System;
using System.Collections.Generic;
using System.Linq;
using QuantConnect.Data.Market;
using QuantConnect.Interfaces;
using QuantConnect.Securities;
using QuantConnect.Securities.Future;

namespace QuantConnect.Data.UniverseSelection
{
    /// <summary>
    /// Defines a universe for a single futures chain
    /// </summary>
    public class FuturesChainUniverse : Universe
    {
        private readonly UniverseSettings _universeSettings;
        private DateTime _cacheDate;

        /// <summary>
        /// Initializes a new instance of the <see cref="FuturesChainUniverse"/> class
        /// </summary>
        /// <param name="future">The canonical future chain security</param>
        /// <param name="universeSettings">The universe settings to be used for new subscriptions</param>
        public FuturesChainUniverse(Future future,
            UniverseSettings universeSettings)
            : base(future.SubscriptionDataConfig)
        {
            Future = future;
            _universeSettings = new UniverseSettings(universeSettings) { DataNormalizationMode = DataNormalizationMode.Raw };
        }

        /// <summary>
        /// The canonical future chain security
        /// </summary>
        public Future Future { get; }

        /// <summary>
        /// Gets the settings used for subscriptons added for this universe
        /// </summary>
        public override UniverseSettings UniverseSettings
        {
            get { return _universeSettings; }
        }

        /// <summary>
        /// Performs universe selection using the data specified
        /// </summary>
        /// <param name="utcTime">The current utc time</param>
        /// <param name="data">The symbols to remain in the universe</param>
        /// <returns>The data that passes the filter</returns>
        public override IEnumerable<Symbol> SelectSymbols(DateTime utcTime, BaseDataCollection data)
        {
            var underlying = new Tick { Time = utcTime };

            // date change detection needs to be done in exchange time zone
            if (_cacheDate == data.Time.ConvertFromUtc(Future.Exchange.TimeZone).Date)
            {
                return Unchanged;
            }

            var availableContracts = data.Data.Select(x => x.Symbol);
            var results = Future.ContractFilter.Filter(new FutureFilterUniverse(availableContracts, underlying));

            // if results are not dynamic, we cache them and won't call filtering till the end of the day
            if (!results.IsDynamic)
            {
                _cacheDate = data.Time.ConvertFromUtc(Future.Exchange.TimeZone).Date;
            }

            return results;
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
            if (Future.Symbol.Underlying == security.Symbol)
            {
                Future.Underlying = security;
            }

            return base.GetSubscriptionRequests(security, currentTimeUtc, maximumEndTimeUtc, subscriptionService);
        }
    }
}
