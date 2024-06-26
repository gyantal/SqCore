﻿using System;
using QuantConnect.Util;
using QuantConnect.Securities;

namespace QuantConnect.Orders.Fees
{
    /// <summary>
    /// Provides an implementation of <see cref="FeeModel"/> that models GDAX order fees
    /// </summary>
    public class GDAXFeeModel : FeeModel
    {
        /// <summary>
        /// Get the fee for this order in quote currency
        /// </summary>
        /// <param name="parameters">A <see cref="OrderFeeParameters"/> object
        /// containing the security and order</param>
        /// <returns>The cost of the order in quote currency</returns>
        public override OrderFee GetOrderFee(OrderFeeParameters parameters)
        {
            var order = parameters.Order;
            var security = parameters.Security;

            // marketable limit orders are considered takers
            var isMaker = order.Type == OrderType.Limit && !order.IsMarketable;

            // Check if the current symbol is a StableCoin
            var isStableCoin = Currencies.StablePairsGDAX.Contains(security.Symbol.Value);

            var feePercentage = GetFeePercentage(order.Time, isMaker, isStableCoin);

            // get order value in quote currency, then apply maker/taker fee factor
            var unitPrice = order.Direction == OrderDirection.Buy ? security.AskPrice : security.BidPrice;
            unitPrice *= security.SymbolProperties.ContractMultiplier;

            // currently we do not model 30-day volume, so we use the first tier

            var fee = unitPrice * order.AbsoluteQuantity * feePercentage;

            return new OrderFee(new CashAmount(fee, security.QuoteCurrency.Symbol));
        }

        /// <summary>
        /// Returns the maker/taker fee percentage effective at the requested date.
        /// </summary>
        /// <param name="utcTime">The date/time requested (UTC)</param>
        /// <param name="isMaker">true if the maker percentage fee is requested, false otherwise</param>
        /// <param name="isStableCoin">true if the order security symbol is a StableCoin, false otherwise</param>
        /// <returns>The fee percentage effective at the requested date</returns>
        public static decimal GetFeePercentage(DateTime utcTime, bool isMaker, bool isStableCoin)
        {
            // Tier 1 fees
            // https://pro.coinbase.com/orders/fees
            // https://blog.coinbase.com/coinbase-pro-market-structure-update-fbd9d49f43d7
            // https://blog.coinbase.com/updates-to-coinbase-pro-fee-structure-b3d9ee586108
            if (isStableCoin)
                return isMaker ? 0m : 0.001m;

            else if (utcTime < new DateTime(2019, 3, 23, 1, 30, 0))
                return isMaker ? 0m : 0.003m;
                
            else if (utcTime < new DateTime(2019, 10, 8, 0, 30, 0))
                return isMaker ? 0.0015m : 0.0025m;

            return isMaker ? 0.005m : 0.005m;
        }
    }
}
