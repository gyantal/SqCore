using System.Collections.Generic;

namespace QuantConnect.Securities.Option.StrategyMatcher
{
    /// <summary>
    /// Defines a complete result from running the matcher on a collection of positions.
    /// The matching process will return one these matches for every potential combination
    /// of strategies conforming to the search settings and the positions provided.
    /// </summary>
    public class OptionStrategyMatch
    {
        /// <summary>
        /// The strategies that were matched
        /// </summary>
        public List<OptionStrategy> Strategies { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="OptionStrategyMatch"/> class
        /// </summary>
        public OptionStrategyMatch(List<OptionStrategy> strategies)
        {
            Strategies = strategies;
        }
    }
}
