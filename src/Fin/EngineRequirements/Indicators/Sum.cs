﻿namespace QuantConnect.Indicators
{
    /// <summary>
    /// Represents an indicator capable of tracking the sum for the given period
    /// </summary>
    public class Sum : WindowIndicator<IndicatorDataPoint>, IIndicatorWarmUpPeriodProvider
    {
        /// <summary>
        /// The sum for the given period
        /// </summary>
        private decimal _sum;

        /// <summary>
        /// Required period, in data points, for the indicator to be ready and fully initialized.
        /// </summary>
        public int WarmUpPeriod => Period;

        /// <summary>
        /// Resets this indicator to its initial state
        /// </summary>
        public override void Reset()
        {
            _sum = 0.0m;
            base.Reset();
        }

        /// <summary>
        /// Initializes a new instance of the Sum class with the specified name and period
        /// </summary>
        /// <param name="name">The name of this indicator</param>
        /// <param name="period">The period of the SMA</param>
        public Sum(string name, int period)
            : base(name, period)
        {
        }

        /// <summary>
        /// Initializes a new instance of the Sum class with the default name and period
        /// </summary>
        /// <param name="period">The period of the SMA</param>
        public Sum(int period)
            : this($"SUM({period})", period)
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
            _sum += input.Value;
            if (window.Samples > window.Size)
            {
                _sum -= window.MostRecentlyRemoved.Value;
            }
            return _sum;
        }
    }
}