namespace QuantConnect.Securities.Equity
{
    /// <summary>
    /// Holdings class for equities securities: no specific properties here but it is a placeholder for future equities specific behaviours.
    /// </summary>
    /// <seealso cref="SecurityHolding"/>
    public class EquityHolding : SecurityHolding
    {
        /// <summary>
        /// Constructor for equities holdings.
        /// </summary>
        /// <param name="security">The security being held</param>
        /// <param name="currencyConverter">A currency converter instance</param>
        public EquityHolding(Security security, ICurrencyConverter currencyConverter)
            : base(security, currencyConverter)
        {
        }
    }
}