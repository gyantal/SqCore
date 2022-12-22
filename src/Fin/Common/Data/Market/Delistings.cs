using System;

namespace QuantConnect.Data.Market
{
    /// <summary>
    /// Collections of <see cref="Delisting"/> keyed by <see cref="Symbol"/>
    /// </summary>
    public class Delistings : DataDictionary<Delisting>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Delistings"/> dictionary
        /// </summary>
        public Delistings()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Delistings"/> dictionary
        /// </summary>
        /// <param name="frontier">The time associated with the data in this dictionary</param>
        public Delistings(DateTime frontier)
            : base(frontier)
        {
        }

        /// <summary>
        /// Gets or sets the Delisting with the specified ticker.
        /// </summary>
        /// <returns>
        /// The Delisting with the specified ticker.
        /// </returns>
        /// <param name="ticker">The ticker of the element to get or set.</param>
        /// <remarks>Wraps the base implementation to enable indexing in python algorithms due to pythonnet limitations</remarks>
        public new Delisting this[string ticker] { get { return base[ticker]; } set { base[ticker] = value; } }

        /// <summary>
        /// Gets or sets the Delisting with the specified Symbol.
        /// </summary>
        /// <returns>
        /// The Delisting with the specified Symbol.
        /// </returns>
        /// <param name="symbol">The Symbol of the element to get or set.</param>
        /// <remarks>Wraps the base implementation to enable indexing in python algorithms due to pythonnet limitations</remarks>
        public new Delisting this[Symbol symbol] { get { return base[symbol]; } set { base[symbol] = value; } }
    }
}
