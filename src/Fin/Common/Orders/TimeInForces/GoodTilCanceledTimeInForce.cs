using QuantConnect.Securities;

namespace QuantConnect.Orders.TimeInForces
{
    /// <summary>
    /// Good Til Canceled Time In Force - order does never expires
    /// </summary>
    public class GoodTilCanceledTimeInForce : TimeInForce
    {
        /// <summary>
        /// Checks if an order is expired
        /// </summary>
        /// <param name="security">The security matching the order</param>
        /// <param name="order">The order to be checked</param>
        /// <returns>Returns true if the order has expired, false otherwise</returns>
        public override bool IsOrderExpired(Security security, Order order)
        {
            return false;
        }

        /// <summary>
        /// Checks if an order fill is valid
        /// </summary>
        /// <param name="security">The security matching the order</param>
        /// <param name="order">The order to be checked</param>
        /// <param name="fill">The order fill to be checked</param>
        /// <returns>Returns true if the order fill can be emitted, false otherwise</returns>
        public override bool IsFillValid(Security security, Order order, OrderEvent fill)
        {
            return true;
        }
    }
}
