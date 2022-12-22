namespace QuantConnect.Indicators
{
    /// <summary>
    /// Represents the relative moving average indicator (RMA).
    /// RMA = SMA(3 x Period) - SMA(2 x Period) + SMA(1 x Period) per formula:
    /// https://www.hybrid-solutions.com/plugins/client-vtl-plugins/free/rma.html
    /// </summary>
    public class RelativeMovingAverage : Indicator, IIndicatorWarmUpPeriodProvider
    {
        /// <summary>
        /// Gets the Short Term SMA with 1 x Period of RMA
        /// </summary>
        public SimpleMovingAverage ShortAverage { get; }

        /// <summary>
        /// Gets the Medium Term SMA with 2 x Period of RMA
        /// </summary>
        public SimpleMovingAverage MediumAverage { get; }

        /// <summary>
        /// Gets the Long Term SMA with 3 x Period of RMA
        /// </summary>
        public SimpleMovingAverage LongAverage { get; }

        /// <summary>
        /// Gets a flag indicating when this indicator is ready and fully initialized
        /// </summary>
        public override bool IsReady => LongAverage.IsReady;

        /// <summary>
        /// Required period, in data points, for the indicator to be ready and fully initialized.
        /// </summary>
        public int WarmUpPeriod => LongAverage.WarmUpPeriod;

        /// <summary>
        /// Initializes a new instance of the RelativeMovingAverage class with the specified name and period
        /// </summary>
        /// <param name="name">The name of this indicator</param>
        /// <param name="period">The period of the RMA</param>
        public RelativeMovingAverage(string name, int period) 
            : base(name)
        {
            ShortAverage = new SimpleMovingAverage(name + "_Short", period);
            MediumAverage = new SimpleMovingAverage(name + "_Medium", period * 2);
            LongAverage = new SimpleMovingAverage(name + "_Long", period * 3);
        }

        /// <summary>
        /// Initializes a new instance of the SimpleMovingAverage class with the default name and period
        /// </summary>
        /// <param name="period"></param>
        public RelativeMovingAverage(int period)
            : this($"RMA({period})", period)
        {
        }

        /// <summary>
        /// Copmutes the next value for this indicator from the given state.
        /// </summary>
        /// <param name="input">The input value to this indicator on this time step</param>
        /// <returns>A new value for this indicator</returns>
        protected override decimal ComputeNextValue(IndicatorDataPoint input)
        {
            ShortAverage.Update(input);
            MediumAverage.Update(input);
            LongAverage.Update(input);

            return LongAverage.Current.Value - MediumAverage.Current.Value + ShortAverage.Current.Value;
        }

        /// <summary>
        /// Resets this indicator to its initial state
        /// </summary>
        public override void Reset()
        {
            base.Reset();
            ShortAverage.Reset();
            MediumAverage.Reset();
            LongAverage.Reset();
        }
    }
}
