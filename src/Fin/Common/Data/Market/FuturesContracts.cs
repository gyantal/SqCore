using System;

namespace QuantConnect.Data.Market
{
    /// <summary>
    /// Collection of <see cref="FuturesContract"/> keyed by futures symbol
    /// </summary>
    public class FuturesContracts : DataDictionary<FuturesContract>
    {
        /// <summary>
        /// Creates a new instance of the <see cref="FuturesContracts"/> dictionary
        /// </summary>
        public FuturesContracts()
        {
        }

        /// <summary>
        /// Creates a new instance of the <see cref="FuturesContracts"/> dictionary
        /// </summary>
        public FuturesContracts(DateTime time)
            : base(time)
        {
        }

        /// <summary>
        /// Gets or sets the FuturesContract with the specified ticker.
        /// </summary>
        /// <returns>
        /// The FuturesContract with the specified ticker.
        /// </returns>
        /// <param name="ticker">The ticker of the element to get or set.</param>
        /// <remarks>Wraps the base implementation to enable indexing in python algorithms due to pythonnet limitations</remarks>
        public new FuturesContract this[string ticker] { get { return base[ticker]; } set { base[ticker] = value; } }

        /// <summary>
        /// Gets or sets the FuturesContract with the specified Symbol.
        /// </summary>
        /// <returns>
        /// The FuturesContract with the specified Symbol.
        /// </returns>
        /// <param name="symbol">The Symbol of the element to get or set.</param>
        /// <remarks>Wraps the base implementation to enable indexing in python algorithms due to pythonnet limitations</remarks>
        public new FuturesContract this[Symbol symbol] { get { return base[symbol]; } set { base[symbol] = value; } }
    }
}