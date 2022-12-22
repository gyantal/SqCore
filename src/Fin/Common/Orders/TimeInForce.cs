using System;
using Newtonsoft.Json;
using QuantConnect.Interfaces;
using QuantConnect.Orders.TimeInForces;
using QuantConnect.Securities;

namespace QuantConnect.Orders
{
    /// <summary>
    /// Time In Force - defines the length of time over which an order will continue working before it is canceled
    /// </summary>
    [JsonConverter(typeof(TimeInForceJsonConverter))]
    public abstract class TimeInForce : ITimeInForceHandler
    {
        /// <summary>
        /// Gets a <see cref="GoodTilCanceledTimeInForce"/> instance
        /// </summary>
        public static readonly TimeInForce GoodTilCanceled = new GoodTilCanceledTimeInForce();

        /// <summary>
        /// Gets a <see cref="DayTimeInForce"/> instance
        /// </summary>
        public static readonly TimeInForce Day = new DayTimeInForce();

        /// <summary>
        /// Gets a <see cref="GoodTilDateTimeInForce"/> instance
        /// </summary>
        public static TimeInForce GoodTilDate(DateTime expiry)
        {
            return new GoodTilDateTimeInForce(expiry);
        }

        /// <summary>
        /// Checks if an order is expired
        /// </summary>
        /// <param name="security">The security matching the order</param>
        /// <param name="order">The order to be checked</param>
        /// <returns>Returns true if the order has expired, false otherwise</returns>
        public abstract bool IsOrderExpired(Security security, Order order);

        /// <summary>
        /// Checks if an order fill is valid
        /// </summary>
        /// <param name="security">The security matching the order</param>
        /// <param name="order">The order to be checked</param>
        /// <param name="fill">The order fill to be checked</param>
        /// <returns>Returns true if the order fill can be emitted, false otherwise</returns>
        public abstract bool IsFillValid(Security security, Order order, OrderEvent fill);
    }
}
