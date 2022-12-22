using System;
using QuantConnect.Interfaces;
using QuantConnect.Securities;

namespace QuantConnect.Orders
{
    /// <summary>
    /// Market order type definition
    /// </summary>
    public class MarketOrder : Order
    {
        /// <summary>
        /// Added a default constructor for JSON Deserialization:
        /// </summary>
        public MarketOrder()
        {
        }

        /// <summary>
        /// Market Order Type
        /// </summary>
        public override OrderType Type
        {
            get { return OrderType.Market; }
        }

        /// <summary>
        /// New market order constructor
        /// </summary>
        /// <param name="symbol">Symbol asset we're seeking to trade</param>
        /// <param name="quantity">Quantity of the asset we're seeking to trade</param>
        /// <param name="time">Time the order was placed</param>
        /// <param name="price">Price of the order</param>
        /// <param name="tag">User defined data tag for this order</param>
        /// <param name="properties">The order properties for this order</param>
        public MarketOrder(Symbol symbol, decimal quantity, DateTime time, decimal price, string tag = "", IOrderProperties properties = null)
            : this(symbol, quantity, time, tag, properties)
        {
            Price = price;
        }

        /// <summary>
        /// New market order constructor
        /// </summary>
        /// <param name="symbol">Symbol asset we're seeking to trade</param>
        /// <param name="quantity">Quantity of the asset we're seeking to trade</param>
        /// <param name="time">Time the order was placed</param>
        /// <param name="tag">User defined data tag for this order</param>
        /// <param name="properties">The order properties for this order</param>
        public MarketOrder(Symbol symbol, decimal quantity, DateTime time, string tag = "", IOrderProperties properties = null)
            : base(symbol, quantity, time, tag, properties)
        {
        }

        /// <summary>
        /// Gets the order value in units of the security's quote currency
        /// </summary>
        /// <param name="security">The security matching this order's symbol</param>
        protected override decimal GetValueImpl(Security security)
        {
            return Quantity*security.Price;
        }

        /// <summary>
        /// Creates a deep-copy clone of this order
        /// </summary>
        /// <returns>A copy of this order</returns>
        public override Order Clone()
        {
            var order = new MarketOrder();
            CopyTo(order);
            return order;
        }
    }
}
