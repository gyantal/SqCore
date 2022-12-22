using System;
using System.Collections.Generic;
using QuantConnect.Orders;

namespace QuantConnect.Securities
{
    /// <summary>
    /// Represents a type capable of fetching Order instances by its QC order id or by a brokerage id
    /// </summary>
    public interface IOrderProvider
    {
        /// <summary>
        /// Gets the current number of orders that have been processed
        /// </summary>
        int OrdersCount { get; }

        /// <summary>
        /// Get the order by its id
        /// </summary>
        /// <param name="orderId">Order id to fetch</param>
        /// <returns>A clone of the order with the specified id, or null if no match is found</returns>
        Order GetOrderById(int orderId);

        /// <summary>
        /// Gets the order by its brokerage id
        /// </summary>
        /// <param name="brokerageId">The brokerage id to fetch</param>
        /// <returns>The first order matching the brokerage id, or null if no match is found</returns>
        Order GetOrderByBrokerageId(string brokerageId);

        /// <summary>
        /// Gets and enumerable of <see cref="OrderTicket"/> matching the specified <paramref name="filter"/>
        /// </summary>
        /// <param name="filter">The filter predicate used to find the required order tickets. If null is specified then all tickets are returned</param>
        /// <returns>An enumerable of <see cref="OrderTicket"/> matching the specified <paramref name="filter"/></returns>
        IEnumerable<OrderTicket> GetOrderTickets(Func<OrderTicket, bool> filter = null);

        /// <summary>
        /// Gets and enumerable of opened <see cref="OrderTicket"/> matching the specified <paramref name="filter"/>
        /// </summary>
        /// <param name="filter">The filter predicate used to find the required order tickets. If null is specified then all tickets are returned</param>
        /// <returns>An enumerable of opened <see cref="OrderTicket"/> matching the specified <paramref name="filter"/></returns>
        IEnumerable<OrderTicket> GetOpenOrderTickets(Func<OrderTicket, bool> filter = null);

        /// <summary>
        /// Gets the order ticket for the specified order id. Returns null if not found
        /// </summary>
        /// <param name="orderId">The order's id</param>
        /// <returns>The order ticket with the specified id, or null if not found</returns>
        OrderTicket GetOrderTicket(int orderId);

        /// <summary>
        /// Gets all orders matching the specified filter. Specifying null will return an enumerable
        /// of all orders.
        /// </summary>
        /// <param name="filter">Delegate used to filter the orders</param>
        /// <returns>All orders this order provider currently holds by the specified filter</returns>
        IEnumerable<Order> GetOrders(Func<Order, bool> filter = null);

        /// <summary>
        /// Gets open orders matching the specified filter. Specifying null will return an enumerable
        /// of all open orders.
        /// </summary>
        /// <param name="filter">Delegate used to filter the orders</param>
        /// <returns>All filtered open orders this order provider currently holds</returns>
        List<Order> GetOpenOrders(Func<Order, bool> filter = null);
    }

    /// <summary>
    /// Provides extension methods for the <see cref="IOrderProvider"/> interface
    /// </summary>
    public static class OrderProviderExtensions
    {
        /// <summary>
        /// Gets the order by its brokerage id
        /// </summary>
        /// <param name="orderProvider">The order provider to search</param>
        /// <param name="brokerageId">The brokerage id to fetch</param>
        /// <returns>The first order matching the brokerage id, or null if no match is found</returns>
        public static Order GetOrderByBrokerageId(this IOrderProvider orderProvider, long brokerageId)
        {
            return orderProvider.GetOrderByBrokerageId(brokerageId.ToStringInvariant());
        }

        /// <summary>
        /// Gets the order by its brokerage id
        /// </summary>
        /// <param name="orderProvider">The order provider to search</param>
        /// <param name="brokerageId">The brokerage id to fetch</param>
        /// <returns>The first order matching the brokerage id, or null if no match is found</returns>
        public static Order GetOrderByBrokerageId(this IOrderProvider orderProvider, int brokerageId)
        {
            return orderProvider.GetOrderByBrokerageId(brokerageId.ToStringInvariant());
        }
    }
}