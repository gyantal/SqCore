namespace QuantConnect.Indicators
{
    /// <summary>
    /// The signal for the Relative Vigor Index, itself an indicator. 
    /// </summary>
    public class RelativeVigorIndexSignal : Indicator, IIndicatorWarmUpPeriodProvider
    {
        private readonly RollingWindow<IndicatorDataPoint> _rollingRvi;

        /// <summary>
        /// Initializes the signal term.
        /// </summary>
        /// <param name="name"></param>
        protected internal RelativeVigorIndexSignal(string name)
            : base(name) // Accessibility set to prevent out-of-scope use
        {
            WarmUpPeriod = 3;
            _rollingRvi = new RollingWindow<IndicatorDataPoint>(3);
        }

        /// <summary>
        /// Gets a flag indicating when this indicator is ready and fully initialized
        /// </summary>
        public override bool IsReady => _rollingRvi.IsReady;

        /// <summary>
        /// Resets this indicator to its initial state
        /// </summary>
        public int WarmUpPeriod { get; }

        /// <summary>
        /// Computes the next value of this indicator from the given state
        /// </summary>
        /// <param name="input">The input given to the indicator</param>
        /// <returns>A new value for this indicator</returns>
        protected override decimal ComputeNextValue(IndicatorDataPoint input)
        {
            if (IsReady)
            {
                var output = (input.Value + 2 * (_rollingRvi[0] + _rollingRvi[1]) + _rollingRvi[2]) / 6;
                _rollingRvi.Add(input);
                return output;
            }

            _rollingRvi.Add(input);
            return 0m;
        }

        /// <summary>
        /// Resets this indicator to its initial state
        /// </summary>
        public override void Reset()
        {
            base.Reset();
            _rollingRvi.Reset();
        }
    }
}
