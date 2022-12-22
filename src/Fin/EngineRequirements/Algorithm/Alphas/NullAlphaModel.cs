using System.Collections.Generic;
using System.Linq;
using QuantConnect.Data;

namespace QuantConnect.Algorithm.Framework.Alphas
{
    /// <summary>
    /// Provides a null implementation of an alpha model
    /// </summary>
    public class NullAlphaModel : AlphaModel
    {
        /// <summary>
        /// Updates this alpha model with the latest data from the algorithm.
        /// This is called each time the algorithm receives data for subscribed securities
        /// </summary>
        /// <param name="algorithm">The algorithm instance</param>
        /// <param name="data">The new data available</param>
        /// <returns>The new insights generated</returns>
        public override IEnumerable<Insight> Update(QCAlgorithm algorithm, Slice data)
        {
            return Enumerable.Empty<Insight>();
        }
    }
}