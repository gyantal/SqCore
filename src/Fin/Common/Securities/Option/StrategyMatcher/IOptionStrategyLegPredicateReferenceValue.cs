using System.Collections.Generic;

namespace QuantConnect.Securities.Option.StrategyMatcher
{
    /// <summary>
    /// When decoding leg predicates, we extract the value we're comparing against
    /// If we're comparing against another leg's value (such as legs[0].Strike), then
    /// we'll create a OptionStrategyLegPredicateReferenceValue. If we're comparing against a literal/constant value,
    /// then we'll create a ConstantOptionStrategyLegPredicateReferenceValue. These reference values are used to slice
    /// the <see cref="OptionPositionCollection"/> to only include positions matching the
    /// predicate.
    /// </summary>
    public interface IOptionStrategyLegPredicateReferenceValue
    {
        /// <summary>
        /// Gets the target of this value
        /// </summary>
        PredicateTargetValue Target { get; }

        /// <summary>
        /// Resolves the value of the comparand specified in an <see cref="OptionStrategyLegPredicate"/>.
        /// For example, the predicate may include ... > legs[0].Strike, and upon evaluation, we need to
        /// be able to extract leg[0].Strike for the currently contemplated set of legs adhering to a
        /// strategy's definition.
        /// </summary>
        object Resolve(IReadOnlyList<OptionPosition> legs);
    }
}