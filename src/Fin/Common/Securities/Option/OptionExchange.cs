namespace QuantConnect.Securities.Option
{
    /// <summary>
    /// Option exchange class - information and helper tools for option exchange properties
    /// </summary>
    /// <seealso cref="SecurityExchange"/>
    public class OptionExchange : SecurityExchange
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
        /// Initializes a new instance of the <see cref="OptionExchange"/> class using the specified
        /// exchange hours to determine open/close times
        /// </summary>
        /// <param name="exchangeHours">Contains the weekly exchange schedule plus holidays</param>
        public OptionExchange(SecurityExchangeHours exchangeHours)
            : base(exchangeHours)
        {
        }
    }
}