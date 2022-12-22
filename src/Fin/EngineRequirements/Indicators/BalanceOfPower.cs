using QuantConnect.Data.Market;

namespace QuantConnect.Indicators
{
    /// <summary>
    /// This indicator computes the Balance Of Power (BOP).
    /// The Balance Of Power is calculated with the following formula:
    /// BOP = (Close - Open) / (High - Low)
    /// </summary>
    public class BalanceOfPower : BarIndicator, IIndicatorWarmUpPeriodProvider
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="BalanceOfPower"/> class using the specified name.
        /// </summary>
        public BalanceOfPower()
            : this("BOP")
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="BalanceOfPower"/> class using the specified name.
        /// </summary>
        /// <param name="name">The name of this indicator</param>
        public BalanceOfPower(string name)
            : base(name)
        {
        }

        /// <summary>
        /// Gets a flag indicating when this indicator is ready and fully initialized
        /// </summary>
        public override bool IsReady => Samples > 0;

        /// <summary>
        /// Required period, in data points, for the indicator to be ready and fully initialized.
        /// </summary>
        public int WarmUpPeriod => 1;

        /// <summary>
        /// Computes the next value of this indicator from the given state
        /// </summary>
        /// <param name="input">The input given to the indicator</param>
        /// <returns>A new value for this indicator</returns>
        protected override decimal ComputeNextValue(IBaseDataBar input)
        {
            var range = input.High - input.Low;
            return range > 0 ? (input.Close - input.Open) / range : 0m;
        }
    }
}