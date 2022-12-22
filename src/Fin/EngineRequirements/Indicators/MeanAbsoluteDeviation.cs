using System;
using System.Linq;

namespace QuantConnect.Indicators
{
    /// <summary>
    /// This indicator computes the n-period mean absolute deviation.
    /// </summary>
    public class MeanAbsoluteDeviation : WindowIndicator<IndicatorDataPoint>, IIndicatorWarmUpPeriodProvider
    {
        /// <summary>
        /// Gets the mean used to compute the deviation
        /// </summary>
        public IndicatorBase<IndicatorDataPoint> Mean { get; }

        /// <summary>
        /// Initializes a new instance of the MeanAbsoluteDeviation class with the specified period.
        ///
        /// Evaluates the mean absolute deviation of samples in the lookback period.
        /// </summary>
        /// <param name="period">The sample size of the standard deviation</param>
        public MeanAbsoluteDeviation(int period)
            : this($"MAD({period})", period)
        {
        }

        /// <summary>
        /// Initializes a new instance of the MeanAbsoluteDeviation class with the specified period.
        ///
        /// Evaluates the mean absolute deviation of samples in the look-back period.
        /// </summary>
        /// <param name="name">The name of this indicator</param>
        /// <param name="period">The sample size of the mean absolute deviation</param>
        public MeanAbsoluteDeviation(string name, int period)
            : base(name, period)
        {
            Mean = new SimpleMovingAverage($"{name}_Mean", period);
        }

        /// <summary>
        /// Gets a flag indicating when this indicator is ready and fully initialized
        /// </summary>
        public override bool IsReady => Samples >= Period;

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
            Mean.Update(input);
            return Samples < 2 ? 0m : window.Average(v => Math.Abs(v.Value - Mean.Current.Value));
        }

        /// <summary>
        /// Resets this indicator and its sub-indicator Mean to their initial state
        /// </summary>
        public override void Reset()
        {
            Mean.Reset();
            base.Reset();
        }
    }
}