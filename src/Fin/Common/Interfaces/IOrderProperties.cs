using QuantConnect.Orders;

namespace QuantConnect.Interfaces
{
    /// <summary>
    /// Contains additional properties and settings for an order
    /// </summary>
    public interface IOrderProperties
    {
        /// <summary>
        /// Defines the length of time over which an order will continue working before it is cancelled
        /// </summary>
        TimeInForce TimeInForce { get; set; }

        /// <summary>
        /// Returns a new instance clone of this object
        /// </summary>
        IOrderProperties Clone();
    }
}
