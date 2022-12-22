namespace QuantConnect.Securities
{
    /// <summary>
    /// Result type for <see cref="IBuyingPowerModel.GetInitialMarginRequirement"/>
    /// and <see cref="IBuyingPowerModel.GetInitialMarginRequiredForOrder"/>
    /// </summary>
    public class InitialMargin
    {
        /// <summary>
        /// Gets an instance of <see cref="InitialMargin"/> with zero values
        /// </summary>
        public static InitialMargin Zero { get; } = new InitialMargin(0m);

        /// <summary>
        /// The initial margin value in account currency
        /// </summary>
        public decimal Value { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="InitialMargin"/> class
        /// </summary>
        /// <param name="value">The initial margin</param>
        public InitialMargin(decimal value)
        {
            Value = value;
        }

        /// <summary>
        /// Implicit operator <see cref="InitialMargin"/> -> <see cref="decimal"/>
        /// </summary>
        public static implicit operator decimal(InitialMargin margin)
        {
            return margin.Value;
        }

        /// <summary>
        /// Implicit operator <see cref="decimal"/> -> <see cref="InitialMargin"/>
        /// </summary>
        public static implicit operator InitialMargin(decimal margin)
        {
            return new InitialMargin(margin);
        }
    }
}
