namespace QuantConnect.Securities.Crypto
{
    /// <summary>
    /// Crypto holdings implementation of the base securities class
    /// </summary>
    /// <seealso cref="SecurityHolding"/>
    public class CryptoHolding : SecurityHolding
    {
        /// <summary>
        /// Crypto Holding Class
        /// </summary>
        /// <param name="security">The Crypto security being held</param>
        /// <param name="currencyConverter">A currency converter instance</param>
        public CryptoHolding(Crypto security, ICurrencyConverter currencyConverter)
            : base(security, currencyConverter)
        {
        }
    }
}