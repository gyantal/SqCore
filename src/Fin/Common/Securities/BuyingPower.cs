namespace QuantConnect.Securities
{
    /// <summary>
    /// Defines the result for <see cref="IBuyingPowerModel.GetBuyingPower"/>
    /// </summary>
    public class BuyingPower
    {
        /// <summary>
        /// Gets the buying power
        /// </summary>
        public decimal Value { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="BuyingPower"/> class
        /// </summary>
        /// <param name="buyingPower">The buying power</param>
        public BuyingPower(decimal buyingPower)
        {
            Value = buyingPower;
        }
    }
}