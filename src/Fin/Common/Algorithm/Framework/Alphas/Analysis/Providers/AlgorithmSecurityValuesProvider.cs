using QuantConnect.Data.Market;
using QuantConnect.Interfaces;
using QuantConnect.Securities;

namespace QuantConnect.Algorithm.Framework.Alphas.Analysis.Providers
{
    /// <summary>
    /// Provides an implementation of <see cref="ISecurityProvider"/> that uses the <see cref="SecurityManager"/>
    /// to get the price for the specified symbols
    /// </summary>
    public class AlgorithmSecurityValuesProvider : ISecurityValuesProvider
    {
        private readonly IAlgorithm _algorithm;

        /// <summary>
        /// Initializes a new instance of the <see cref="AlgorithmSecurityValuesProvider"/> class
        /// </summary>
        /// <param name="algorithm">The wrapped algorithm instance</param>
        public AlgorithmSecurityValuesProvider(IAlgorithm algorithm)
        {
            _algorithm = algorithm;
        }

        /// <summary>
        /// Gets the current values for the specified symbol (price/volatility)
        /// </summary>
        /// <param name="symbol">The symbol to get price/volatility for</param>
        /// <returns>The insight target values for the specified symbol</returns>
        public SecurityValues GetValues(Symbol symbol)
        {
            var security = _algorithm.Securities[symbol];
            var volume = security.Cache.GetData<TradeBar>()?.Volume ?? 0;
            return new SecurityValues(symbol, _algorithm.UtcTime, security.Exchange.Hours, security.Price, security.VolatilityModel.Volatility, volume, security.QuoteCurrency.ConversionRate);
        }

        /// <summary>
        /// Gets the current values for all the algorithm securities (price/volatility)
        /// </summary>
        /// <returns>The insight target values for all the algorithm securities</returns>
        public ReadOnlySecurityValuesCollection GetAllValues()
        {
            // lets be lazy creating the SecurityValues
            return new ReadOnlySecurityValuesCollection(
                symbol =>
                {
                    var security = _algorithm.Securities[symbol];
                    var volume = security.Cache.GetData<TradeBar>()?.Volume ?? 0;
                    return new SecurityValues(security.Symbol, _algorithm.UtcTime, security.Exchange.Hours, security.Price, security.VolatilityModel.Volatility, volume, security.QuoteCurrency.ConversionRate);
                });
        }
    }
}