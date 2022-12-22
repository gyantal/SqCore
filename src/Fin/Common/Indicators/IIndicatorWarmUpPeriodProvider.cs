namespace QuantConnect.Indicators
{
    /// <summary>
    /// Represents an indicator with a warm up period provider.
    /// </summary>
    public interface IIndicatorWarmUpPeriodProvider
    {
        /// <summary>
        /// Required period, in data points, for the indicator to be ready and fully initialized.
        /// </summary>
        int WarmUpPeriod { get; }
    }
}