namespace QuantConnect.Securities.Equity
{
    /// <summary>
    /// Equity exchange information
    /// </summary>
    /// <seealso cref="SecurityExchange"/>
    public class EquityExchange : SecurityExchange
    {
        /// <summary>
        /// Number of trading days in an equity calendar year - 252
        /// </summary>
        public override int TradingDaysPerYear
        {
            get { return 252; }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="EquityExchange"/> class using market hours
        /// derived from the market-hours-database for the USA Equity market
        /// </summary>
        public EquityExchange()
            : base(MarketHoursDatabase.FromDataFolder().GetExchangeHours(Market.USA, null, SecurityType.Equity))
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="EquityExchange"/> class using the specified
        /// exchange hours to determine open/close times
        /// </summary>
        /// <param name="exchangeHours">Contains the weekly exchange schedule plus holidays</param>
        public EquityExchange(SecurityExchangeHours exchangeHours)
            : base(exchangeHours)
        {
        }
    }
}