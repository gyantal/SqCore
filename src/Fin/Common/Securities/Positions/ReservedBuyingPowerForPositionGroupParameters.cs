namespace QuantConnect.Securities.Positions
{
    /// <summary>
    /// Defines the parameters for <see cref="IBuyingPowerModel.GetReservedBuyingPowerForPosition"/>
    /// </summary>
    public class ReservedBuyingPowerForPositionGroupParameters
    {
        /// <summary>
        /// Gets the <see cref="IPositionGroup"/>
        /// </summary>
        public IPositionGroup PositionGroup { get; }

        /// <summary>
        /// Gets the algorithm's portfolio manager
        /// </summary>
        public SecurityPortfolioManager Portfolio { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="ReservedBuyingPowerForPositionGroupParameters"/> class
        /// </summary>
        /// <param name="portfolio">The algorithm's portfolio manager</param>
        /// <param name="positionGroup">The position group</param>
        public ReservedBuyingPowerForPositionGroupParameters(
            SecurityPortfolioManager portfolio,
            IPositionGroup positionGroup
            )
        {
            Portfolio = portfolio;
            PositionGroup = positionGroup;
        }
    }
}
