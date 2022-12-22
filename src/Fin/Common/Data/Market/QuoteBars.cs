using System;

namespace QuantConnect.Data.Market
{
    /// <summary>
    /// Collection of <see cref="QuoteBar"/> keyed by symbol
    /// </summary>
    public class QuoteBars : DataDictionary<QuoteBar>
    {
        /// <summary>
        /// Creates a new instance of the <see cref="QuoteBars"/> dictionary
        /// </summary>
        public QuoteBars()
        {
        }

        /// <summary>
        /// Creates a new instance of the <see cref="QuoteBars"/> dictionary
        /// </summary>
        public QuoteBars(DateTime time)
            : base(time)
        {
        }

        /// <summary>
        /// Gets or sets the QuoteBar with the specified ticker.
        /// </summary>
        /// <returns>
        /// The QuoteBar with the specified ticker.
        /// </returns>
        /// <param name="ticker">The ticker of the element to get or set.</param>
        /// <remarks>Wraps the base implementation to enable indexing in python algorithms due to pythonnet limitations</remarks>
        public new QuoteBar this[string ticker] { get { return base[ticker]; } set { base[ticker] = value; } }

        /// <summary>
        /// Gets or sets the QuoteBar with the specified Symbol.
        /// </summary>
        /// <returns>
        /// The QuoteBar with the specified Symbol.
        /// </returns>
        /// <param name="symbol">The Symbol of the element to get or set.</param>
        /// <remarks>Wraps the base implementation to enable indexing in python algorithms due to pythonnet limitations</remarks>
        public new QuoteBar this[Symbol symbol] { get { return base[symbol]; } set { base[symbol] = value; } }
    }
}