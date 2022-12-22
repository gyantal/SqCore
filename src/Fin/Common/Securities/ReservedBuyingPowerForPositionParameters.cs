namespace QuantConnect.Securities
{
    /// <summary>
    /// Defines the parameters for <see cref="IBuyingPowerModel.GetReservedBuyingPowerForPosition"/>
    /// </summary>
    public class ReservedBuyingPowerForPositionParameters
    {
        /// <summary>
        /// Gets the security
        /// </summary>
        public Security Security { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="ReservedBuyingPowerForPositionParameters"/> class
        /// </summary>
        /// <param name="security">The security</param>
        public ReservedBuyingPowerForPositionParameters(Security security)
        {
            Security = security;
        }

        /// <summary>
        /// Creates the result using the specified reserved buying power in units of the account currency
        /// </summary>
        /// <param name="reservedBuyingPower">The reserved buying power in units of the account currency</param>
        /// <returns>The reserved buying power</returns>
        public ReservedBuyingPowerForPosition ResultInAccountCurrency(decimal reservedBuyingPower)
        {
            return new ReservedBuyingPowerForPosition(reservedBuyingPower);
        }
    }
}