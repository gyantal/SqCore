using System.Linq;
using QuantConnect.Orders;

namespace QuantConnect.Securities.Positions
{
    /// <summary>
    /// Defines the parameters for <see cref="IPositionGroupBuyingPowerModel.HasSufficientBuyingPowerForOrder"/>
    /// </summary>
    public class HasSufficientPositionGroupBuyingPowerForOrderParameters
    {
        /// <summary>
        /// Gets the order
        /// </summary>
        public Order Order { get; }

        /// <summary>
        /// Gets the position group representing the holdings changes contemplated by the order
        /// </summary>
        public IPositionGroup PositionGroup { get; }

        /// <summary>
        /// Gets the algorithm's portfolio manager
        /// </summary>
        public SecurityPortfolioManager Portfolio { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="HasSufficientPositionGroupBuyingPowerForOrderParameters"/> class
        /// </summary>
        /// <param name="portfolio">The algorithm's portfolio manager</param>
        /// <param name="positionGroup">The position group</param>
        /// <param name="order">The order</param>
        public HasSufficientPositionGroupBuyingPowerForOrderParameters(
            SecurityPortfolioManager portfolio,
            IPositionGroup positionGroup,
            Order order
            )
        {
            Order = order;
            Portfolio = portfolio;
            PositionGroup = positionGroup;
        }

        /// <summary>
        /// This may be called for non-combo type orders where the position group is guaranteed to have exactly one position
        /// </summary>
        public static implicit operator HasSufficientBuyingPowerForOrderParameters(
            HasSufficientPositionGroupBuyingPowerForOrderParameters parameters
            )
        {
            var position = parameters.PositionGroup.Single();
            var security = parameters.Portfolio.Securities[position.Symbol];
            return new HasSufficientBuyingPowerForOrderParameters(parameters.Portfolio, security, parameters.Order);
        }

        /// <summary>
        /// Creates a new result indicating that there is sufficient buying power for the contemplated order
        /// </summary>
        public HasSufficientBuyingPowerForOrderResult Sufficient()
        {
            return new HasSufficientBuyingPowerForOrderResult(true);
        }

        /// <summary>
        /// Creates a new result indicating that there is insufficient buying power for the contemplated order
        /// </summary>
        public HasSufficientBuyingPowerForOrderResult Insufficient(string reason)
        {
            return new HasSufficientBuyingPowerForOrderResult(false, reason);
        }

        /// <summary>
        /// Creates a new result indicating that there was an error
        /// </summary>
        public HasSufficientBuyingPowerForOrderResult Error(string reason)
        {
            return new HasSufficientBuyingPowerForOrderResult(false, reason);
        }
    }
}
