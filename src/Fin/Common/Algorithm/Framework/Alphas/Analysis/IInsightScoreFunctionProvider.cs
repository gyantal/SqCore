namespace QuantConnect.Algorithm.Framework.Alphas.Analysis
{
    /// <summary>
    /// Retrieves the registered scoring function for the specified insight/score type
    /// </summary>
    public interface IInsightScoreFunctionProvider
    {
        /// <summary>
        /// Gets the insight scoring function for the specified insight type and score type
        /// </summary>
        /// <param name="insightType">The insight's type</param>
        /// <param name="scoreType">The scoring type</param>
        /// <returns>A function to be used to compute insight scores</returns>
        IInsightScoreFunction GetScoreFunction(InsightType insightType, InsightScoreType scoreType);
    }
}