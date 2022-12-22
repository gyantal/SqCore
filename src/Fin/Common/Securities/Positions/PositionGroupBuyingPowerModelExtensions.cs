using QuantConnect.Orders;

namespace QuantConnect.Securities.Positions
{
    /// <summary>
    /// Provides methods aimed at reducing the noise introduced from having result/parameter types for each method.
    /// These methods aim to accept raw arguments and return the desired value type directly.
    /// </summary>
    public static class PositionGroupBuyingPowerModelExtensions
    {
        /// <summary>
        /// Gets the margin currently allocated to the specified position group
        /// </summary>
        public static decimal GetMaintenanceMargin(
            this IPositionGroupBuyingPowerModel model,
            SecurityPortfolioManager portfolio,
            IPositionGroup positionGroup
            )
        {
            return model.GetMaintenanceMargin(
                new PositionGroupMaintenanceMarginParameters(portfolio, positionGroup)
            );
        }

        /// <summary>
        /// The margin that must be held in order to change positions by the changes defined by the provided position group
        /// </summary>
        public static decimal GetInitialMarginRequirement(
            this IPositionGroupBuyingPowerModel model,
            SecurityPortfolioManager portfolio,
            IPositionGroup positionGroup
            )
        {
            return model.GetInitialMarginRequirement(
                new PositionGroupInitialMarginParameters(portfolio, positionGroup)
            ).Value;
        }

        /// <summary>
        /// Gets the total margin required to execute the specified order in units of the account currency including fees
        /// </summary>
        public static decimal GetInitialMarginRequiredForOrder(
            this IPositionGroupBuyingPowerModel model,
            SecurityPortfolioManager portfolio,
            IPositionGroup positionGroup,
            Order order
            )
        {
            return model.GetInitialMarginRequiredForOrder(
                new PositionGroupInitialMarginForOrderParameters(portfolio, positionGroup, order)
            ).Value;
        }

        /// <summary>
        /// Computes the amount of buying power reserved by the provided position group
        /// </summary>
        public static decimal GetReservedBuyingPowerForPositionGroup(
            this IPositionGroupBuyingPowerModel model,
            SecurityPortfolioManager portfolio,
            IPositionGroup positionGroup
            )
        {
            return model.GetReservedBuyingPowerForPositionGroup(
                new ReservedBuyingPowerForPositionGroupParameters(portfolio, positionGroup)
            ).AbsoluteUsedBuyingPower;
        }

        /// <summary>
        /// Check if there is sufficient buying power for the position group to execute this order.
        /// </summary>
        public static HasSufficientBuyingPowerForOrderResult HasSufficientBuyingPowerForOrder(
            this IPositionGroupBuyingPowerModel model,
            SecurityPortfolioManager portfolio,
            IPositionGroup positionGroup,
            Order order
            )
        {
            return model.HasSufficientBuyingPowerForOrder(new HasSufficientPositionGroupBuyingPowerForOrderParameters(
                portfolio, positionGroup, order
            ));
        }

        /// <summary>
        /// Gets the buying power available for a position group trade
        /// </summary>
        public static PositionGroupBuyingPower GetPositionGroupBuyingPower(
            this IPositionGroupBuyingPowerModel model,
            SecurityPortfolioManager portfolio,
            IPositionGroup positionGroup,
            OrderDirection direction
            )
        {
            return model.GetPositionGroupBuyingPower(new PositionGroupBuyingPowerParameters(
                portfolio, positionGroup, direction
            ));
        }
    }
}
