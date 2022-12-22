namespace QuantConnect.Securities.Positions
{
    /// <summary>
    /// Defines the result for <see cref="IPositionGroupBuyingPowerModel.GetPositionGroupBuyingPower"/>
    /// </summary>
    public class PositionGroupBuyingPower
    {
        /// <summary>
        /// Gets the buying power
        /// </summary>
        public decimal Value { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="PositionGroupBuyingPower"/> class
        /// </summary>
        /// <param name="buyingPower">The buying power</param>
        public PositionGroupBuyingPower(decimal buyingPower)
        {
            Value = buyingPower;
        }

        /// <summary>
        /// Implicit operator from decimal
        /// </summary>
        public static implicit operator PositionGroupBuyingPower(decimal result)
        {
            return new PositionGroupBuyingPower(result);
        }

        /// <summary>
        /// Implicit operator to decimal
        /// </summary>
        public static implicit operator decimal(PositionGroupBuyingPower result)
        {
            return result.Value;
        }
    }
}
