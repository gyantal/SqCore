using QuantConnect.Data;
using QuantConnect.Data.Market;

namespace QuantConnect.Securities.Option
{
    /// <summary>
    /// Class implements Fed's US primary credit rate as risk free rate, implementing <see cref="IQLRiskFreeRateEstimator"/>.
    /// </summary>
    /// <remarks>
    /// Board of Governors of the Federal Reserve System (US), Primary Credit Rate - Historical Dates of Changes and Rates for Federal Reserve District 8: St. Louis [PCREDIT8]
    /// retrieved from FRED, Federal Reserve Bank of St. Louis; https://fred.stlouisfed.org/series/PCREDIT8
    /// </remarks>
    public class FedRateQLRiskFreeRateEstimator : IQLRiskFreeRateEstimator
    {
        private readonly InterestRateProvider _interestRateProvider = new ();

        /// <summary>
        /// Returns current flat estimate of the risk free rate
        /// </summary>
        /// <param name="security">The option security object</param>
        /// <param name="slice">The current data slice. This can be used to access other information
        /// available to the algorithm</param>
        /// <param name="contract">The option contract to evaluate</param>
        /// <returns>The estimate</returns>
        public decimal Estimate(Security security, Slice slice, OptionContract contract)
        {
            return slice == null
                ? InterestRateProvider.DefaultRiskFreeRate
                : _interestRateProvider.GetInterestRate(slice.Time.Date);
        }
    }
}
