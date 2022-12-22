using QuantConnect.Data.Market;

namespace QuantConnect.Indicators
{
    /// <summary>
    /// Represents an Indicator of the Market Profile with Time Price Opportunity (TPO) mode and its attributes
    /// </summary>
    public class TimeProfile: MarketProfile
    {
        /// <summary>
        /// Creates a new TimeProfile indicator with the specified period
        /// </summary>
        /// <param name="period">The period of this indicator</param>
        public TimeProfile(int period = 2)
            : this($"TP({period})", period)
        {
        }

        /// <summary>
        /// Creates a new TimeProfile indicator with the specified name, period and priceRangeRoundOff
        /// </summary>
        /// <param name="name">The name of this indicator</param>
        /// <param name="period">The period of this indicator</param>
        /// <param name="valueAreaVolumePercentage">The percentage of volume contained in the value area</param>
        /// <param name="priceRangeRoundOff">How many digits you want to round and the precision.
        /// i.e 0.01 round to two digits exactly.</param>
        public TimeProfile(string name, int period, decimal valueAreaVolumePercentage = 0.70m, decimal priceRangeRoundOff = 0.05m)
            : base(name, period, valueAreaVolumePercentage, priceRangeRoundOff)
        { }

        /// <summary>
        /// Define the Volume in Time Profile mode
        /// </summary>
        /// <param name="input"></param>
        /// <returns>1</returns>
        protected override decimal GetVolume(TradeBar input)
        {
            return 1;
        }
    }
}
