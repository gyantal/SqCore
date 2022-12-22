namespace QuantConnect.Securities
{
    /// <summary>
    /// Defines the result for <see cref="IBuyingPowerModel.GetReservedBuyingPowerForPosition"/>
    /// </summary>
    public class ReservedBuyingPowerForPosition
    {
        /// <summary>
        /// Gets the reserved buying power
        /// </summary>
        public decimal AbsoluteUsedBuyingPower { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="ReservedBuyingPowerForPosition"/> class
        /// </summary>
        /// <param name="reservedBuyingPowerForPosition">The reserved buying power for the security's holdings</param>
        public ReservedBuyingPowerForPosition(decimal reservedBuyingPowerForPosition)
        {
            AbsoluteUsedBuyingPower = reservedBuyingPowerForPosition;
        }
    }
}