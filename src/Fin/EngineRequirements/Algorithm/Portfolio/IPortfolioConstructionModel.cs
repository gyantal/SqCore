using System.Collections.Generic;
using QuantConnect.Algorithm.Framework.Alphas;

namespace QuantConnect.Algorithm.Framework.Portfolio
{
    /// <summary>
    /// Algorithm framework model that
    /// </summary>
    public interface IPortfolioConstructionModel : INotifiedSecurityChanges
    {
        /// <summary>
        /// Create portfolio targets from the specified insights
        /// </summary>
        /// <param name="algorithm">The algorithm instance</param>
        /// <param name="insights">The insights to create portfolio targets from</param>
        /// <returns>An enumerable of portfolio targets to be sent to the execution model</returns>
        IEnumerable<IPortfolioTarget> CreateTargets(QCAlgorithm algorithm, Insight[] insights);
    }
}
