using System;

namespace QuantConnect.Securities
{
    /// <summary>
    /// Represents the model responsible for applying cash settlement rules
    /// </summary>
    /// <remarks>This model applies cash settlement immediately</remarks>
    public class ImmediateSettlementModel : ISettlementModel
    {
        /// <summary>
        /// Applies cash settlement rules
        /// </summary>
        /// <param name="portfolio">The algorithm's portfolio</param>
        /// <param name="security">The fill's security</param>
        /// <param name="applicationTimeUtc">The fill time (in UTC)</param>
        /// <param name="currency">The currency symbol</param>
        /// <param name="amount">The amount of cash to apply</param>
        public void ApplyFunds(SecurityPortfolioManager portfolio, Security security, DateTime applicationTimeUtc, string currency, decimal amount)
        {
            portfolio.CashBook[currency].AddAmount(amount);
        }
    }
}
