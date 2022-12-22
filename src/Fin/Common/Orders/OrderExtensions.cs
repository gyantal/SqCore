namespace QuantConnect.Orders
{
    /// <summary>
    /// Provides extension methods for the <see cref="Order"/> class and for the <see cref="OrderStatus"/> enumeration
    /// </summary>
    public static class OrderExtensions
    {
        /// <summary>
        /// Determines if the specified status is in a closed state.
        /// </summary>
        /// <param name="status">The status to check</param>
        /// <returns>True if the status is <see cref="OrderStatus.Filled"/>, <see cref="OrderStatus.Canceled"/>, or <see cref="OrderStatus.Invalid"/></returns>
        public static bool IsClosed(this OrderStatus status)
        {
            return status == OrderStatus.Filled
                || status == OrderStatus.Canceled
                || status == OrderStatus.Invalid;
        }

        /// <summary>
        /// Determines if the specified status is in an open state.
        /// </summary>
        /// <param name="status">The status to check</param>
        /// <returns>True if the status is not <see cref="OrderStatus.Filled"/>, <see cref="OrderStatus.Canceled"/>, or <see cref="OrderStatus.Invalid"/></returns>
        public static bool IsOpen(this OrderStatus status)
        {
            return !status.IsClosed();
        }

        /// <summary>
        /// Determines if the specified status is a fill, that is, <see cref="OrderStatus.Filled"/>
        /// order <see cref="OrderStatus.PartiallyFilled"/>
        /// </summary>
        /// <param name="status">The status to check</param>
        /// <returns>True if the status is <see cref="OrderStatus.Filled"/> or <see cref="OrderStatus.PartiallyFilled"/>, false otherwise</returns>
        public static bool IsFill(this OrderStatus status)
        {
            return status == OrderStatus.Filled || status == OrderStatus.PartiallyFilled;
        }

        /// <summary>
        /// Determines whether or not the specified order is a limit order
        /// </summary>
        /// <param name="orderType">The order to check</param>
        /// <returns>True if the order is a limit order, false otherwise</returns>
        public static bool IsLimitOrder(this OrderType orderType)
        {
            return orderType == OrderType.Limit || orderType == OrderType.StopLimit;
        }

        /// <summary>
        /// Determines whether or not the specified order is a stop order
        /// </summary>
        /// <param name="orderType">The order to check</param>
        /// <returns>True if the order is a stop order, false otherwise</returns>
        public static bool IsStopOrder(this OrderType orderType)
        {
            return orderType == OrderType.StopMarket || orderType == OrderType.StopLimit;
        }
    }
}
