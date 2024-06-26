﻿namespace QuantConnect.Indicators
{
    /// <summary>
    /// Represents an indicator that is a ready after ingesting enough samples (# samples > period) 
    /// and always returns the same value as it is given.
    /// </summary>
    public class WindowIdentity : WindowIndicator<IndicatorDataPoint>
    {
        /// <summary>
        /// Initializes a new instance of the WindowIdentity class with the specified name and period
        /// </summary>
        /// <param name="name">The name of this indicator</param>
        /// <param name="period">The period of the WindowIdentity</param>
        public WindowIdentity(string name, int period)
            : base(name, period)
        {
        }

        /// <summary>
        /// Initializes a new instance of the WindowIdentity class with the default name and period
        /// </summary>
        /// <param name="period">The period of the WindowIdentity</param>
        public WindowIdentity(int period)
            : this("WIN-ID" + period, period)
        {
        }

        /// <summary>
        /// Gets a flag indicating when this indicator is ready and fully initialized
        /// </summary>
        public override bool IsReady
        {
            get { return Samples >= Period; }
        }

        /// <summary>
        /// Computes the next value for this indicator from the given state.
        /// </summary>
        /// <param name="window">The window of data held in this indicator</param>
        /// <param name="input">The input value to this indicator on this time step</param>
        /// <returns>A new value for this indicator</returns>
        protected override decimal ComputeNextValue(IReadOnlyWindow<IndicatorDataPoint> window, IndicatorDataPoint input)
        {
            return input.Value;
        }
    }
}