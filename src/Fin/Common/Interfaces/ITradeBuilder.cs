﻿using System.Collections.Generic;
using QuantConnect.Orders;
using QuantConnect.Statistics;

namespace QuantConnect.Interfaces
{
    /// <summary>
    /// Generates trades from executions and market price updates
    /// </summary>
    public interface ITradeBuilder
    {
        /// <summary>
        /// Sets the live mode flag
        /// </summary>
        /// <param name="live">The live mode flag</param>
        void SetLiveMode(bool live);

        /// <summary>
        /// The list of closed trades
        /// </summary>
        List<Trade> ClosedTrades { get; }

        /// <summary>
        /// Returns true if there is an open position for the symbol
        /// </summary>
        /// <param name="symbol">The symbol</param>
        /// <returns>true if there is an open position for the symbol</returns>
        bool HasOpenPosition(Symbol symbol);

        /// <summary>
        /// Sets the current market price for the symbol
        /// </summary>
        /// <param name="symbol"></param>
        /// <param name="price"></param>
        void SetMarketPrice(Symbol symbol, decimal price);

        /// <summary>
        /// Processes a new fill, eventually creating new trades
        /// </summary>
        /// <param name="fill">The new fill order event</param>
        /// <param name="securityConversionRate">The current security market conversion rate into the account currency</param>
        /// <param name="feeInAccountCurrency">The current order fee in the account currency</param>
        /// <param name="multiplier">The contract multiplier</param>
        void ProcessFill(OrderEvent fill,
            decimal securityConversionRate,
            decimal feeInAccountCurrency,
            decimal multiplier = 1.0m);
    }
}
