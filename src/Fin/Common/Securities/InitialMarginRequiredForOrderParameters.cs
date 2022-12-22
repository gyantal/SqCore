using QuantConnect.Orders;

namespace QuantConnect.Securities
{
    /// <summary>
    /// Defines the parameters for <see cref="BuyingPowerModel.GetInitialMarginRequiredForOrder"/>
    /// </summary>
    public class InitialMarginRequiredForOrderParameters
    {
        /// <summary>
        /// Gets the security
        /// </summary>
        public Security Security { get; }

        /// <summary>
        /// Gets the order
        /// </summary>
        public Order Order { get; }

        /// <summary>
        /// Gets the currency converter
        /// </summary>
        public ICurrencyConverter CurrencyConverter { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="InitialMarginRequiredForOrderParameters"/> class
        /// </summary>
        /// <param name="currencyConverter">The currency converter</param>
        /// <param name="security">The security</param>
        /// <param name="order">The order</param>
        public InitialMarginRequiredForOrderParameters(
            ICurrencyConverter currencyConverter,
            Security security,
            Order order
            )
        {
            Order = order;
            Security = security;
            CurrencyConverter = currencyConverter;
        }
    }
}
