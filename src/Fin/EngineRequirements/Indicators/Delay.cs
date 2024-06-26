﻿namespace QuantConnect.Indicators
{
    /// <summary>
    /// An indicator that delays its input for a certain period
    /// </summary>
    public class Delay : WindowIndicator<IndicatorDataPoint>, IIndicatorWarmUpPeriodProvider
    {
        /// <summary>
        /// Creates a new Delay indicator that delays its input by the specified period
        /// </summary>
        /// <param name="period">The period to delay input, must be greater than zero</param>
        public Delay(int period)
            : this($"DELAY({period})", period)
        {
        }

        /// <summary>
        /// Creates a new Delay indicator that delays its input by the specified period
        /// </summary>
        /// <param name="name">Name of the delay window indicator</param>
        /// <param name="period">The period to delay input, must be greater than zero</param>
        public Delay(string name, int period) 
            : base(name, period)
        {
        }

        /// <summary>
        /// Gets a flag indicating when this indicator is ready and fully initialized
        /// </summary>
        public override bool IsReady => Samples > Period;

        /// <summary>
        /// Required period, in data points, for the indicator to be ready and fully initialized.
        /// </summary>
        public int WarmUpPeriod => 1 + Period;

        /// <summary>
        /// Computes the next value for this indicator from the given state.
        /// </summary>
        /// <param name="window">The window of data held in this indicator</param>
        /// <param name="input">The input value to this indicator on this time step</param>
        /// <returns>A new value for this indicator</returns>
        protected override decimal ComputeNextValue(IReadOnlyWindow<IndicatorDataPoint> window, IndicatorDataPoint input)
        {
            if (!IsReady)
            {
                // grab the initial value until we're ready
                return window[window.Count - 1].Value;
            }

            return window.MostRecentlyRemoved.Value;
        }
    }
}
