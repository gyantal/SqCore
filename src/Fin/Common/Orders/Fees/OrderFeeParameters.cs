using QuantConnect.Securities;
namespace QuantConnect.Orders.Fees
{
    /// <summary>
    /// Defines the parameters for <see cref="IFeeModel.GetOrderFee"/>
    /// </summary>
    public class OrderFeeParameters
    {
        /// <summary>
        /// Gets the security
        /// </summary>
        public Security Security { get; }

        /// <summary>
        /// Gets the order
        /// </summary>
        public Order Order { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="OrderFeeParameters"/> class
        /// </summary>
        /// <param name="security">The security</param>
        /// <param name="order">The order</param>
        public OrderFeeParameters(Security security, Order order)
        {
            Security = security;
            Order = order;
        }
    }
}