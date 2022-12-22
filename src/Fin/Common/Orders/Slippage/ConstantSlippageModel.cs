using QuantConnect.Data;
using QuantConnect.Securities;

namespace QuantConnect.Orders.Slippage
{
    /// <summary>
    /// Represents a slippage model that uses a constant percentage of slip
    /// </summary>
    public class ConstantSlippageModel : ISlippageModel
    {
        private readonly decimal _slippagePercent;
        /// <summary>
        /// Initializes a new instance of the <see cref="ConstantSlippageModel"/> class
        /// </summary>
        /// <param name="slippagePercent">The slippage percent for each order. Percent is ranged 0 to 1.</param>
        public ConstantSlippageModel(decimal slippagePercent)
        {
            _slippagePercent = slippagePercent;
        }

        /// <summary>
        /// Slippage Model. Return a decimal cash slippage approximation on the order.
        /// </summary>
        public decimal GetSlippageApproximation(Security asset, Order order)
        {
            var lastData = asset.GetLastData();
            if (lastData == null) return 0;

            return lastData.Value*_slippagePercent;
        }
    }
}
