namespace QuantConnect.Indicators
{
    /// <summary>
    /// This indicator computes the n-period percentage rate of change in a value using the following:
    /// 100 * (value_0 - value_n) / value_n
    /// </summary>
    public class RateOfChangePercent : RateOfChange
    {
        /// <summary>
        /// Creates a new RateOfChangePercent indicator with the specified period
        /// </summary>
        /// <param name="period">The period over which to perform to computation</param>
        public RateOfChangePercent(int period)
            : this($"ROCP({period})", period)
        {
        }

        /// <summary>
        /// Creates a new RateOfChangePercent indicator with the specified period
        /// </summary>
        /// <param name="name">The name of this indicator</param>
        /// <param name="period">The period over which to perform to computation</param>
        public RateOfChangePercent(string name, int period)
            : base(name, period)
        {
        }

        /// <summary>
        /// Computes the next value for this indicator from the given state.
        /// </summary>
        /// <param name="window">The window of data held in this indicator</param>
        /// <param name="input">The input value to this indicator on this time step</param>
        /// <returns>A new value for this indicator</returns>
        protected override decimal ComputeNextValue(IReadOnlyWindow<IndicatorDataPoint> window, IndicatorDataPoint input)
        {
            return 100 * base.ComputeNextValue(window, input);
        }
    }
}