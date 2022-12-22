using QuantConnect.Data;
using QuantConnect.Data.Market;

namespace QuantConnect.Securities.Option
{
    /// <summary>
    /// Defines QuantLib underlying volatility estimator for option pricing model. User may define his own estimators, 
    /// including those forward and backward looking ones.
    /// </summary>
    public interface IQLUnderlyingVolatilityEstimator
    {
        /// <summary>
        /// Returns current estimate of the underlying volatility
        /// </summary>
        /// <param name="security">The option security object</param>
        /// <param name="slice">The current data slice. This can be used to access other information
        /// available to the algorithm</param>
        /// <param name="contract">The option contract to evaluate</param>
        /// <returns>Volatility</returns>
        double Estimate(Security security, Slice slice, OptionContract contract);

        /// <summary>
        /// Indicates whether volatility model is warmed up or no
        /// </summary>
        bool IsReady { get; }
    }
}
