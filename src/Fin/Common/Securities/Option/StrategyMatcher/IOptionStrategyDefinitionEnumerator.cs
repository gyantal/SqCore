using System.Collections.Generic;

namespace QuantConnect.Securities.Option.StrategyMatcher
{
    /// <summary>
    /// Enumerates <see cref="OptionStrategyDefinition"/> for the purposes of providing a bias towards definitions
    /// that are more favorable to be matched before matching less favorable definitions.
    /// </summary>
    public interface IOptionStrategyDefinitionEnumerator
    {
        /// <summary>
        /// Enumerates the <paramref name="definitions"/> according to the implementation's own concept of favorability.
        /// </summary>
        IEnumerable<OptionStrategyDefinition> Enumerate(IReadOnlyList<OptionStrategyDefinition> definitions);
    }
}