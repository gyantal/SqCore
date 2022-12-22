using QuantConnect.Data;
using QuantConnect.Data.Market;

namespace QuantConnect.Securities.Option
{
    /// <summary>
    /// Class implements default flat dividend yield curve estimator, implementing <see cref="IQLDividendYieldEstimator"/>.  
    /// </summary>
    public class ConstantQLDividendYieldEstimator : IQLDividendYieldEstimator
    {
        private readonly double _dividendYield;
        /// <summary>
        /// Constructor initializes class with constant dividend yield. 
        /// </summary>
        /// <param name="dividendYield"></param>
        public ConstantQLDividendYieldEstimator(double dividendYield = 0.00)
        {
            _dividendYield = dividendYield;
        }

        /// <summary>
        /// Returns current flat estimate of the dividend yield
        /// </summary>
        /// <param name="security">The option security object</param>
        /// <param name="slice">The current data slice. This can be used to access other information
        /// available to the algorithm</param>
        /// <param name="contract">The option contract to evaluate</param>
        /// <returns>The estimate</returns>
        public double Estimate(Security security, Slice slice, OptionContract contract)
        {
            return _dividendYield;
        }
    }
}
