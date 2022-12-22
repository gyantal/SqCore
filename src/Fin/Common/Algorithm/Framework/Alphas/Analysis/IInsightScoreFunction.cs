namespace QuantConnect.Algorithm.Framework.Alphas.Analysis
{
    /// <summary>
    /// Defines a function used to determine how correct a particular insight is.
    /// The result of calling <see cref="Evaluate"/> is expected to be within the range [0, 1]
    /// where 0 is completely wrong and 1 is completely right
    /// </summary>
    public interface IInsightScoreFunction
    {
        /// <summary>
        /// Evaluates the score of the insight within the context
        /// </summary>
        /// <param name="context">The insight's analysis context</param>
        /// <param name="scoreType">The score type to be evaluated</param>
        /// <returns>The insight's current score</returns>
        double Evaluate(InsightAnalysisContext context, InsightScoreType scoreType);
    }
}