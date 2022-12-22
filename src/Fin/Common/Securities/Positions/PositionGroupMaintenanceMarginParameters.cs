namespace QuantConnect.Securities.Positions
{
    /// <summary>
    /// Defines parameters for <see cref="IPositionGroupBuyingPowerModel.GetMaintenanceMargin"/>
    /// </summary>
    public class PositionGroupMaintenanceMarginParameters
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
        /// Initializes a new instance of the <see cref="PositionGroupMaintenanceMarginParameters"/> class
        /// </summary>
        /// <param name="portfolio">The algorithm's portfolio manager</param>
        /// <param name="positionGroup">The position group</param>
        public PositionGroupMaintenanceMarginParameters(
            SecurityPortfolioManager portfolio,
            IPositionGroup positionGroup
            )
        {
            Portfolio = portfolio;
            PositionGroup = positionGroup;
        }
    }
}
