using QuantConnect.Data;
using QuantConnect.Data.Market;

namespace QuantConnect.Securities.Option
{
    /// <summary>
    /// Class implements default flat risk free curve, implementing <see cref="IQLRiskFreeRateEstimator"/>.
    /// </summary>
    public class ConstantQLRiskFreeRateEstimator : IQLRiskFreeRateEstimator
    {
        private readonly decimal _riskFreeRate;
        /// <summary>
        /// Constructor initializes class with risk free rate constant
        /// </summary>
        /// <param name="riskFreeRate"></param>
        public ConstantQLRiskFreeRateEstimator(decimal riskFreeRate = 0.01m)
        {
            _riskFreeRate = riskFreeRate;
        }

        /// <summary>
        /// Returns current flat estimate of the risk free rate
        /// </summary>
        /// <param name="security">The option security object</param>
        /// <param name="slice">The current data slice. This can be used to access other information
        /// available to the algorithm</param>
        /// <param name="contract">The option contract to evaluate</param>
        /// <returns>The estimate</returns>
        public decimal Estimate(Security security, Slice slice, OptionContract contract) => _riskFreeRate;
    }
}
