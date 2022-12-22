using QuantConnect.Data;
using QuantConnect.Data.Market;

namespace QuantConnect.Securities.Option
{
    /// <summary>
    /// Defines QuantLib dividend yield estimator for option pricing model. User may define his own estimators, 
    /// including those forward and backward looking ones.
    /// </summary>
    public interface IQLDividendYieldEstimator
    {
        /// <summary>
        /// Returns current estimate of the stock dividend yield
        /// </summary>
        /// <param name="security">The option security object</param>
        /// <param name="slice">The current data slice. This can be used to access other information
        /// available to the algorithm</param>
        /// <param name="contract">The option contract to evaluate</param>
        /// <returns>Dividend yield</returns>
        double Estimate(Security security, Slice slice, OptionContract contract);
    }
}
