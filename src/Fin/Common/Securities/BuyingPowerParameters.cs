using QuantConnect.Orders;

namespace QuantConnect.Securities
{
    /// <summary>
    /// Defines the parameters for <see cref="IBuyingPowerModel.GetBuyingPower"/>
    /// </summary>
    public class BuyingPowerParameters
    {
        /// <summary>
        /// Gets the security
        /// </summary>
        public Security Security { get; }

        /// <summary>
        /// Gets the algorithm's portfolio
        /// </summary>
        public SecurityPortfolioManager Portfolio { get; }

        /// <summary>
        /// Gets the direction in which buying power is to be computed
        /// </summary>
        public OrderDirection Direction { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="BuyingPowerParameters"/> class
        /// </summary>
        /// <param name="portfolio">The algorithm's portfolio</param>
        /// <param name="security">The security</param>
        /// <param name="direction">The direction to compute buying power in</param>
        public BuyingPowerParameters(SecurityPortfolioManager portfolio, Security security, OrderDirection direction)
        {
            Portfolio = portfolio;
            Security = security;
            Direction = direction;
        }

        /// <summary>
        /// Creates the result using the specified buying power
        /// </summary>
        /// <param name="buyingPower">The buying power</param>
        /// <param name="currency">The units the buying power is denominated in</param>
        /// <returns>The buying power</returns>
        public BuyingPower Result(decimal buyingPower, string currency)
        {
            // TODO: Properly account for 'currency' - not accounted for currently as only performing mechanical refactoring
            return new BuyingPower(buyingPower);
        }

        /// <summary>
        /// Creates the result using the specified buying power in units of the account currency
        /// </summary>
        /// <param name="buyingPower">The buying power</param>
        /// <returns>The buying power</returns>
        public BuyingPower ResultInAccountCurrency(decimal buyingPower)
        {
            return new BuyingPower(buyingPower);
        }
    }
}