﻿namespace QuantConnect.Indicators
{
    /// <summary>
    /// This indicator computes the Rate Of Change Ratio (ROCR). 
    /// The Rate Of Change Ratio is calculated with the following formula:
    /// ROCR = price / prevPrice
    /// </summary>
    public class RateOfChangeRatio : RateOfChange
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="RateOfChangeRatio"/> class using the specified name and period.
        /// </summary> 
        /// <param name="name">The name of this indicator</param>
        /// <param name="period">The period of the ROCR</param>
        public RateOfChangeRatio(string name, int period)
            : base(name, period)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="RateOfChangeRatio"/> class using the specified period.
        /// </summary> 
        /// <param name="period">The period of the ROCR</param>
        public RateOfChangeRatio(int period)
            : this($"ROCR({period})", period)
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
            var roc = base.ComputeNextValue(window, input);
            return roc != 0 ? roc + 1 : 0m;
        }
    }
}