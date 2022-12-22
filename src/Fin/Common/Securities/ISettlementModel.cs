using System;

namespace QuantConnect.Securities
{
    /// <summary>
    /// Represents the model responsible for applying cash settlement rules
    /// </summary>
    public interface ISettlementModel
    {
        /// <summary>
        /// Applies cash settlement rules
        /// </summary>
        /// <param name="portfolio">The algorithm's portfolio</param>
        /// <param name="security">The fill's security</param>
        /// <param name="applicationTimeUtc">The fill time (in UTC)</param>
        /// <param name="currency">The currency symbol</param>
        /// <param name="amount">The amount of cash to apply</param>
        void ApplyFunds(SecurityPortfolioManager portfolio, Security security, DateTime applicationTimeUtc, string currency, decimal amount);
    }
}
