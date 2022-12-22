namespace QuantConnect.Indicators
{
    /// <summary>
    /// Represents the moving average indicator defined by Welles Wilder in his book:
    /// New Concepts in Technical Trading Systems.
    /// </summary>
    public class WilderMovingAverage : Indicator, IIndicatorWarmUpPeriodProvider
    {
        private readonly decimal _k;
        private readonly int _period;
        private readonly IndicatorBase<IndicatorDataPoint> _sma;

        /// <summary>
        /// Initializes a new instance of the WilderMovingAverage class with the specified name and period
        /// </summary>
        /// <param name="name">The name of this indicator</param>
        /// <param name="period">The period of the Wilder Moving Average</param>
        public WilderMovingAverage(string name, int period)
            : base(name)
        {
            _period = period;
            _k = 1m / period;
            _sma = new SimpleMovingAverage(name + "_SMA", period);
        }

        /// <summary>
        /// Initializes a new instance of the WilderMovingAverage class with the default name and period
        /// </summary>
        /// <param name="period">The period of the Wilder Moving Average</param>
        public WilderMovingAverage(int period)
            : this("WWMA" + period, period)
        {
        }

        /// <summary>
        /// Gets a flag indicating when this indicator is ready and fully initialized
        /// </summary>
        public override bool IsReady => Samples >= _period;

        /// <summary>
        /// Required period, in data points, for the indicator to be ready and fully initialized.
        /// </summary>
        public int WarmUpPeriod => _period;

        /// <summary>
        /// Resets this indicator to its initial state
        /// </summary>
        public override void Reset()
        {
            _sma.Reset();
            base.Reset();
        }

        /// <summary>
        /// Computes the next value of this indicator from the given state
        /// </summary>
        /// <param name="input">The input given to the indicator</param>
        /// <returns>A new value for this indicator</returns>
        protected override decimal ComputeNextValue(IndicatorDataPoint input)
        {
            if (!IsReady)
            {
                _sma.Update(input);
                return _sma.Current.Value;
            }
            return input.Value * _k + Current.Value * (1 - _k);
        }
    }
}