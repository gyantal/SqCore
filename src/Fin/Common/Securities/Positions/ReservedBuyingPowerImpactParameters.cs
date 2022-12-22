using QuantConnect.Orders;

namespace QuantConnect.Securities.Positions
{
    /// <summary>
    /// Parameters for the <see cref="IPositionGroupBuyingPowerModel.GetReservedBuyingPowerImpact"/>
    /// </summary>
    public class ReservedBuyingPowerImpactParameters
    {
        /// <summary>
        /// Gets the position changes being contemplated
        /// </summary>
        public IPositionGroup ContemplatedChanges { get; }

        /// <summary>
        /// Gets the algorithm's portfolio manager
        /// </summary>
        public SecurityPortfolioManager Portfolio { get; }

        /// <summary>
        /// The order associated with this request
        /// </summary>
        public Order Order { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="ReservedBuyingPowerImpactParameters"/> class
        /// </summary>
        /// <param name="portfolio">The algorithm's portfolio manager</param>
        /// <param name="contemplatedChanges">The position changes being contemplated</param>
        /// <param name="order">The order associated with this request</param>
        public ReservedBuyingPowerImpactParameters(
            SecurityPortfolioManager portfolio,
            IPositionGroup contemplatedChanges,
            Order order
            )
        {
            Order = order;
            Portfolio = portfolio;
            ContemplatedChanges = contemplatedChanges;
        }
    }
}
