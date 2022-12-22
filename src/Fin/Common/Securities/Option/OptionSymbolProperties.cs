namespace QuantConnect.Securities.Option
{
    /// <summary>
    /// Represents common properties for a specific option contract
    /// </summary>
    public class OptionSymbolProperties : SymbolProperties
    {
        /// <summary>
        /// When the holder of an equity option exercises one contract, or when the writer of an equity option is assigned
        /// an exercise notice on one contract, this unit of trade, usually 100 shares of the underlying security, changes hands.
        /// </summary>
        public int ContractUnitOfTrade
        {
            get; protected set;
        }

        /// <summary>
        /// Overridable minimum price variation, required for index options contracts with
        /// variable sized quoted prices depending on the premium of the option.
        /// </summary>
        public override decimal MinimumPriceVariation
        {
            get;
            protected set;
        }

        /// <summary>
        /// Creates an instance of the <see cref="OptionSymbolProperties"/> class
        /// </summary>
        public OptionSymbolProperties(string description, string quoteCurrency, decimal contractMultiplier, decimal pipSize, decimal lotSize)
            : base(description, quoteCurrency, contractMultiplier, pipSize, lotSize, string.Empty)
        {
            ContractUnitOfTrade = (int)contractMultiplier;
        }

        /// <summary>
        /// Creates an instance of the <see cref="OptionSymbolProperties"/> class from <see cref="SymbolProperties"/> class
        /// </summary>
        public OptionSymbolProperties(SymbolProperties properties)
            : base(properties.Description,
                 properties.QuoteCurrency,
                 properties.ContractMultiplier,
                 properties.MinimumPriceVariation,
                 properties.LotSize,
                 properties.MarketTicker)
        {
            ContractUnitOfTrade = (int)properties.ContractMultiplier;
        }

        internal void SetContractUnitOfTrade(int unitOfTrade)
        {
            ContractUnitOfTrade = unitOfTrade;
        }

        internal void SetContractMultiplier(decimal multiplier)
        {
            ContractMultiplier = multiplier;
        }
    }
}
