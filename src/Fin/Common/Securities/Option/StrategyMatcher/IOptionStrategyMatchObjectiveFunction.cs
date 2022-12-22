namespace QuantConnect.Securities.Option.StrategyMatcher
{
    /// <summary>
    /// Evaluates the provided match to assign an objective score. Higher scores are better.
    /// </summary>
    public interface IOptionStrategyMatchObjectiveFunction
    {
        /// <summary>
        /// Evaluates the objective function for the provided match solution. Solution with the highest score will be selected
        /// as the solution. NOTE: This part of the match has not been implemented as of 2020-11-06 as it's only evaluating the
        /// first solution match (MatchOnce).
        /// </summary>
        decimal ComputeScore(OptionPositionCollection input, OptionStrategyMatch match, OptionPositionCollection unmatched);
    }
}