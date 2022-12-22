using System.Collections.Generic;

namespace QuantConnect.Algorithm.Framework.Alphas.Analysis
{
    /// <summary>
    /// Provides a simple abstraction that returns a security's current price and volatility.
    /// This facilitates testing by removing the dependency of IAlgorithm on the analysis components
    /// </summary>
    public interface ISecurityValuesProvider
    {
        /// <summary>
        /// Gets the current values for the specified symbol (price/volatility)
        /// </summary>
        /// <param name="symbol">The symbol to get price/volatility for</param>
        /// <returns>The insight target values for the specified symbol</returns>
        SecurityValues GetValues(Symbol symbol);

        /// <summary>
        /// Gets the current values for all the algorithm securities (price/volatility)
        /// </summary>
        /// <returns>The insight target values for all the algorithm securities</returns>
        ReadOnlySecurityValuesCollection GetAllValues();
    }

    /// <summary>
    /// Provides extension methods for <see cref="ISecurityValuesProvider"/>
    /// </summary>
    public static class SecurityValuesProviderExtensions
    {
        /// <summary>
        /// Creates a new instance of <see cref="ReadOnlySecurityValuesCollection"/> to hold all <see cref="SecurityValues"/> for
        /// the specified symbol at the current instant in time
        /// </summary>
        /// <param name="securityValuesProvider">Security values provider fetches security values for each symbol</param>
        /// <param name="symbols">The symbols to get values for</param>
        /// <returns>A collection of</returns>
        public static ReadOnlySecurityValuesCollection GetValues(this ISecurityValuesProvider securityValuesProvider, ICollection<Symbol> symbols)
        {
            var values = new Dictionary<Symbol, SecurityValues>(symbols.Count);
            foreach (var symbol in symbols)
            {
                values[symbol] = securityValuesProvider.GetValues(symbol);
            }

            return new ReadOnlySecurityValuesCollection(values);
        }
    }
}