using System;
using QuantConnect.Interfaces;
using QuantConnect.Securities;

namespace QuantConnect.Orders
{
    /// <summary>
    /// Market on Open order type, submits a market order when the exchange opens
    /// </summary>
    public class MarketOnOpenOrder : Order
    {
        /// <summary>
        /// MarketOnOpen Order Type
        /// </summary>
        public override OrderType Type
        {
            get { return OrderType.MarketOnOpen; }
        }

        /// <summary>
        /// Intiializes a new instance of the <see cref="MarketOnOpenOrder"/> class.
        /// </summary>
        public MarketOnOpenOrder()
        {
        }

        /// <summary>
        /// Intiializes a new instance of the <see cref="MarketOnOpenOrder"/> class.
        /// </summary>
        /// <param name="symbol">The security's symbol being ordered</param>
        /// <param name="quantity">The number of units to order</param>
        /// <param name="time">The current time</param>
        /// <param name="tag">A user defined tag for the order</param>
        /// <param name="properties">The order properties for this order</param>
        public MarketOnOpenOrder(Symbol symbol, decimal quantity, DateTime time, string tag = "", IOrderProperties properties = null)
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
            var order = new MarketOnOpenOrder();
            CopyTo(order);
            return order;
        }
    }
}
