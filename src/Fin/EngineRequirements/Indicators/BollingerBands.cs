namespace QuantConnect.Indicators
{
    /// <summary>
    /// This indicator creates a moving average (middle band) with an upper band and lower band
    /// fixed at k standard deviations above and below the moving average.
    /// </summary>
    public class BollingerBands : Indicator, IIndicatorWarmUpPeriodProvider
    {
        /// <summary>
        /// Gets the type of moving average
        /// </summary>
        public MovingAverageType MovingAverageType { get; }

        /// <summary>
        /// Gets the standard deviation
        /// </summary>
        public IndicatorBase<IndicatorDataPoint> StandardDeviation { get; }

        /// <summary>
        /// Gets the middle Bollinger band (moving average)
        /// </summary>
        public IndicatorBase<IndicatorDataPoint> MiddleBand { get; }

        /// <summary>
        /// Gets the upper Bollinger band (middleBand + k * stdDev)
        /// </summary>
        public IndicatorBase<IndicatorDataPoint> UpperBand { get; }

        /// <summary>
        /// Gets the lower Bollinger band (middleBand - k * stdDev)
        /// </summary>
        public IndicatorBase<IndicatorDataPoint> LowerBand { get; }

        /// <summary>
        /// Gets the Bollinger BandWidth indicator
        /// BandWidth = ((Upper Band - Lower Band) / Middle Band) * 100
        /// </summary>
        public IndicatorBase<IndicatorDataPoint> BandWidth { get; }

        /// <summary>
        /// Gets the Bollinger %B
        /// %B = (Price - Lower Band)/(Upper Band - Lower Band)
        /// </summary>
        public IndicatorBase<IndicatorDataPoint> PercentB { get; }

        /// <summary>
        /// Gets the Price level
        /// </summary>
        public IndicatorBase<IndicatorDataPoint> Price { get; }

        /// <summary>
        /// Initializes a new instance of the BollingerBands class
        /// </summary>
        /// <param name="period">The period of the standard deviation and moving average (middle band)</param>
        /// <param name="k">The number of standard deviations specifying the distance between the middle band and upper or lower bands</param>
        /// <param name="movingAverageType">The type of moving average to be used</param>
        public BollingerBands(int period, decimal k, MovingAverageType movingAverageType = MovingAverageType.Simple)
            : this($"BB({period},{k})", period, k, movingAverageType)
        {
        }

        /// <summary>
        /// Initializes a new instance of the BollingerBands class
        /// </summary>
        /// <param name="name">The name of this indicator</param>
        /// <param name="period">The period of the standard deviation and moving average (middle band)</param>
        /// <param name="k">The number of standard deviations specifying the distance between the middle band and upper or lower bands</param>
        /// <param name="movingAverageType">The type of moving average to be used</param>
        public BollingerBands(string name, int period, decimal k, MovingAverageType movingAverageType = MovingAverageType.Simple)
            : base(name)
        {
            WarmUpPeriod = period;
            MovingAverageType = movingAverageType;
            StandardDeviation = new StandardDeviation(name + "_StandardDeviation", period);
            MiddleBand = movingAverageType.AsIndicator(name + "_MiddleBand", period);
            LowerBand = MiddleBand.Minus(StandardDeviation.Times(k), name + "_LowerBand");
            UpperBand = MiddleBand.Plus(StandardDeviation.Times(k), name + "_UpperBand");

            var UpperMinusLower = UpperBand.Minus(LowerBand);
            BandWidth = UpperMinusLower
                .Over(MiddleBand)
                .Times(new ConstantIndicator<IndicatorDataPoint>("ct", 100m), name + "_BandWidth");

            Price = new Identity(name + "_Close");
            PercentB = IndicatorExtensions.Over(
                Price.Minus(LowerBand),
                UpperMinusLower,
                name + "_%B");
        }

        /// <summary>
        /// Gets a flag indicating when this indicator is ready and fully initialized
        /// </summary>
        public override bool IsReady => MiddleBand.IsReady && UpperBand.IsReady && LowerBand.IsReady && BandWidth.IsReady && PercentB.IsReady;

        /// <summary>
        /// Required period, in data points, for the indicator to be ready and fully initialized.
        /// </summary>
        public int WarmUpPeriod { get; }

        /// <summary>
        /// Computes the next value of the following sub-indicators from the given state:
        /// StandardDeviation, MiddleBand, UpperBand, LowerBand, BandWidth, %B
        /// </summary>
        /// <param name="input">The input given to the indicator</param>
        /// <returns>The input is returned unmodified.</returns>
        protected override decimal ComputeNextValue(IndicatorDataPoint input)
        {
            StandardDeviation.Update(input);
            MiddleBand.Update(input);
            Price.Update(input);

            return input.Value;
        }

        /// <summary>
        /// Validate and Compute the next value for this indicator
        /// </summary>
        /// <param name="input">Input for this indicator</param>
        /// <returns><see cref="IndicatorResult"/> of this update</returns>
        /// <remarks>Override implemented to handle GH issue #4927</remarks>
        protected override IndicatorResult ValidateAndComputeNextValue(IndicatorDataPoint input)
        {
            // Update our Indicators
            var value = ComputeNextValue(input);

            // If the STD = 0, we know that the our PercentB indicator will fail to update. This is
            // because the denominator will be 0. When this is the case after fully ready we do not
            // want the BollingerBands to emit an update because its PercentB property will be stale.
            return IsReady && StandardDeviation.Current.Value == 0
                ? new IndicatorResult(value, IndicatorStatus.MathError)
                : new IndicatorResult(value);
        }

        /// <summary>
        /// Resets this indicator and all sub-indicators (StandardDeviation, LowerBand, MiddleBand, UpperBand, BandWidth, %B)
        /// </summary>
        public override void Reset()
        {
            StandardDeviation.Reset();
            MiddleBand.Reset();
            UpperBand.Reset();
            LowerBand.Reset();
            BandWidth.Reset();
            PercentB.Reset();
            base.Reset();
        }
    }
}
