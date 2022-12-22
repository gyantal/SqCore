namespace QuantConnect.Securities.Positions
{
    /// <summary>
    /// Defines the result for <see cref="IBuyingPowerModel.GetReservedBuyingPowerForPosition"/>
    /// </summary>
    public class ReservedBuyingPowerForPositionGroup
    {
        /// <summary>
        /// Gets the reserved buying power
        /// </summary>
        public decimal AbsoluteUsedBuyingPower { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="ReservedBuyingPowerForPosition"/> class
        /// </summary>
        /// <param name="reservedBuyingPowerForPosition">The reserved buying power for the security's holdings</param>
        public ReservedBuyingPowerForPositionGroup(decimal reservedBuyingPowerForPosition)
        {
            AbsoluteUsedBuyingPower = reservedBuyingPowerForPosition;
        }

        /// <summary>
        /// Implicit operator to <see cref="decimal"/> to remove noise
        /// </summary>
        public static implicit operator decimal(ReservedBuyingPowerForPositionGroup reservedBuyingPower)
        {
            return reservedBuyingPower.AbsoluteUsedBuyingPower;
        }

        /// <summary>
        /// Implicit operator to <see cref="decimal"/> to remove noise
        /// </summary>
        public static implicit operator ReservedBuyingPowerForPositionGroup(decimal reservedBuyingPower)
        {
            return new ReservedBuyingPowerForPositionGroup(reservedBuyingPower);
        }
    }
}
