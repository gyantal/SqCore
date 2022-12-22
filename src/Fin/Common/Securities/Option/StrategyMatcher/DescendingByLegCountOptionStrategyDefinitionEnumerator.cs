using System.Collections.Generic;
using System.Linq;

namespace QuantConnect.Securities.Option.StrategyMatcher
{
    /// <summary>
    /// Provides an implementation of <see cref="IOptionStrategyDefinitionEnumerator"/> that enumerates definitions
    /// requiring more leg matches first. This ensures more complex definitions are evaluated before simpler definitions.
    /// </summary>
    public class DescendingByLegCountOptionStrategyDefinitionEnumerator : IOptionStrategyDefinitionEnumerator
    {
        /// <summary>
        /// Enumerates definitions in descending order of <see cref="OptionStrategyDefinition.LegCount"/>
        /// </summary>
        public IEnumerable<OptionStrategyDefinition> Enumerate(IReadOnlyList<OptionStrategyDefinition> definitions)
        {
            return definitions.OrderByDescending(d => d.LegCount);
        }
    }
}