using System;
using System.Collections.Generic;
using QuantConnect.Data.UniverseSelection;
using QuantConnect.Interfaces;

namespace QuantConnect.Algorithm.Framework.Selection
{
    /// <summary>
    /// Algorithm framework model that defines the universes to be used by an algorithm
    /// </summary>
    public interface IUniverseSelectionModel
    {
        /// <summary>
        /// Gets the next time the framework should invoke the `CreateUniverses` method to refresh the set of universes.
        /// </summary>
        DateTime GetNextRefreshTimeUtc();

        /// <summary>
        /// Creates the universes for this algorithm. Called once after <see cref="IAlgorithm.Initialize"/>
        /// </summary>
        /// <param name="algorithm">The algorithm instance to create universes for</param>
        /// <returns>The universes to be used by the algorithm</returns>
        IEnumerable<Universe> CreateUniverses(QCAlgorithm algorithm);
    }
}