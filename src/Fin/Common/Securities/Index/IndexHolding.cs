namespace QuantConnect.Securities.Index
{
    /// <summary>
    /// Index holdings implementation of the base securities class
    /// </summary>
    /// <seealso cref="SecurityHolding"/>
    public class IndexHolding : SecurityHolding
    {
        /// <summary>
        /// INDEX Holding Class constructor
        /// </summary>
        /// <param name="security">The INDEX security being held</param>
        /// <param name="currencyConverter">A currency converter instance</param>
        public IndexHolding(Index security, ICurrencyConverter currencyConverter)
            : base(security, currencyConverter)
        {
        }
    }
}
