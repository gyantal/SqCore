namespace QuantConnect.Securities
{
    /// <summary>
    /// Defines the parameters for <see cref="IPriceVariationModel.GetMinimumPriceVariation"/>
    /// </summary>
    public class GetMinimumPriceVariationParameters
    {
        /// <summary>
        /// Gets the security
        /// </summary>
        public Security Security { get; }

        /// <summary>
        /// Gets the reference price to be used for the calculation
        /// </summary>
        public decimal ReferencePrice { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="GetMinimumPriceVariationParameters"/> class
        /// </summary>
        /// <param name="security">The security</param>
        /// <param name="referencePrice">The reference price to be used for the calculation</param>
        public GetMinimumPriceVariationParameters(Security security, decimal referencePrice)
        {
            Security = security;
            ReferencePrice = referencePrice;
        }
    }
}
