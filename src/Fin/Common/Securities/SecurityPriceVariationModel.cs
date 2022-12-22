namespace QuantConnect.Securities
{
    /// <summary>
    /// Provides default implementation of <see cref="IPriceVariationModel"/>
    /// for use in defining the minimum price variation.
    /// </summary>
    public class SecurityPriceVariationModel : IPriceVariationModel
    {
        /// <summary>
        /// Get the minimum price variation from a security
        /// </summary>
        /// <param name="parameters">An object containing the method parameters</param>
        /// <returns>Decimal minimum price variation of a given security</returns>
        public virtual decimal GetMinimumPriceVariation(GetMinimumPriceVariationParameters parameters)
        {
            return parameters.Security.SymbolProperties.MinimumPriceVariation;
        }
    }
}