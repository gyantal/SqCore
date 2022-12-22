using System;

namespace QuantConnect.Data.Market
{
    /// <summary>
    /// Collection of <see cref="SymbolChangedEvent"/> keyed by the original, requested symbol
    /// </summary>
    public class SymbolChangedEvents : DataDictionary<SymbolChangedEvent>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SymbolChangedEvent"/> dictionary
        /// </summary>
        public SymbolChangedEvents()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SymbolChangedEvent"/> dictionary
        /// </summary>
        /// <param name="frontier">The time associated with the data in this dictionary</param>
        public SymbolChangedEvents(DateTime frontier)
            : base(frontier)
        {
        }

        /// <summary>
        /// Gets or sets the SymbolChangedEvent with the specified ticker.
        /// </summary>
        /// <returns>
        /// The SymbolChangedEvent with the specified ticker.
        /// </returns>
        /// <param name="ticker">The ticker of the element to get or set.</param>
        /// <remarks>Wraps the base implementation to enable indexing in python algorithms due to pythonnet limitations</remarks>
        public new SymbolChangedEvent this[string ticker] { get { return base[ticker]; } set { base[ticker] = value; } }

        /// <summary>
        /// Gets or sets the SymbolChangedEvent with the specified Symbol.
        /// </summary>
        /// <returns>
        /// The SymbolChangedEvent with the specified Symbol.
        /// </returns>
        /// <param name="symbol">The Symbol of the element to get or set.</param>
        /// <remarks>Wraps the base implementation to enable indexing in python algorithms due to pythonnet limitations</remarks>
        public new SymbolChangedEvent this[Symbol symbol] { get { return base[symbol]; } set { base[symbol] = value; } }
    }
}
