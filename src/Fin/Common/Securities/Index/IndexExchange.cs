namespace QuantConnect.Securities.Index
{
    /// <summary>
    /// INDEX exchange class - information and helper tools for Index exchange properties
    /// </summary>
    /// <seealso cref="SecurityExchange"/>
    public class IndexExchange : SecurityExchange
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
        /// Initializes a new instance of the <see cref="IndexExchange"/> class using the specified
        /// exchange hours to determine open/close times
        /// </summary>
        /// <param name="exchangeHours">Contains the weekly exchange schedule plus holidays</param>
        public IndexExchange(SecurityExchangeHours exchangeHours)
            : base(exchangeHours)
        {
        }
    }
}
