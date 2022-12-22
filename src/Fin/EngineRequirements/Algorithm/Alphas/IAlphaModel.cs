using System.Collections.Generic;
using QuantConnect.Data;

namespace QuantConnect.Algorithm.Framework.Alphas
{
    /// <summary>
    /// Algorithm framework model that produces insights
    /// </summary>
    public interface IAlphaModel : INotifiedSecurityChanges
    {
        /// <summary>
        /// Updates this alpha model with the latest data from the algorithm.
        /// This is called each time the algorithm receives data for subscribed securities
        /// </summary>
        /// <param name="algorithm">The algorithm instance</param>
        /// <param name="data">The new data available</param>
        /// <returns>The new insights generated</returns>
        IEnumerable<Insight> Update(QCAlgorithm algorithm, Slice data);
    }
}