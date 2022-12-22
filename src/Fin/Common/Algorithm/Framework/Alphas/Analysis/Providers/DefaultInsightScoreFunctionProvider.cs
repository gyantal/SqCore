using QuantConnect.Algorithm.Framework.Alphas.Analysis.Functions;

namespace QuantConnect.Algorithm.Framework.Alphas.Analysis.Providers
{
    /// <summary>
    /// Default implementation of <see cref="IInsightScoreFunctionProvider"/> always returns the <see cref="BinaryInsightScoreFunction"/>
    /// </summary>
    public class DefaultInsightScoreFunctionProvider : IInsightScoreFunctionProvider
    {
        private static readonly BinaryInsightScoreFunction Function = new BinaryInsightScoreFunction();

        /// <inheritdoc />
        public IInsightScoreFunction GetScoreFunction(InsightType insightType, InsightScoreType scoreType)
        {
            return Function;
        }
    }
}
