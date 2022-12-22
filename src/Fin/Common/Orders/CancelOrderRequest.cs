using System;
using static QuantConnect.StringExtensions;

namespace QuantConnect.Orders
{
    /// <summary>
    /// Defines a request to cancel an order
    /// </summary>
    public class CancelOrderRequest : OrderRequest
    {
        /// <summary>
        /// Gets <see cref="Orders.OrderRequestType.Cancel"/>
        /// </summary>
        public override OrderRequestType OrderRequestType
        {
            get { return OrderRequestType.Cancel; }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CancelOrderRequest"/> class
        /// </summary>
        /// <param name="time">The time this cancelation was requested</param>
        /// <param name="orderId">The order id to be canceled</param>
        /// <param name="tag">A new tag for the order</param>
        public CancelOrderRequest(DateTime time, int orderId, string tag)
            : base(time, orderId, tag)
        {
        }

        /// <summary>
        /// Returns a string that represents the current object.
        /// </summary>
        /// <returns>
        /// A string that represents the current object.
        /// </returns>
        /// <filterpriority>2</filterpriority>
        public override string ToString()
        {
            return Invariant($"{Time.ToStringInvariant()} UTC: Cancel Order: ({OrderId}) - {Tag} Status: {Status}");
        }
    }
}