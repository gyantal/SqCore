namespace QuantConnect.Indicators
{
    /// <summary>
    /// Calculation of the Sortino Ratio, a modification of the <see cref="SharpeRatio"/>.
    ///
    /// Reference: https://www.cmegroup.com/education/files/rr-sortino-a-sharper-ratio.pdf
    /// Formula: S(x) = (R - T) / TDD
    /// Where:
    /// S(x) - Sortino ratio of x
    /// R - the average period return
    /// T - the target or required rate of return for the investment strategy under consideration. In
    /// Sortino’s early work, T was originally known as the minimum acceptable return, or MAR. In his
    /// more recent work, MAR is now referred to as the Desired Target Return.
    /// TDD - the target downside deviation. <see cref="TargetDownsideDeviation"/>
    /// </summary>
    public class SortinoRatio : SharpeRatio
    {
        /// <summary>
        /// Creates a new Sortino Ratio indicator using the specified periods
        /// </summary>
        /// <param name="name">The name of this indicator</param>
        /// <param name="period">Period of historical observation for Sortino ratio calculation</param>
        /// <param name="minimumAcceptableReturn">Minimum acceptable return for Sortino ratio calculation</param>
        public SortinoRatio(string name, int period, double minimumAcceptableReturn = 0)
             : base(name, period, minimumAcceptableReturn.SafeDecimalCast())
        {
            var denominator = new TargetDownsideDeviation(period, minimumAcceptableReturn).Of(RateOfChange);
            Ratio = Numerator.Over(denominator);
        }

        /// <summary>
        /// Creates a new SortinoRatio indicator using the specified periods
        /// </summary>
        /// <param name="period">Period of historical observation for Sortino ratio calculation</param>
        /// <param name="minimumAcceptableReturn">Minimum acceptable return for Sortino ratio calculation</param>
        public SortinoRatio(int period, double minimumAcceptableReturn = 0)
            : this($"SORTINO({period},{minimumAcceptableReturn})", period, minimumAcceptableReturn)
        {
        }
    }
}
