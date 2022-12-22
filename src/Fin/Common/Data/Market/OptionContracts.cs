using System;

namespace QuantConnect.Data.Market
{
    /// <summary>
    /// Collection of <see cref="OptionContract"/> keyed by option symbol
    /// </summary>
    public class OptionContracts : DataDictionary<OptionContract>
    {
        /// <summary>
        /// Creates a new instance of the <see cref="OptionContracts"/> dictionary
        /// </summary>
        public OptionContracts()
        {
        }

        /// <summary>
        /// Creates a new instance of the <see cref="OptionContracts"/> dictionary
        /// </summary>
        public OptionContracts(DateTime time)
            : base(time)
        {
        }

        /// <summary>
        /// Gets or sets the OptionContract with the specified ticker.
        /// </summary>
        /// <returns>
        /// The OptionContract with the specified ticker.
        /// </returns>
        /// <param name="ticker">The ticker of the element to get or set.</param>
        /// <remarks>Wraps the base implementation to enable indexing in python algorithms due to pythonnet limitations</remarks>
        public new OptionContract this[string ticker] { get { return base[ticker]; } set { base[ticker] = value; } }

        /// <summary>
        /// Gets or sets the OptionContract with the specified Symbol.
        /// </summary>
        /// <returns>
        /// The OptionContract with the specified Symbol.
        /// </returns>
        /// <param name="symbol">The Symbol of the element to get or set.</param>
        /// <remarks>Wraps the base implementation to enable indexing in python algorithms due to pythonnet limitations</remarks>
        public new OptionContract this[Symbol symbol] { get { return base[symbol]; } set { base[symbol] = value; } }
    }
}