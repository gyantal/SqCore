namespace QuantConnect.Securities.Cfd
{
    /// <summary>
    /// CFD holdings implementation of the base securities class
    /// </summary>
    /// <seealso cref="SecurityHolding"/>
    public class CfdHolding : SecurityHolding
    {
        /// <summary>
        /// CFD Holding Class constructor
        /// </summary>
        /// <param name="security">The CFD security being held</param>
        /// <param name="currencyConverter">A currency converter instance</param>
        public CfdHolding(Cfd security, ICurrencyConverter currencyConverter)
            : base(security, currencyConverter)
        {
        }
    }
}
