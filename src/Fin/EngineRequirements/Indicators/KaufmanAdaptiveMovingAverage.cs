namespace QuantConnect.Indicators
{
    /// <summary>
    /// This indicator computes the Kaufman Adaptive Moving Average (KAMA).
    /// The Kaufman Adaptive Moving Average is calculated as explained here:
    /// http://stockcharts.com/school/doku.php?id=chart_school:technical_indicators:kaufman_s_adaptive_moving_average
    /// </summary>
    public class KaufmanAdaptiveMovingAverage : KaufmanEfficiencyRatio
    {
        private readonly decimal _slowSmoothingFactor;
        private readonly decimal _diffSmoothingFactor;
        private decimal _prevKama;

        /// <summary>
        /// Initializes a new instance of the <see cref="KaufmanAdaptiveMovingAverage"/> class using the specified name and period.
        /// </summary>
        /// <param name="name">The name of this indicator</param>
        /// <param name="period">The period of the Efficiency Ratio (ER)</param>
        /// <param name="fastEmaPeriod">The period of the fast EMA used to calculate the Smoothing Constant (SC)</param>
        /// <param name="slowEmaPeriod">The period of the slow EMA used to calculate the Smoothing Constant (SC)</param>
        public KaufmanAdaptiveMovingAverage(string name, int period, int fastEmaPeriod = 2, int slowEmaPeriod = 30)
            : base(name, period)
        {
            // Smoothing factor of the slow EMA
            _slowSmoothingFactor = 2m / (slowEmaPeriod + 1m);
            // Difference between the smoothing factor of the fast and slow EMA
            _diffSmoothingFactor = 2m / (fastEmaPeriod + 1m) - _slowSmoothingFactor;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="KaufmanAdaptiveMovingAverage"/> class using the specified period.
        /// </summary>
        /// <param name="period">The period of the Efficiency Ratio (ER)</param>
        /// <param name="fastEmaPeriod">The period of the fast EMA used to calculate the Smoothing Constant (SC)</param>
        /// <param name="slowEmaPeriod">The period of the slow EMA used to calculate the Smoothing Constant (SC)</param>
        public KaufmanAdaptiveMovingAverage(int period, int fastEmaPeriod = 2, int slowEmaPeriod = 30)
            : this($"KAMA({period},{fastEmaPeriod},{slowEmaPeriod})", period, fastEmaPeriod, slowEmaPeriod)
        {
        }

        /// <summary>
        /// Computes the next value of this indicator from the given state
        /// </summary>
        /// <param name="input">The input given to the indicator</param>
        /// <param name="window">The window for the input history</param>
        /// <returns>A new value for this indicator</returns>
        protected override decimal ComputeNextValue(IReadOnlyWindow<IndicatorDataPoint> window, IndicatorDataPoint input)
        {
            // Calculate the efficiency ratio
            var efficiencyRatio = base.ComputeNextValue(window, input);

            if (Samples < Period)
            {
                return input.Value;
            }

            if (Samples == Period)
            {
                // Calculate the first KAMA
                // The yesterday price is used here as the previous KAMA.
                _prevKama = window[1].Value;
            }

            // Calculate the smoothing constant
            var smoothingConstant = efficiencyRatio * _diffSmoothingFactor + _slowSmoothingFactor;
            smoothingConstant *= smoothingConstant;

            // Calculate the KAMA like an EMA, using the
            // smoothing constant as the adaptive factor.
            _prevKama = (input.Value - _prevKama) * smoothingConstant + _prevKama;

            return _prevKama;
        }

        /// <summary>
        /// Resets this indicator to its initial state
        /// </summary>
        public override void Reset()
        {
            _prevKama = 0;
            base.Reset();
        }
    }
}
