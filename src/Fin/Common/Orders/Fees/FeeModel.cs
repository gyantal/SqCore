using QuantConnect.Securities;

namespace QuantConnect.Orders.Fees
{
    /// <summary>
    /// Base class for any order fee model
    /// </summary>
    /// <remarks>Please use <see cref="FeeModel"/> as the base class for
    /// any implementations of <see cref="IFeeModel"/></remarks>
    public class FeeModel : IFeeModel
    {
        /// <summary>
        /// Gets the order fee associated with the specified order.
        /// </summary>
        /// <param name="parameters">A <see cref="OrderFeeParameters"/> object
        /// containing the security and order</param>
        /// <returns>The cost of the order in a <see cref="CashAmount"/> instance</returns>
        public virtual OrderFee GetOrderFee(OrderFeeParameters parameters)
        {
            return new OrderFee(new CashAmount(
                0,
                "USD"));
        }
    }
}