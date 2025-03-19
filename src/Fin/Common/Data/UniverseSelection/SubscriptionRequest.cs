using System;
using System.Collections.Generic;
using QuantConnect.Securities;

namespace QuantConnect.Data.UniverseSelection
{
    /// <summary>
    /// Defines the parameters required to add a subscription to a data feed.
    /// </summary>
    public class SubscriptionRequest : BaseDataRequest
    {
        /// <summary>
        /// Gets true if the subscription is a universe
        /// </summary>
        public bool IsUniverseSubscription { get; }

        /// <summary>
        /// Gets the universe this subscription resides in
        /// </summary>
        public Universe Universe { get; }

        /// <summary>
        /// Gets the security. This is the destination of data for non-internal subscriptions.
        /// </summary>
        public Security Security { get; }

        /// <summary>
        /// Gets the subscription configuration. This defines how/where to read the data.
        /// </summary>
        public SubscriptionDataConfig Configuration { get; }

        /// <summary>
        /// Gets the tradable days specified by this request, in the security's data time zone
        /// </summary>
        public override IEnumerable<DateTime> TradableDays => Time.EachTradeableDayInTimeZone(Security.Exchange.Hours,
            StartTimeLocal,
                EndTimeLocal,
                Configuration.DataTimeZone,
                Configuration.ExtendedMarketHours);

        /// <summary>
        /// Initializes a new instance of the <see cref="SubscriptionRequest"/> class
        /// </summary>
        public SubscriptionRequest(bool isUniverseSubscription,
            Universe universe,
            Security security,
            SubscriptionDataConfig configuration,
            DateTime startTimeUtc,
            DateTime endTimeUtc)
            : base(startTimeUtc, endTimeUtc, security.Exchange.Hours, configuration.TickType)
        {
            // SqCore Change NEW:
            // Note that in TradeBar.Parse(stream) we do AddHours(-8) to shift the StartTime (we also shift dividend and splits with AddHours(16) to shift those again to the 16:00 time). So, Backtests work fine.
            // Don't put the -8 hours shift into the Base class, because HistoryRequest doesn't need that. HistoryRequest has a proper UTC input, that is not modified later.
            // When Algorithm.Init called SetEndDate(2022-02-13) as Local, endTimeUtc comes here as 2022-02-14 4:59, because SetEndDate applied a Midnight -1 tick, converted to UTC. We convert this 5:00 to previous day 21:00
            EndTimeUtc = endTimeUtc.AddHours(-8);
            StartTimeUtc = startTimeUtc.AddHours(-8);
            // SqCore Change END

            IsUniverseSubscription = isUniverseSubscription;
            Universe = universe;
            Security = security;
            Configuration = configuration;

            // open interest data comes in once a day before market open,
            // make the subscription start from midnight and use always open exchange
            if (Configuration.TickType == TickType.OpenInterest)
            {
                StartTimeUtc = StartTimeUtc.ConvertFromUtc(ExchangeHours.TimeZone).Date.ConvertToUtc(ExchangeHours.TimeZone);
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SubscriptionRequest"/> class
        /// </summary>
        public SubscriptionRequest(SubscriptionRequest template,
            bool? isUniverseSubscription = null,
            Universe universe = null,
            Security security = null,
            SubscriptionDataConfig configuration = null,
            DateTime? startTimeUtc = null,
            DateTime? endTimeUtc = null
            )
            : this(isUniverseSubscription ?? template.IsUniverseSubscription,
                  universe ?? template.Universe,
                  security ?? template.Security,
                  configuration ?? template.Configuration,
                  startTimeUtc ?? template.StartTimeUtc,
                  endTimeUtc ?? template.EndTimeUtc
                  )
        {
        }
    }
}
