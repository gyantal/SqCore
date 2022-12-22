using System;
using System.Collections.Generic;
using QuantConnect.Data.UniverseSelection;
using QuantConnect.Interfaces;

namespace QuantConnect.Algorithm.Framework.Selection
{
    /// <summary>
    /// Provides a base class for universe selection models.
    /// </summary>
    public class UniverseSelectionModel : IUniverseSelectionModel
    {
        /// <summary>
        /// Gets the next time the framework should invoke the `CreateUniverses` method to refresh the set of universes.
        /// </summary>
        public virtual DateTime GetNextRefreshTimeUtc()
        {
            return DateTime.MaxValue;
        }

        /// <summary>
        /// Creates the universes for this algorithm. Called once after <see cref="IAlgorithm.Initialize"/>
        /// </summary>
        /// <param name="algorithm">The algorithm instance to create universes for</param>
        /// <returns>The universes to be used by the algorithm</returns>
        public virtual IEnumerable<Universe> CreateUniverses(QCAlgorithm algorithm)
        {
            throw new System.NotImplementedException("Types deriving from 'UniverseSelectionModel' must implement the 'IEnumerable<Universe> CreateUniverses(QCAlgorithm) method.");
        }
    }
}