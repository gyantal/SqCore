using System;

namespace QuantConnect.Data.Market
{
    /// <summary>
    /// Collection of splits keyed by <see cref="Symbol"/>
    /// </summary>
    public class Splits : DataDictionary<Split>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Splits"/> dictionary
        /// </summary>
        public Splits()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Splits"/> dictionary
        /// </summary>
        /// <param name="frontier">The time associated with the data in this dictionary</param>
        public Splits(DateTime frontier)
            : base(frontier)
        {
        }

        /// <summary>
        /// Gets or sets the Split with the specified ticker.
        /// </summary>
        /// <returns>
        /// The Split with the specified ticker.
        /// </returns>
        /// <param name="ticker">The ticker of the element to get or set.</param>
        /// <remarks>Wraps the base implementation to enable indexing in python algorithms due to pythonnet limitations</remarks>
        public new Split this[string ticker] { get { return base[ticker]; } set { base[ticker] = value; } }

        /// <summary>
        /// Gets or sets the Split with the specified Symbol.
        /// </summary>
        /// <returns>
        /// The Split with the specified Symbol.
        /// </returns>
        /// <param name="symbol">The Symbol of the element to get or set.</param>
        /// <remarks>Wraps the base implementation to enable indexing in python algorithms due to pythonnet limitations</remarks>
        public new Split this[Symbol symbol] { get { return base[symbol]; } set { base[symbol] = value; } }
    }
}