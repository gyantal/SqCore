using QuantConnect.Orders;

namespace QuantConnect.Securities.Positions
{
    /// <summary>
    /// Defines the parameters for <see cref="IPositionGroupBuyingPowerModel.GetPositionGroupBuyingPower"/>
    /// </summary>
    public class PositionGroupBuyingPowerParameters
    {
        /// <summary>
        /// Gets the position group
        /// </summary>
        public IPositionGroup PositionGroup { get; }

        /// <summary>
        /// Gets the algorithm's portfolio manager
        /// </summary>
        public SecurityPortfolioManager Portfolio { get; }

        /// <summary>
        /// Gets the direction in which buying power is to be computed
        /// </summary>
        public OrderDirection Direction { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="PositionGroupBuyingPowerParameters"/> class
        /// </summary>
        /// <param name="portfolio">The algorithm's portfolio manager</param>
        /// <param name="positionGroup">The position group</param>
        /// <param name="direction">The direction to compute buying power in</param>
        public PositionGroupBuyingPowerParameters(
            SecurityPortfolioManager portfolio,
            IPositionGroup positionGroup,
            OrderDirection direction
            )
        {
            Portfolio = portfolio;
            Direction = direction;
            PositionGroup = positionGroup;
        }

        /// <summary>
        /// Implicit operator to dependent function to remove noise
        /// </summary>
        public static implicit operator ReservedBuyingPowerForPositionGroupParameters(
            PositionGroupBuyingPowerParameters parameters
            )
        {
            return new ReservedBuyingPowerForPositionGroupParameters(parameters.Portfolio, parameters.PositionGroup);
        }
    }
}
