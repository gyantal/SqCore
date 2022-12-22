using System;
using QuantConnect.Orders;

namespace QuantConnect.Securities.Future
{
    /// <summary>
    /// Future holdings implementation of the base securities class
    /// </summary>
    /// <seealso cref="SecurityHolding"/>
    public class FutureHolding : SecurityHolding
    {
        /// <summary>
        /// Future Holding Class constructor
        /// </summary>
        /// <param name="security">The future security being held</param>
        /// <param name="currencyConverter">A currency converter instance</param>
        public FutureHolding(Security security, ICurrencyConverter currencyConverter)
            : base(security, currencyConverter)
        {
        }
    }
}