using QuantConnect.Data.Market;

namespace QuantConnect.Indicators
{
    /// <summary>
    /// The Arms Index, also called the Short-Term Trading Index (TRIN) 
    /// is a technical analysis indicator that compares the number of advancing 
    /// and declining stocks (AD Ratio) to advancing and declining volume (AD volume).
    /// </summary>
    public class ArmsIndex : TradeBarIndicator, IIndicatorWarmUpPeriodProvider
    {
        private readonly IndicatorBase<IndicatorDataPoint> _arms;

        /// <summary>
        /// Gets the Advance/Decline Ratio (ADR) indicator
        /// </summary>
        public AdvanceDeclineRatio ADRatio { get; }

        /// <summary>
        /// Gets the Advance/Decline Volume Ratio (ADVR) indicator
        /// </summary>
        public AdvanceDeclineVolumeRatio ADVRatio { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="ArmsIndex"/> class
        /// </summary>
        public ArmsIndex(string name) : base(name)
        {
            ADRatio = new AdvanceDeclineRatio(name + "_A/D Ratio");
            ADVRatio = new AdvanceDeclineVolumeRatio(name + "_A/D Volume Ratio");

            _arms = ADRatio.Over(ADVRatio, name);
        }

        /// <summary>
        /// Gets a flag indicating when this indicator is ready and fully initialized
        /// </summary>
        public override bool IsReady => ADRatio.IsReady && ADVRatio.IsReady;

        /// <summary>
        /// Required period, in data points, for the indicator to be ready and fully initialized.
        /// </summary>
        public int WarmUpPeriod => System.Math.Max(ADRatio.WarmUpPeriod, ADVRatio.WarmUpPeriod);

        /// <summary>
        /// Computes the next value of this indicator from the given state
        /// </summary>
        /// <param name="input">The input given to the indicator</param>
        /// <returns>A new value for this indicator</returns>
        protected override decimal ComputeNextValue(TradeBar input)
        {
            ADRatio.Update(input);
            ADVRatio.Update(input);

            return _arms.Current.Value;
        }

        /// <summary>
        /// Resets this indicator to its initial state
        /// </summary>
        public override void Reset()
        {
            ADRatio.Reset();
            ADVRatio.Reset();
            _arms.Reset();

            base.Reset();
        }

        /// <summary>
        /// Add Tracking stock issue
        /// </summary>
        /// <param name="symbol">the tracking stock issue</param>
        public void AddStock(Symbol symbol)
        {
            ADRatio.AddStock(symbol);
            ADVRatio.AddStock(symbol);
        }

        /// <summary>
        /// Remove Tracking stock issue
        /// </summary>
        /// <param name="symbol">the tracking stock issue</param>
        public void RemoveStock(Symbol symbol)
        {
            ADRatio.RemoveStock(symbol);
            ADVRatio.RemoveStock(symbol);
        }
    }
}
