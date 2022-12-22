using QuantConnect.Securities;

namespace QuantConnect.Orders.Fees
{
    /// <summary>
    /// Represents a model the simulates order fees
    /// </summary>
    /// <remarks>Please use <see cref="FeeModel"/> as the base class for
    /// any implementations of <see cref="IFeeModel"/></remarks>
    public interface IFeeModel
    {
        /// <summary>
        /// Gets the order fee associated with the specified order.
        /// </summary>
        /// <param name="parameters">A <see cref="OrderFeeParameters"/> object
        /// containing the security and order</param>
        /// <returns>The cost of the order in a <see cref="CashAmount"/> instance</returns>
        OrderFee GetOrderFee(OrderFeeParameters parameters);
    }

    /// <summary>
    /// Provide extension method for <see cref="IFeeModel"/> to enable
    /// backwards compatibility of invocations.
    /// </summary>
    public static class FeeModelExtensions
    {
        /// <summary>
        /// Gets the order fee associated with the specified order. This returns the cost
        /// of the transaction in the account currency
        /// </summary>
        /// <param name="model">The fee model</param>
        /// <param name="security">The security matching the order</param>
        /// <param name="order">The order to compute fees for</param>
        /// <returns>The cost of the order in units of the account currency</returns>
        public static decimal GetOrderFee(this IFeeModel model, Security security, Order order)
        {
            var parameters = new OrderFeeParameters(security, order);
            var fee = model.GetOrderFee(parameters);

            return fee.Value.Amount;
        }
    }
}