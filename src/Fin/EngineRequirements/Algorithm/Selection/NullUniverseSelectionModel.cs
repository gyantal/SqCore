using System.Collections.Generic;
using QuantConnect.Data.UniverseSelection;

namespace QuantConnect.Algorithm.Framework.Selection
{
    /// <summary>
    /// Provides a null implementation of <see cref="IUniverseSelectionModel"/>
    /// </summary>
    public class NullUniverseSelectionModel : UniverseSelectionModel
    {
        /// <summary>
        /// Creates the universes for this algorithm.
        /// Called at algorithm start.
        /// </summary>
        /// <returns>The universes defined by this model</returns>
        public override IEnumerable<Universe> CreateUniverses(QCAlgorithm algorithm)
        {
            yield break;
        }
    }
}
