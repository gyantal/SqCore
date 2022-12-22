using System;
using System.Collections.Generic;

namespace QuantConnect.Data.Market
{
    /// <summary>
    /// Ticks collection which implements an IDictionary-string-list of ticks. This way users can iterate over the string indexed ticks of the requested symbol.
    /// </summary>
    /// <remarks>Ticks are timestamped to the nearest second in QuantConnect</remarks>
    public class Ticks : DataDictionary<List<Tick>>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Ticks"/> dictionary
        /// </summary>
        public Ticks()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Ticks"/> dictionary
        /// </summary>
        /// <param name="frontier">The time associated with the data in this dictionary</param>
        public Ticks(DateTime frontier)
            : base(frontier)
        {
        }

        /// <summary>
        /// Gets or sets the list of Tick with the specified ticker.
        /// </summary>
        /// <returns>
        /// The list of Tick with the specified ticker.
        /// </returns>
        /// <param name="ticker">The ticker of the element to get or set.</param>
        /// <remarks>Wraps the base implementation to enable indexing in python algorithms due to pythonnet limitations</remarks>
        public new List<Tick> this[string ticker] { get { return base[ticker]; } set { base[ticker] = value; } }

        /// <summary>
        /// Gets or sets the list of Tick with the specified Symbol.
        /// </summary>
        /// <returns>
        /// The list of Tick with the specified Symbol.
        /// </returns>
        /// <param name="symbol">The Symbol of the element to get or set.</param>
        /// <remarks>Wraps the base implementation to enable indexing in python algorithms due to pythonnet limitations</remarks>
        public new List<Tick> this[Symbol symbol] { get { return base[symbol]; } set { base[symbol] = value; } }
    }
}
