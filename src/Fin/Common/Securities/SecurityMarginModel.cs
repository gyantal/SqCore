namespace QuantConnect.Securities
{
    /// <summary>
    /// Represents a simple, constant margin model by specifying the percentages of required margin.
    /// </summary>
    public class SecurityMarginModel : BuyingPowerModel
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SecurityMarginModel"/> with no leverage (1x)
        /// </summary>
        public SecurityMarginModel()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SecurityMarginModel"/>
        /// </summary>
        /// <param name="initialMarginRequirement">The percentage of an order's absolute cost
        /// that must be held in free cash in order to place the order</param>
        /// <param name="maintenanceMarginRequirement">The percentage of the holding's absolute
        /// cost that must be held in free cash in order to avoid a margin call</param>
        /// <param name="requiredFreeBuyingPowerPercent">The percentage used to determine the required
        /// unused buying power for the account.</param>
        public SecurityMarginModel(
            decimal initialMarginRequirement,
            decimal maintenanceMarginRequirement,
            decimal requiredFreeBuyingPowerPercent
            )
            : base(initialMarginRequirement, maintenanceMarginRequirement, requiredFreeBuyingPowerPercent)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SecurityMarginModel"/>
        /// </summary>
        /// <param name="leverage">The leverage</param>
        /// <param name="requiredFreeBuyingPowerPercent">The percentage used to determine the required
        /// unused buying power for the account.</param>
        public SecurityMarginModel(decimal leverage, decimal requiredFreeBuyingPowerPercent = 0)
            : base(leverage, requiredFreeBuyingPowerPercent)
        {
        }
    }
}