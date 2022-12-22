using System;

namespace QuantConnect.Indicators
{
    /// <summary>
    /// This indicator computes the n-period population variance.
    /// </summary>
    public class Variance : WindowIndicator<IndicatorDataPoint>, IIndicatorWarmUpPeriodProvider
    {
        private decimal _rollingSum;
        private decimal _rollingSumOfSquares;

        /// <summary>
        /// Initializes a new instance of the <see cref="Variance"/> class using the specified period.
        /// </summary> 
        /// <param name="period">The period of the indicator</param>
        public Variance(int period)
            : this($"VAR({period})", period)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Variance"/> class using the specified name and period.
        /// </summary> 
        /// <param name="name">The name of this indicator</param>
        /// <param name="period">The period of the indicator</param>
        public Variance(string name, int period)
            : base(name, period)
        {
        }

        /// <summary>
        /// Required period, in data points, for the indicator to be ready and fully initialized.
        /// </summary>
        public int WarmUpPeriod => Period;

        /// <summary>
        /// Computes the next value of this indicator from the given state
        /// </summary>
        /// <param name="input">The input given to the indicator</param>
        /// <param name="window">The window for the input history</param>
        /// <returns>A new value for this indicator</returns>
        protected override decimal ComputeNextValue(IReadOnlyWindow<IndicatorDataPoint> window, IndicatorDataPoint input)
        {
            _rollingSum += input.Value;
            _rollingSumOfSquares += input.Value * input.Value;

            if (Samples < 2)
                return 0m;

            var n = Math.Min(Period, Samples);
            var meanValue1 = _rollingSum / n;
            var meanValue2 = _rollingSumOfSquares / n;

            if (n == Period)
            {
                var removedValue = window[Period - 1];
                _rollingSum -= removedValue.Value;
                _rollingSumOfSquares -= removedValue.Value * removedValue.Value;
            }

            // Ensure non-negative variance
            return Math.Max(0m, meanValue2 - meanValue1 * meanValue1);
        }

        /// <summary>
        /// Resets this indicator to its initial state
        /// </summary>
        public override void Reset()
        {
            _rollingSum = 0;
            _rollingSumOfSquares = 0;
            base.Reset();
        }
    }
}