using QuantConnect.Securities;

namespace QuantConnect.Orders.Slippage
{
    /// <summary>
    /// Represents a model that simulates market order slippage
    /// </summary>
    public interface ISlippageModel
    {
        /// <summary>
        /// Slippage Model. Return a decimal cash slippage approximation on the order.
        /// </summary>
        decimal GetSlippageApproximation(Security asset, Order order);
    }
}