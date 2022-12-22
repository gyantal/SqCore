using QuantConnect.Orders;

namespace QuantConnect.Securities
{
    /// <summary>
    /// Represents a type capable of processing orders
    /// </summary>
    public interface IOrderProcessor : IOrderProvider
    {
        /// <summary>
        /// Adds the specified order to be processed
        /// </summary>
        /// <param name="request">The <see cref="OrderRequest"/> to be processed</param>
        /// <returns>The <see cref="OrderTicket"/> for the corresponding <see cref="OrderRequest.OrderId"/></returns>
        OrderTicket Process(OrderRequest request);
    }
}