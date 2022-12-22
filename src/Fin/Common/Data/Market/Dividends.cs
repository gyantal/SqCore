using System;

namespace QuantConnect.Data.Market
{
    /// <summary>
    /// Collection of dividends keyed by <see cref="Symbol"/>
    /// </summary>
    public class Dividends : DataDictionary<Dividend>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Dividends"/> dictionary
        /// </summary>
        public Dividends()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Dividends"/> dictionary
        /// </summary>
        /// <param name="frontier">The time associated with the data in this dictionary</param>
        public Dividends(DateTime frontier)
            : base(frontier)
        {
        }

        /// <summary>
        /// Gets or sets the Dividend with the specified ticker.
        /// </summary>
        /// <returns>
        /// The Dividend with the specified ticker.
        /// </returns>
        /// <param name="ticker">The ticker of the element to get or set.</param>
        /// <remarks>Wraps the base implementation to enable indexing in python algorithms due to pythonnet limitations</remarks>
        public new Dividend this[string ticker] { get { return base[ticker]; } set { base[ticker] = value; } }

        /// <summary>
        /// Gets or sets the Dividend with the specified Symbol.
        /// </summary>
        /// <returns>
        /// The Dividend with the specified Symbol.
        /// </returns>
        /// <param name="symbol">The Symbol of the element to get or set.</param>
        /// <remarks>Wraps the base implementation to enable indexing in python algorithms due to pythonnet limitations</remarks>
        public new Dividend this[Symbol symbol] { get { return base[symbol]; } set { base[symbol] = value; } }
    }
}
