namespace QuantConnect.Securities
{
    /// <summary>
    /// Provides an implementation of <see cref="IPriceVariationModel"/>
    /// for use when data is <see cref="DataNormalizationMode.Adjusted"/>.
    /// </summary>
    public class AdjustedPriceVariationModel : IPriceVariationModel
    {
        /// <summary>
        /// Get the minimum price variation from a security
        /// </summary>
        /// <param name="parameters">An object containing the method parameters</param>
        /// <returns>Zero</returns>
        public virtual decimal GetMinimumPriceVariation(GetMinimumPriceVariationParameters parameters)
        {
            return 0;
        }
    }
}