using QuantConnect.Data;
using QuantConnect.Data.Market;
using QuantConnect.Securities.Option;

namespace QuantConnect.Interfaces
{
    /// <summary>
    /// Reduced interface for accessing <see cref="Option"/>
    /// specific price properties and methods
    /// </summary>
    public interface IOptionPrice : ISecurityPrice
    {
        /// <summary>
        /// Gets a reduced interface of the underlying security object.
        /// </summary>
        ISecurityPrice Underlying { get; }

        /// <summary>
        /// Evaluates the specified option contract to compute a theoretical price, IV and greeks
        /// </summary>
        /// <param name="slice">The current data slice. This can be used to access other information
        /// available to the algorithm</param>
        /// <param name="contract">The option contract to evaluate</param>
        /// <returns>An instance of <see cref="OptionPriceModelResult"/> containing the theoretical
        /// price of the specified option contract</returns>
        OptionPriceModelResult EvaluatePriceModel(Slice slice, OptionContract contract);
    }
}
