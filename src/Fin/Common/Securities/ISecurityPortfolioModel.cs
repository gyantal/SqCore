using QuantConnect.Orders;

namespace QuantConnect.Securities
{
    /// <summary>
    /// Performs order fill application to portfolio
    /// </summary>
    public interface ISecurityPortfolioModel
    {
        /// <summary>
        /// Performs application of an OrderEvent to the portfolio
        /// </summary>
        /// <param name="portfolio">The algorithm's portfolio</param>
        /// <param name="security">The fill's security</param>
        /// <param name="fill">The order event fill object to be applied</param>
        void ProcessFill(SecurityPortfolioManager portfolio, Security security, OrderEvent fill);
    }
}