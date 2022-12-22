using QuantConnect.Data;
using QuantConnect.Data.Market;

namespace QuantConnect.Securities.Option
{
    /// <summary>
    /// Defines QuantLib risk free rate estimator for option pricing model. 
    /// </summary>
    public interface IQLRiskFreeRateEstimator
    {
        /// <summary>
        /// Returns current estimate of the risk free rate
        /// </summary>
        /// <param name="security">The option security object</param>
        /// <param name="slice">The current data slice. This can be used to access other information
        /// available to the algorithm</param>
        /// <param name="contract">The option contract to evaluate</param>
        /// <returns>Risk free rate</returns>
        decimal Estimate(Security security, Slice slice, OptionContract contract);
    }
}
