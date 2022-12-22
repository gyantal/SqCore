using System.Collections.Generic;

namespace QuantConnect.Securities.Option.StrategyMatcher
{
    /// <summary>
    /// Matches <see cref="OptionPositionCollection"/> against a collection of <see cref="OptionStrategyDefinition"/>
    /// according to the <see cref="OptionStrategyMatcherOptions"/> provided.
    /// </summary>
    public class OptionStrategyMatcher
    {
        /// <summary>
        /// Specifies options controlling how the matcher operates
        /// </summary>
        public OptionStrategyMatcherOptions Options { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="OptionStrategyMatcher"/> class
        /// </summary>
        /// <param name="options">Specifies definitions and other options controlling the matcher</param>
        public OptionStrategyMatcher(OptionStrategyMatcherOptions options)
        {
            Options = options;
        }

        // TODO : Implement matching multiple permutations and using the objective function to select the best solution

        /// <summary>
        /// Using the definitions provided in <see cref="Options"/>, attempts to match all <paramref name="positions"/>.
        /// The resulting <see cref="OptionStrategyMatch"/> presents a single, valid solution for matching as many positions
        /// as possible.
        /// </summary>
        public OptionStrategyMatch MatchOnce(OptionPositionCollection positions)
        {
            // these definitions are enumerated according to the configured IOptionStrategyDefinitionEnumerator

            var strategies = new List<OptionStrategy>();
            foreach (var definition in Options.Definitions)
            {
                // simplest implementation here is to match one at a time, updating positions in between
                // a better implementation would be to evaluate all possible matches and make decisions
                // prioritizing positions that would require more margin if not matched

                OptionStrategyDefinitionMatch match;
                while (definition.TryMatchOnce(Options, positions, out match))
                {
                    positions = match.RemoveFrom(positions);
                    strategies.Add(match.CreateStrategy());
                }

                if (positions.IsEmpty)
                {
                    break;
                }
            }

            return new OptionStrategyMatch(strategies);
        }
    }
}
