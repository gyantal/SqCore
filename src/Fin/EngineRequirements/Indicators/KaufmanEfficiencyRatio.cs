using System;

namespace QuantConnect.Indicators
{
    /// <summary>
    /// This indicator computes the Kaufman Efficiency Ratio (KER).
    /// The Kaufman Efficiency Ratio is calculated as explained here:
    /// https://www.marketvolume.com/technicalanalysis/efficiencyratio.asp
    /// </summary>
    public class KaufmanEfficiencyRatio : WindowIndicator<IndicatorDataPoint>
    {
        private decimal _sumRoc1;
        private decimal _periodRoc;
        private decimal _trailingValue;

        /// <summary>
        /// Gets a flag indicating when this indicator is ready and fully initialized
        /// </summary>
        public override bool IsReady => Samples >= Period;

        /// <summary>
        /// Initializes a new instance of the <see cref="KaufmanEfficiencyRatio"/> class using the specified name and period.
        /// </summary>
        /// <param name="name">The name of this indicator</param>
        /// <param name="period">The period of the Efficiency Ratio (ER)</param>
        public KaufmanEfficiencyRatio(string name, int period)
            : base(name, period + 1)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="KaufmanEfficiencyRatio"/> class using the specified period.
        /// </summary>
        /// <param name="period">The period of the Efficiency Ratio (ER)</param>
        public KaufmanEfficiencyRatio(int period)
            : this($"KER({period})", period)
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
            if (Samples < Period)
            {
                if (Samples > 1)
                {
                    _sumRoc1 += Math.Abs(input.Value - window[1].Value);
                }

                return input.Value;
            }

            if (Samples == Period)
            {
                _sumRoc1 += Math.Abs(input.Value - window[1].Value);
            }

            var newTrailingValue = window[Period - 1];
            _periodRoc = input.Value - newTrailingValue.Value;

            if (Samples > Period)
            {
                // Adjust sumROC1:
                // - Remove trailing ROC1 
                // - Add new ROC1
                _sumRoc1 -= Math.Abs(_trailingValue - newTrailingValue.Value);
                _sumRoc1 += Math.Abs(input.Value - window[1].Value);
            }

            _trailingValue = newTrailingValue.Value;

            // Calculate the efficiency ratio
            return _sumRoc1 <= _periodRoc || _sumRoc1 == 0 ? 1m : Math.Abs(_periodRoc / _sumRoc1);
        }

        /// <summary>
        /// Resets this indicator to its initial state
        /// </summary>
        public override void Reset()
        {
            _sumRoc1 = 0;
            _periodRoc = 0;
            _trailingValue = 0;
            base.Reset();
        }
    }
}
