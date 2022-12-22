namespace QuantConnect.Securities.Option.StrategyMatcher
{
    /// <summary>
    /// Specifies the type of value being compared against in a <see cref="OptionStrategyLegPredicate"/>.
    /// These values define the limits of what can be filtered and must match available slice methods in
    /// <see cref="OptionPositionCollection"/>
    /// </summary>
    public enum PredicateTargetValue
    {
        /// <summary>
        /// Predicate matches on <see cref="OptionPosition.Right"/> (0)
        /// </summary>
        Right,

        /// <summary>
        /// Predicate match on <see cref="OptionPosition.Quantity"/> (1)
        /// </summary>
        Quantity,

        /// <summary>
        /// Predicate matches on <see cref="OptionPosition.Strike"/> (2)
        /// </summary>
        Strike,

        /// <summary>
        /// Predicate matches on <see cref="OptionPosition.Expiration"/> (3)
        /// </summary>
        Expiration
    }
}