using System;

namespace QuantConnect.Algorithm.Framework.Alphas.Analysis.Functions
{
    /// <summary>
    /// Defines a scoring function that always returns 1 or 0.
    /// You're either right or you're wrong with this one :)
    /// </summary>
    public class BinaryInsightScoreFunction : IInsightScoreFunction
    {
        /// <inheritdoc />
        public double Evaluate(InsightAnalysisContext context, InsightScoreType scoreType)
        {
            var insight = context.Insight;

            var startingValue = context.InitialValues.Get(insight.Type);
            var currentValue = context.CurrentValues.Get(insight.Type);

            switch (insight.Direction)
            {
                case InsightDirection.Down:
                    return currentValue < startingValue ? 1 : 0;

                case InsightDirection.Flat:
                    // can't really do percent changes with zero
                    if (startingValue == 0) return currentValue == startingValue ? 1 : 0;

                    // TODO : Re-evaluate flat predictions, potentially adding Insight.Tolerance to say 'how flat'
                    var deltaPercent = Math.Abs(currentValue - startingValue)/startingValue;
                    if (insight.Magnitude.HasValue)
                    {
                        return Math.Abs(deltaPercent) < Math.Abs(insight.Magnitude.Value).SafeDecimalCast() ? 1 : 0;
                    }

                    // this is pretty much impossible, I suppose unless the ticks are large and/or volumes are small
                    return currentValue == startingValue ? 1 : 0;

                case InsightDirection.Up:
                    return currentValue > startingValue ? 1 : 0;

                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }
}