using QuantConnect.Securities;
using QuantConnect.Orders.Fees;

namespace QuantConnect.Orders.Fills
{
    /// <summary>
    /// Represents the fill model used to simulate order fills for futures
    /// </summary>
    public class FutureFillModel : ImmediateFillModel
    {
        /// <summary>
        /// Default market fill model for the base security class. Fills at the last traded price.
        /// </summary>
        /// <param name="asset">Security asset we're filling</param>
        /// <param name="order">Order packet to model</param>
        /// <returns>Order fill information detailing the average price and quantity filled.</returns>
        public override OrderEvent MarketFill(Security asset, MarketOrder order)
        {
            //Default order event to return.
            var utcTime = asset.LocalTime.ConvertToUtc(asset.Exchange.TimeZone);
            var fill = new OrderEvent(order, utcTime, OrderFee.Zero);

            if (order.Status == OrderStatus.Canceled) return fill;

            // make sure the exchange is open on regular/extended market hours before filling
            if (!IsExchangeOpen(asset, true)) return fill;

            var prices = GetPricesCheckingPythonWrapper(asset, order.Direction);
            var pricesEndTimeUtc = prices.EndTime.ConvertToUtc(asset.Exchange.TimeZone);

            // if the order is filled on stale (fill-forward) data, set a warning message on the order event
            if (pricesEndTimeUtc.Add(Parameters.StalePriceTimeSpan) < order.Time)
            {
                fill.Message = $"Warning: fill at stale price ({prices.EndTime.ToStringInvariant()} {asset.Exchange.TimeZone})";
            }

            //Order [fill]price for a market order model is the current security price
            fill.FillPrice = prices.Current;
            fill.Status = OrderStatus.Filled;

            //Calculate the model slippage: e.g. 0.01c
            var slip = asset.SlippageModel.GetSlippageApproximation(asset, order);

            //Apply slippage
            switch (order.Direction)
            {
                case OrderDirection.Buy:
                    fill.FillPrice += slip;
                    break;
                case OrderDirection.Sell:
                    fill.FillPrice -= slip;
                    break;
            }

            // assume the order completely filled
            fill.FillQuantity = order.Quantity;

            return fill;
        }
    }
}
