using QuantConnect.Data.Market;

namespace QuantConnect.Indicators
{
    /// <summary>
    /// Represents an Indicator of the Market Profile with Volume Profile mode and its attributes
    /// </summary>
    public class VolumeProfile: MarketProfile
    {
        /// <summary>
        /// Creates a new VolumeProfile indicator with the specified period
        /// </summary>
        /// <param name="period">The period of the indicator</param>
        public VolumeProfile(int period = 2)
            : this($"VP({period})", period)
        {
        }

        /// <summary>
        /// Creates a new VolumeProfile indicator with the specified name, period and priceRangeRoundOff
        /// </summary>
        /// <param name="name">The name of this indicator</param>
        /// <param name="period">The period of this indicator</param>
        /// <param name="valueAreaVolumePercentage">The percentage of volume contained in the value area</param>
        /// <param name="priceRangeRoundOff">How many digits you want to round and the precision.
        /// i.e 0.01 round to two digits exactly.</param>
        public VolumeProfile(string name, int period, decimal valueAreaVolumePercentage = 0.70m, decimal priceRangeRoundOff = 0.05m)
            : base(name, period, valueAreaVolumePercentage, priceRangeRoundOff)
        { }

        /// <summary>
        /// Define the Volume for the Volume Profile mode
        /// </summary>
        /// <param name="input"></param>
        /// <returns>The volume of the input Data Point</returns>
        protected override decimal GetVolume(TradeBar input)
        {
            return input.Volume;
        }
    }
}
