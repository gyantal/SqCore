using QuantConnect.Data.Market;

namespace QuantConnect.Indicators
{
    /// <summary>
    /// This indicator computes the Average Directional Movement Index Rating (ADXR). 
    /// The Average Directional Movement Index Rating is calculated with the following formula:
    /// ADXR[i] = (ADX[i] + ADX[i - period + 1]) / 2
    /// </summary>
    public class AverageDirectionalMovementIndexRating : BarIndicator, IIndicatorWarmUpPeriodProvider
    {
        private readonly int _period;
        private readonly RollingWindow<decimal> _adxHistory;

        /// <summary>
        /// Initializes a new instance of the <see cref="AverageDirectionalMovementIndexRating"/> class using the specified name and period.
        /// </summary> 
        /// <param name="name">The name of this indicator</param>
        /// <param name="period">The period of the ADXR</param>
        public AverageDirectionalMovementIndexRating(string name, int period) 
            : base(name)
        {
            _period = period;
            ADX = new AverageDirectionalIndex(name + "_ADX", period);
            _adxHistory = new RollingWindow<decimal>(period);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AverageDirectionalMovementIndexRating"/> class using the specified period.
        /// </summary> 
        /// <param name="period">The period of the ADXR</param>
        public AverageDirectionalMovementIndexRating(int period)
            : this($"ADXR({period})", period)
        {
        }

        /// <summary>
        /// The Average Directional Index indicator instance being used
        /// </summary>
        public AverageDirectionalIndex ADX { get; }

        /// <summary>
        /// Gets a flag indicating when this indicator is ready and fully initialized
        /// </summary>
        public override bool IsReady => _adxHistory.IsReady;

        /// <summary>
        /// Required period, in data points, for the indicator to be ready and fully initialized.
        /// </summary>
        public int WarmUpPeriod => _period * 3 - 1;

        /// <summary>
        /// Computes the next value of this indicator from the given state
        /// </summary>
        /// <param name="input">The input given to the indicator</param>
        /// <returns>A new value for this indicator</returns>
        protected override decimal ComputeNextValue(IBaseDataBar input)
        {
            ADX.Update(input);

            if (ADX.IsReady)
            {
                _adxHistory.Add(ADX.Current.Value);
            }

            return IsReady ? (ADX.Current.Value + _adxHistory[_period - 1]) / 2 : 50m;
        }

        /// <summary>
        /// Resets this indicator to its initial state
        /// </summary>
        public override void Reset()
        {
            ADX.Reset();
            _adxHistory.Reset();
            base.Reset();
        }
    }
}