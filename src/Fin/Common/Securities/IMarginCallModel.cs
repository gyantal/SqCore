﻿using System.Collections.Generic;
using QuantConnect.Orders;

namespace QuantConnect.Securities
{
    /// <summary>
    /// Represents the model responsible for picking which orders should be executed during a margin call
    /// </summary>
    public interface IMarginCallModel
    {
        /// <summary>
        /// Scan the portfolio and the updated data for a potential margin call situation which may get the holdings below zero!
        /// If there is a margin call, liquidate the portfolio immediately before the portfolio gets sub zero.
        /// </summary>
        /// <param name="issueMarginCallWarning">Set to true if a warning should be issued to the algorithm</param>
        /// <returns>True for a margin call on the holdings.</returns>
        List<SubmitOrderRequest> GetMarginCallOrders(out bool issueMarginCallWarning);

        /// <summary>
        /// Executes synchronous orders to bring the account within margin requirements.
        /// </summary>
        /// <param name="generatedMarginCallOrders">These are the margin call orders that were generated
        /// by individual security margin models.</param>
        /// <returns>The list of orders that were actually executed</returns>
        List<OrderTicket> ExecuteMarginCall(IEnumerable<SubmitOrderRequest> generatedMarginCallOrders);
    }

    /// <summary>
    /// Provides access to a null implementation for <see cref="IMarginCallModel"/>
    /// </summary>
    public static class MarginCallModel
    {
        /// <summary>
        /// Gets an instance of <see cref="IMarginCallModel"/> that will always
        /// return an empty list of executed orders.
        /// </summary>
        public static readonly IMarginCallModel Null = new NullMarginCallModel();

        private sealed class NullMarginCallModel : IMarginCallModel
        {
            public List<SubmitOrderRequest> GetMarginCallOrders(out bool issueMarginCallWarning)
            {
                issueMarginCallWarning = false;
                return new List<SubmitOrderRequest>();
            }

            public List<OrderTicket> ExecuteMarginCall(IEnumerable<SubmitOrderRequest> generatedMarginCallOrders)
            {
                return new List<OrderTicket>();
            }
        }
    }
}
