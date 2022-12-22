using QuantConnect.Data;
using QuantConnect.Data.Market;

namespace QuantConnect.Securities.Option
{
    /// <summary>
    /// Class implements default underlying constant volatility estimator (<see cref="IQLUnderlyingVolatilityEstimator"/>.), that projects the underlying own volatility 
    /// model into corresponding option pricing model.
    /// </summary>
    public class ConstantQLUnderlyingVolatilityEstimator : IQLUnderlyingVolatilityEstimator
    {
        /// <summary>
        /// Indicates whether volatility model has been warmed ot not
        /// </summary>
        public bool IsReady { get; private set; }

        /// <summary>
        /// Returns current estimate of the underlying volatility
        /// </summary>
        /// <param name="security">The option security object</param>
        /// <param name="slice">The current data slice. This can be used to access other information
        /// available to the algorithm</param>
        /// <param name="contract">The option contract to evaluate</param>
        /// <returns>The estimate</returns>
        public double Estimate(Security security, Slice slice, OptionContract contract)
        {
            var option = security as Option;

            if (option != null &&
                option.Underlying != null &&
                option.Underlying.VolatilityModel != null &&
                option.Underlying.VolatilityModel.Volatility > 0m)
            {
                IsReady = true;
                return (double)option.Underlying.VolatilityModel.Volatility;
            }

            return 0.0;
        }
    }
}
