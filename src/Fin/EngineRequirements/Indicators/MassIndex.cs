﻿using QuantConnect.Data.Market;

namespace QuantConnect.Indicators
{
    /// <summary>
    /// The Mass Index uses the high-low range to identify trend reversals based on range expansions.
    /// In this sense, the Mass Index is a volatility indicator that does not have a directional
    /// bias. Instead, the Mass Index identifies range bulges that can foreshadow a reversal of the
    /// current trend. Developed by Donald Dorsey.
    /// </summary>
    /// <seealso cref="TradeBarIndicator"/>
    public class MassIndex : TradeBarIndicator, IIndicatorWarmUpPeriodProvider
    {
        private readonly ExponentialMovingAverage _ema1;
        private readonly ExponentialMovingAverage _ema2;
        private readonly Sum _sum;

        /// <summary>
        /// Initializes a new instance of the <see cref="MassIndex"/> class.
        /// </summary>
        /// <param name="name">The name for this instance.</param>
        /// <param name="emaPeriod">The period used by both EMA.</param>
        /// <param name="sumPeriod">The sum period.</param>
        public MassIndex(string name, int emaPeriod, int sumPeriod)
            : base(name)
        {
            _ema1 = new ExponentialMovingAverage(emaPeriod);
            _ema2 = _ema1.EMA(emaPeriod);
            _sum = new Sum(sumPeriod);
            WarmUpPeriod = 2 * (emaPeriod - 1) + sumPeriod;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="MassIndex"/> class.
        /// </summary>
        /// <param name="emaPeriod">The period used by both EMA.</param>
        /// <param name="sumPeriod">The sum period.</param>
        public MassIndex(int emaPeriod = 9, int sumPeriod = 25)
            : this($"MASS({emaPeriod},{sumPeriod})", emaPeriod, sumPeriod)
        {
        }

        /// <summary>
        /// Gets a flag indicating when this indicator is ready and fully initialized
        /// </summary>
        public override bool IsReady => _sum.IsReady;

        /// <summary>
        /// Required period, in data points, for the indicator to be ready and fully initialized.
        /// </summary>
        public int WarmUpPeriod { get; }

        /// <summary>
        /// Resets this indicator to its initial state
        /// </summary>
        public override void Reset()
        {
            base.Reset();
            _ema1.Reset();
            _ema2.Reset();
            _sum.Reset();
        }

        /// <summary>
        /// Computes the next value of this indicator from the given state
        /// </summary>
        /// <param name="input">The input given to the indicator</param>
        /// <returns>
        /// A new value for this indicator
        /// </returns>
        protected override decimal ComputeNextValue(TradeBar input)
        {
            _ema1.Update(input.Time, input.High - input.Low);
            if (_ema2.IsReady)
            {
                _sum.Update(input.Time, _ema1.Current.Value / _ema2.Current.Value);
            }

            if (!_sum.IsReady)
            {
                return _sum.Period;
            }
            return _sum.Current.Value;
        }
    }
}