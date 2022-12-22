namespace QuantConnect.Securities
{
    /// <summary>
    /// Represents a type capable of fetching the holdings for the specified symbol
    /// </summary>
    public interface ISecurityProvider
    {
        /// <summary>
        /// Retrieves a summary of the holdings for the specified symbol
        /// </summary>
        /// <param name="symbol">The symbol to get holdings for</param>
        /// <returns>The holdings for the symbol or null if the symbol is invalid and/or not in the portfolio</returns>
        Security GetSecurity(Symbol symbol);
    }

    /// <summary>
    /// Provides extension methods for the <see cref="ISecurityProvider"/> interface.
    /// </summary>
    public static class SecurityProviderExtensions
    {
        /// <summary>
        /// Extension method to return the quantity of holdings, if no holdings are present, then zero is returned.
        /// </summary>
        /// <param name="provider">The <see cref="ISecurityProvider"/></param>
        /// <param name="symbol">The symbol we want holdings quantity for</param>
        /// <returns>The quantity of holdings for the specified symbol</returns>
        public static decimal GetHoldingsQuantity(this ISecurityProvider provider, Symbol symbol)
        {
            var security = provider.GetSecurity(symbol);
            return security == null ? 0 : security.Holdings.Quantity;
        }
    }
}