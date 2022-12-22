namespace QuantConnect.Securities.Future
{
    /// <summary>
    /// Future exchange class - information and helper tools for future exchange properties
    /// </summary>
    /// <seealso cref="SecurityExchange"/>
    public class FutureExchange : SecurityExchange
    {
        /// <summary>
        /// Number of trading days per year for this security, 252.
        /// </summary>
        /// <remarks>Used for performance statistics to calculate sharpe ratio accurately</remarks>
        public override int TradingDaysPerYear
        {
            get { return 252; }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="FutureExchange"/> class using the specified
        /// exchange hours to determine open/close times
        /// </summary>
        /// <param name="exchangeHours">Contains the weekly exchange schedule plus holidays</param>
        public FutureExchange(SecurityExchangeHours exchangeHours)
            : base(exchangeHours)
        {
        }
    }
}