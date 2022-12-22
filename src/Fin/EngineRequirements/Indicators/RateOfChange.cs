namespace QuantConnect.Indicators
{
    /// <summary>
    /// This indicator computes the n-period rate of change in a value using the following:
    /// (value_0 - value_n) / value_n
    /// </summary>
    public class RateOfChange : WindowIndicator<IndicatorDataPoint>, IIndicatorWarmUpPeriodProvider
    {
        /// <summary>
        /// Gets a flag indicating when this indicator is ready and fully initialized.
        /// </summary>
        public override bool IsReady => Samples > Period;

        /// <summary>
        /// Required period, in data points, for the indicator to be ready and fully initialized.
        /// Our formula is Period + 1 because we need to fill the window and have one removed before
        /// it is ready.
        /// </summary>
        public int WarmUpPeriod => Period + 1;

        /// <summary>
        /// Creates a new RateOfChange indicator with the specified period
        /// </summary>
        /// <param name="period">The period over which to perform to computation</param>
        public RateOfChange(int period)
            : base($"ROC({period})", period)
        {
        }

        /// <summary>
        /// Creates a new RateOfChange indicator with the specified period
        /// </summary>
        /// <param name="name">The name of this indicator</param>
        /// <param name="period">The period over which to perform to computation</param>
        public RateOfChange(string name, int period)
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
            // if we're not ready just grab the first input point in the window
            var denominator = window.Samples <= window.Size ? window[window.Count - 1] : window.MostRecentlyRemoved;

            if (denominator.Value == 0)
            {
                return 0;
            }

            return (input.Value - denominator.Value) / denominator.Value;
        }
    }
}
