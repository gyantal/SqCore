using QuantConnect.Orders;

namespace QuantConnect.Securities.Positions
{
    /// <summary>
    /// Defines parameters for <see cref="IPositionGroupBuyingPowerModel.GetInitialMarginRequiredForOrder"/>
    /// </summary>
    public class PositionGroupInitialMarginForOrderParameters
    {
        /// <summary>
        /// Gets the algorithm's portfolio manager
        /// </summary>
        public SecurityPortfolioManager Portfolio { get; }

        /// <summary>
        /// Gets the position group
        /// </summary>
        public IPositionGroup PositionGroup { get; }

        /// <summary>
        /// Gets the order
        /// </summary>
        public Order Order { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="PositionGroupInitialMarginForOrderParameters"/> class
        /// </summary>
        /// <param name="portfolio">The algorithm's portfolio manager</param>
        /// <param name="positionGroup">The position group</param>
        /// <param name="order">The order</param>
        public PositionGroupInitialMarginForOrderParameters(
            SecurityPortfolioManager portfolio,
            IPositionGroup positionGroup,
            Order order
            )
        {
            Portfolio = portfolio;
            PositionGroup = positionGroup;
            Order = order;
        }
    }
}
