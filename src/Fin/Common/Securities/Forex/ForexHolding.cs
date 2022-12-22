using static System.Math;

namespace QuantConnect.Securities.Forex
{
    /// <summary>
    /// FOREX holdings implementation of the base securities class
    /// </summary>
    /// <seealso cref="SecurityHolding"/>
    public class ForexHolding : SecurityHolding
    {
        /// <summary>
        /// Forex Holding Class
        /// </summary>
        /// <param name="security">The forex security being held</param>
        /// <param name="currencyConverter">A currency converter instance</param>
        public ForexHolding(Forex security, ICurrencyConverter currencyConverter)
            : base(security, currencyConverter)
        {
        }

        /// <summary>
        /// Profit in pips if we closed the holdings right now including the approximate fees
        /// </summary>
        public decimal TotalCloseProfitPips()
        {
            var pipDecimal = Security.SymbolProperties.MinimumPriceVariation * 10;
            var exchangeRate = Security.QuoteCurrency.ConversionRate;

            var pipCashCurrencyValue = (pipDecimal * AbsoluteQuantity * exchangeRate);
            return Round((TotalCloseProfit() / pipCashCurrencyValue), 1);
        }
    }
}