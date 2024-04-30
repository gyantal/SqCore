using System;
using QuantConnect.Interfaces;
using QuantConnect.Securities;

namespace QuantConnect.Orders
{
    /// <summary>
    /// Fix Price order type -Fixed prices coming from Algorithm.PortTradeHist (List<Fin.Base.Trade>)
    /// </summary>
    public class FixPriceOrder : Order
    {
        /// <summary>
        /// FixPrice Order Type
        /// </summary>
        public override OrderType Type
        {
            get { return OrderType.FixPrice; }
        }

        public decimal FixPrice
        {
            get; set;
        }

        /// <summary>
        /// Intiializes a new instance of the <see cref="FixPriceOrder"/> class.
        /// </summary>
        public FixPriceOrder()
        {
        }

        /// <summary>
        /// Intiializes a new instance of the <see cref="FixPriceOrder"/> class.
        /// </summary>
        /// <param name="symbol">The security's symbol being ordered</param>
        /// <param name="quantity">The number of units to order</param>
        /// <param name="time">The current time</param>
        /// <param name="tag">A user defined tag for the order</param>
        /// <param name="properties">The order properties for this order</param>
        public FixPriceOrder(Symbol symbol, decimal quantity, DateTime time, string tag = "", IOrderProperties properties = null, decimal fixPrice = decimal.MinValue)
            : base(symbol, quantity, time, tag, properties)
        {
            FixPrice = fixPrice;
        }

        /// <summary>
        /// Gets the order value in units of the security's quote currency
        /// </summary>
        /// <param name="security">The security matching this order's symbol</param>
        protected override decimal GetValueImpl(Security security)
        {
            return Quantity * security.Price;
        }

        /// <summary>
        /// Creates a deep-copy clone of this order
        /// </summary>
        /// <returns>A copy of this order</returns>
        public override Order Clone()
        {
            var order = new FixPriceOrder();
            CopyTo(order);
            order.FixPrice = this.FixPrice; // copy our private field too
            return order;
        }
    }
}
