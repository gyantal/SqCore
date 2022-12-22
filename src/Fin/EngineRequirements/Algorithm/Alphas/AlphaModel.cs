using System;
using System.Collections.Generic;
using QuantConnect.Data;
using QuantConnect.Data.UniverseSelection;

namespace QuantConnect.Algorithm.Framework.Alphas
{
    /// <summary>
    /// Provides a base class for alpha models.
    /// </summary>
    public class AlphaModel : IAlphaModel, INamedModel
    {
        /// <summary>
        /// Defines a name for a framework model
        /// </summary>
        public virtual string Name { get; set; }

        /// <summary>
        /// Initialize new <see cref="AlphaModel"/>
        /// </summary>
        public AlphaModel()
        {
            Name = Guid.NewGuid().ToString();
        }

        /// <summary>
        /// Updates this alpha model with the latest data from the algorithm.
        /// This is called each time the algorithm receives data for subscribed securities
        /// </summary>
        /// <param name="algorithm">The algorithm instance</param>
        /// <param name="data">The new data available</param>
        /// <returns>The new insights generated</returns>
        public virtual IEnumerable<Insight> Update(QCAlgorithm algorithm, Slice data)
        {
            throw new System.NotImplementedException("Types deriving from 'AlphaModel' must implement the 'IEnumerable<Insight> Update(QCAlgorithm, Slice) method.");
        }

        /// <summary>
        /// Event fired each time the we add/remove securities from the data feed
        /// </summary>
        /// <param name="algorithm">The algorithm instance that experienced the change in securities</param>
        /// <param name="changes">The security additions and removals from the algorithm</param>
        public virtual void OnSecuritiesChanged(QCAlgorithm algorithm, SecurityChanges changes)
        {
        }
    }
}