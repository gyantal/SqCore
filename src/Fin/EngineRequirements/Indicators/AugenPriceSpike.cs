using System;

namespace QuantConnect.Indicators
{
    /// <summary>
    /// The Augen Price Spike indicator is an indicator that measures price 
    /// changes in terms of standard deviations. In the book, The
    /// Volatility Edge in Options Trading, Jeff Augen describes a
    /// method for tracking absolute price changes in terms of recent
    /// volatility, using the standard deviation.
    /// 
    /// length = x
    /// closes = closeArray
    /// closes1 = closeArray shifted right by 1
    /// closes2 = closeArray shifted right by 2
    /// closeLog = np.log(np.divide(closes1, closes2))
    /// SDev = np.std(closeLog)
    /// m = SDev * closes1[-1]
    /// spike = (closes[-1]-closes1[-1])/m
    /// return spike
    /// 
    /// Augen Price Spike from TradingView
    /// https://www.tradingview.com/script/fC7Pn2X2-Price-Spike-Jeff-Augen/  
    /// 
    /// </summary>
    public class AugenPriceSpike : Indicator, IIndicatorWarmUpPeriodProvider
    {
        private readonly StandardDeviation _standardDeviation;
        private readonly RollingWindow<decimal> _rollingData;

        /// <summary>
        /// Initializes a new instance of the AugenPriceSpike class using the specified period
        /// </summary>
        /// <param name="period">The period over which to perform to computation</param>
        public AugenPriceSpike(int period = 3)
            : this($"APS({period})", period)
        {
        }
        /// <summary>
        /// Creates a new AugenPriceSpike indicator with the specified period
        /// </summary>
        /// <param name="name">The name of this indicator</param>
        /// <param name="period">The period of this indicator</param>
        public AugenPriceSpike(string name, int period)
            : base(name)
        {
            if (period < 3)
            {
                throw new ArgumentException("AugenPriceSpike Indicator must have a period of at least 3", nameof(period));
            }
            _standardDeviation = new StandardDeviation(period);
            _rollingData = new RollingWindow<decimal>(3);
            WarmUpPeriod = period+2;
        }

        /// <summary>
        /// Gets a flag indicating when the indicator is ready and fully initialized
        /// </summary>
        public override bool IsReady => _rollingData.IsReady && _standardDeviation.IsReady;

        /// <summary>
        /// Required period, in data points, for the indicator to be ready and fully initialized.
        /// </summary>
        public int WarmUpPeriod { get; }

        /// <summary>
        /// Computes the next value for this indicator from the given state.
        /// </summary>
        /// <param name="input">The input value to this indicator on this time step</param>
        /// <returns>A a value for this indicator</returns>
        protected override decimal ComputeNextValue(IndicatorDataPoint input)
        {
            _rollingData.Add(input.Value);
            if (_rollingData.Count < 3) { return 0m; }

            var previousPoint = _rollingData[1];
            var previousPoint2 = _rollingData[2];

            var logPoint = 0.0;
            if (previousPoint2 != 0)
            {
                logPoint = Math.Log((double)(previousPoint / previousPoint2));
            }

            _standardDeviation.Update(input.Time, (decimal)logPoint);

            if (!_rollingData.IsReady) { return 0m; }
            if (!_standardDeviation.IsReady) { return 0m; }

            var m = _standardDeviation.Current.Value * previousPoint;
            if (m == 0) { return 0; }

            var spikeValue = (input.Value - previousPoint) / m;
            return spikeValue;
        }

        /// <summary>
        /// Resets this indicator to its initial state
        /// </summary>
        public override void Reset()
        {
            _standardDeviation.Reset();
            _rollingData.Reset();
            base.Reset();
        }
    }
}
