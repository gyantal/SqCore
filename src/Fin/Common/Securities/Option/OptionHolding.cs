using System;
using QuantConnect.Orders;

namespace QuantConnect.Securities.Option
{
    /// <summary>
    /// Option holdings implementation of the base securities class
    /// </summary>
    /// <seealso cref="SecurityHolding"/>
    public class OptionHolding : SecurityHolding
    {
        /// <summary>
        /// Option Holding Class constructor
        /// </summary>
        /// <param name="security">The option security being held</param>
        /// <param name="currencyConverter">A currency converter instance</param>
        public OptionHolding(Option security, ICurrencyConverter currencyConverter)
            : base(security, currencyConverter)
        {
        }
    }
}