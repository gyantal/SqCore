using System.Linq;

namespace QuantConnect.Securities.Option.StrategyMatcher
{
    /// <summary>
    /// Provides an implementation of <see cref="IOptionStrategyMatchObjectiveFunction"/> that evaluates the number of unmatched
    /// positions, in number of contracts, giving precedence to solutions that have fewer unmatched contracts.
    /// </summary>
    public class UnmatchedPositionCountOptionStrategyMatchObjectiveFunction : IOptionStrategyMatchObjectiveFunction
    {
        /// <summary>
        /// Computes the delta in matched vs unmatched positions, which gives precedence to solutions that match more contracts.
        /// </summary>
        public decimal ComputeScore(OptionPositionCollection input, OptionStrategyMatch match, OptionPositionCollection unmatched)
        {
            var value = 0m;
            foreach (var strategy in match.Strategies)
            {
                foreach (var leg in strategy.OptionLegs.Concat<OptionStrategy.LegData>(strategy.UnderlyingLegs))
                {
                    value += leg.Quantity;
                }
            }

            return value - unmatched.Count;
        }
    }
}