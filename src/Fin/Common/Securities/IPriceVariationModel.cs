namespace QuantConnect.Securities
{
    /// <summary>
    /// Gets the minimum price variation of a given security
    /// </summary>
    public interface IPriceVariationModel
    {
        /// <summary>
        /// Get the minimum price variation from a security
        /// </summary>
        /// <param name="parameters">An object containing the method parameters</param>
        /// <returns>Decimal minimum price variation of a given security</returns>
        decimal GetMinimumPriceVariation(GetMinimumPriceVariationParameters parameters);
    }
}
