namespace QuantConnect.Securities
{
    /// <summary>
    /// Defines the parameters for <see cref="IBuyingPowerModel.GetMaximumOrderQuantityForDeltaBuyingPower"/>
    /// </summary>
    public class GetMaximumOrderQuantityForDeltaBuyingPowerParameters
    {
        /// <summary>
        /// Gets the algorithm's portfolio
        /// </summary>
        public SecurityPortfolioManager Portfolio { get; }

        /// <summary>
        /// Gets the security
        /// </summary>
        public Security Security { get; }

        /// <summary>
        /// The delta buying power.
        /// </summary>
        /// <remarks>Sign defines the position side to apply the delta, positive long, negative short side.</remarks>
        public decimal DeltaBuyingPower { get; }

        /// <summary>
        /// True enables the <see cref="IBuyingPowerModel"/> to skip setting <see cref="GetMaximumOrderQuantityResult.Reason"/>
        /// for non error situations, for performance
        /// </summary>
        public bool SilenceNonErrorReasons { get; }

        /// <summary>
        /// Configurable minimum order margin portfolio percentage to ignore bad orders, orders with unrealistic small sizes
        /// </summary>
        /// <remarks>Default value is 0. This setting is useful to avoid small trading noise when using SetHoldings</remarks>
        public decimal MinimumOrderMarginPortfolioPercentage { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="GetMaximumOrderQuantityForDeltaBuyingPowerParameters"/> class
        /// </summary>
        /// <param name="portfolio">The algorithm's portfolio</param>
        /// <param name="security">The security</param>
        /// <param name="deltaBuyingPower">The delta buying power to apply.
        /// Sign defines the position side to apply the delta</param>
        /// <param name="minimumOrderMarginPortfolioPercentage">Configurable minimum order margin portfolio percentage to ignore orders with unrealistic small sizes</param>
        /// <param name="silenceNonErrorReasons">True will not return <see cref="GetMaximumOrderQuantityResult.Reason"/>
        /// set for non error situation, this is for performance</param>
        public GetMaximumOrderQuantityForDeltaBuyingPowerParameters(SecurityPortfolioManager portfolio, Security security, decimal deltaBuyingPower,
            decimal minimumOrderMarginPortfolioPercentage, bool silenceNonErrorReasons = false)
        {
            Portfolio = portfolio;
            Security = security;
            DeltaBuyingPower = deltaBuyingPower;
            SilenceNonErrorReasons = silenceNonErrorReasons;
            MinimumOrderMarginPortfolioPercentage = minimumOrderMarginPortfolioPercentage;
        }
    }
}
