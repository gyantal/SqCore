using System;
using System.Linq;
using MathNet.Numerics;
using MathNet.Numerics.LinearAlgebra;

namespace QuantConnect.Indicators
{
    /// <summary>
    /// The Least Squares Moving Average (LSMA) first calculates a least squares regression line
    /// over the preceding time periods, and then projects it forward to the current period. In
    /// essence, it calculates what the value would be if the regression line continued.
    /// Source: https://rtmath.net/assets/docs/finanalysis/html/b3fab79c-f4b2-40fb-8709-fdba43cdb363.htm
    /// </summary>
    public class LeastSquaresMovingAverage : WindowIndicator<IndicatorDataPoint>, IIndicatorWarmUpPeriodProvider
    {
        /// <summary>
        /// Array representing the time.
        /// </summary>
        private readonly double[] _t;

        /// <summary>
        /// The point where the regression line crosses the y-axis (price-axis)
        /// </summary>
        public IndicatorBase<IndicatorDataPoint> Intercept { get; }
        
        /// <summary>
        /// The regression line slope
        /// </summary>
        public IndicatorBase<IndicatorDataPoint> Slope { get; }

        /// <summary>
        /// Required period, in data points, for the indicator to be ready and fully initialized.
        /// </summary>
        public int WarmUpPeriod => Period;

        /// <summary>
        /// Initializes a new instance of the <see cref="LeastSquaresMovingAverage"/> class.
        /// </summary>
        /// <param name="name">The name of this indicator</param>
        /// <param name="period">The number of data points to hold in the window</param>
        public LeastSquaresMovingAverage(string name, int period)
            : base(name, period)
        {
            _t = Vector<double>.Build.Dense(period, i => i + 1).ToArray();
            Intercept = new Identity(name + "_Intercept");
            Slope = new Identity(name + "_Slope");
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="LeastSquaresMovingAverage"/> class.
        /// </summary>
        /// <param name="period">The number of data points to hold in the window.</param>
        public LeastSquaresMovingAverage(int period)
            : this($"LSMA({period})", period)
        {
        }

        /// <summary>
        /// Computes the next value of this indicator from the given state
        /// </summary>
        /// <param name="window"></param>
        /// <param name="input">The input given to the indicator</param>
        /// <returns>
        /// A new value for this indicator
        /// </returns>
        protected override decimal ComputeNextValue(IReadOnlyWindow<IndicatorDataPoint> window, IndicatorDataPoint input)
        {
            // Until the window is ready, the indicator returns the input value.
            if (window.Samples <= window.Size) return input.Value;

            // Sort the window by time, convert the observations to double and transform it to an array
            var series = window
                .OrderBy(i => i.Time)
                .Select(i => Convert.ToDouble(i.Value))
                .ToArray();
            // Fit OLS
            var ols = Fit.Line(x: _t, y: series);
            Intercept.Update(input.Time, (decimal)ols.Item1);
            Slope.Update(input.Time, (decimal)ols.Item2);

            // Calculate the fitted value corresponding to the input
            return Intercept.Current.Value + Slope.Current.Value * Period;
        }

        /// <summary>
        /// Resets this indicator and all sub-indicators (Intercept, Slope)
        /// </summary>
        public override void Reset()
        {
            Intercept.Reset();
            Slope.Reset();
            base.Reset();
        }
    }
}