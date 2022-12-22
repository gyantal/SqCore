using System;
using System.Collections.Generic;
using System.Linq;

namespace QuantConnect.Algorithm.Framework.Alphas
{
    /// <summary>
    /// Defines a collection of insights that were generated at the same time step
    /// </summary>
    public class GeneratedInsightsCollection
    {
        /// <summary>
        /// The utc date time the insights were generated
        /// </summary>
        public DateTime DateTimeUtc { get; }

        /// <summary>
        /// The generated insights
        /// </summary>
        public List<Insight> Insights { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="GeneratedInsightsCollection"/> class
        /// </summary>
        /// <param name="dateTimeUtc">The utc date time the sinals were generated</param>
        /// <param name="insights">The generated insights</param>
        /// <param name="clone">Keep a clone of the generated insights</param>
        public GeneratedInsightsCollection(DateTime dateTimeUtc,
            IEnumerable<Insight> insights,
            bool clone = true)
        {
            DateTimeUtc = dateTimeUtc;

            // for performance only call 'ToArray' if not empty enumerable (which is static)
            Insights = insights == Enumerable.Empty<Insight>()
                ? new List<Insight>() : insights.Select(insight => clone ? insight.Clone() : insight).ToList();
        }
    }
}
