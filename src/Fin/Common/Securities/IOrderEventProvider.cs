using System;
using QuantConnect.Orders;

namespace QuantConnect.Securities
{
    /// <summary>
    /// Represents a type with a new <see cref="OrderEvent"/> event <see cref="EventHandler"/>.
    /// </summary>
    public interface IOrderEventProvider
    {
        /// <summary>
        /// Event fired when there is a new <see cref="OrderEvent"/>
        /// </summary>
        /// <remarks>Will be called before the <see cref="SecurityPortfolioManager"/></remarks>
        event EventHandler<OrderEvent> NewOrderEvent;
    }
}