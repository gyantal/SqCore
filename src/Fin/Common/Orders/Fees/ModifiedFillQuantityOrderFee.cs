using QuantConnect.Securities;

namespace QuantConnect.Orders.Fees
{
    /// <summary>
    /// An order fee where the fee quantity has already been subtracted from the filled quantity so instead we subtracted
    /// from the quote currency when applied to the portfolio
    /// </summary>
    /// <remarks>
    /// This type of order fee is returned by some crypto brokerages (e.g. Bitfinex and Binance)
    /// with buy orders with cash accounts.
    /// </remarks>
    public class ModifiedFillQuantityOrderFee : OrderFee
    {
        private readonly string _quoteCurrency;
        private readonly decimal _contractMultiplier;

        /// <summary>
        /// Initializes a new instance of the <see cref="ModifiedFillQuantityOrderFee"/> class
        /// </summary>
        /// <param name="orderFee">The order fee</param>
        /// <param name="quoteCurrency">The associated security quote currency</param>
        /// <param name="contractMultiplier">The associated security contract multiplier</param>
        public ModifiedFillQuantityOrderFee(CashAmount orderFee, string quoteCurrency, decimal contractMultiplier)
            : base(orderFee)
        {
            _quoteCurrency = quoteCurrency;
            _contractMultiplier = contractMultiplier;
        }

        /// <summary>
        /// Applies the order fee to the given portfolio
        /// </summary>
        /// <param name="portfolio">The portfolio instance</param>
        /// <param name="fill">The order fill event</param>
        public override void ApplyToPortfolio(SecurityPortfolioManager portfolio, OrderEvent fill)
        {
            portfolio.CashBook[_quoteCurrency].AddAmount(-Value.Amount * fill.FillPrice * _contractMultiplier);
        }
    }
}
