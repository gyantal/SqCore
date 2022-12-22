using System.Collections.Generic;
using System.Linq;
using QuantConnect.Algorithm.Framework.Alphas;

namespace QuantConnect.Algorithm.Framework.Portfolio
{
    /// <summary>
    /// Provides an implementation of <see cref="IPortfolioConstructionModel"/> that does nothing
    /// </summary>
    public class NullPortfolioConstructionModel : PortfolioConstructionModel
    {
        /// <summary>
        /// Create Targets; Does nothing in this implementation and returns an empty IEnumerable
        /// </summary>
        /// <returns>Empty IEnumerable of <see cref="IPortfolioTarget"/>s</returns>
        public override IEnumerable<IPortfolioTarget> CreateTargets(QCAlgorithm algorithm, Insight[] insights)
        {
            return Enumerable.Empty<IPortfolioTarget>();
        }
    }
}