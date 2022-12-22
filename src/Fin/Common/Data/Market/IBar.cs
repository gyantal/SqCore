namespace QuantConnect.Data.Market
{
    /// <summary>
    /// Generic bar interface with Open, High, Low and Close.
    /// </summary>
    public interface IBar
    {
        /// <summary>
        /// Opening price of the bar: Defined as the price at the start of the time period.
        /// </summary>
        decimal Open { get; }

        /// <summary>
        /// High price of the bar during the time period.
        /// </summary>
        decimal High { get; }

        /// <summary>
        /// Low price of the bar during the time period.
        /// </summary>
        decimal Low { get; }

        /// <summary>
        /// Closing price of the bar. Defined as the price at Start Time + TimeSpan.
        /// </summary>
        decimal Close { get; }
    }
}