namespace QuantConnect.Interfaces
{
    /// <summary>
    /// Primary Exchange Provider interface
    /// </summary>
    public interface IPrimaryExchangeProvider
    {
        /// <summary>
        /// Gets the primary exchange for a given security identifier
        /// </summary>
        /// <param name="securityIdentifier">The security identifier to get the primary exchange for</param>
        /// <returns>Returns the primary exchange or null if not found</returns>
        Exchange GetPrimaryExchange(SecurityIdentifier securityIdentifier);
    }
}
