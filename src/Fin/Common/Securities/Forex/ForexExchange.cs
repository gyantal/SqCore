namespace QuantConnect.Securities.Forex
{
    /// <summary>
    /// Forex exchange class - information and helper tools for forex exchange properties
    /// </summary>
    /// <seealso cref="SecurityExchange"/>
    public class ForexExchange : SecurityExchange
    {
        /// <summary>
        /// Number of trading days per year for this security, used for performance statistics.
        /// </summary>
        public override int TradingDaysPerYear
        {
            // 365 - Saturdays = 313;
            get { return 313; }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ForexExchange"/> class using market hours
        /// derived from the market-hours-database for the FXCM Forex market
        /// </summary>
        public ForexExchange()
            : base(MarketHoursDatabase.FromDataFolder().GetExchangeHours(Market.FXCM, null, SecurityType.Forex))
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ForexExchange"/> class using the specified
        /// exchange hours to determine open/close times
        /// </summary>
        /// <param name="exchangeHours">Contains the weekly exchange schedule plus holidays</param>
        public ForexExchange(SecurityExchangeHours exchangeHours)
            : base(exchangeHours)
        {
        }
    }
}