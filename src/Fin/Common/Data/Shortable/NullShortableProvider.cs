using System;
using System.Collections.Generic;
using QuantConnect.Interfaces;

namespace QuantConnect.Data.Shortable
{
    /// <summary>
    /// Defines the default shortable provider in the case that no local data exists.
    /// This will allow for all assets to be infinitely shortable, with no restrictions.
    /// </summary>
    public class NullShortableProvider : IShortableProvider
    {
        /// <summary>
        /// Gets all shortable Symbols
        /// </summary>
        /// <param name="localTime">Time of the algorithm</param>
        /// <returns>null indicating that all Symbols are shortable</returns>
        public Dictionary<Symbol, long> AllShortableSymbols(DateTime localTime)
        {
            return null;
        }

        /// <summary>
        /// Gets the quantity shortable for the Symbol at the given time.
        /// </summary>
        /// <param name="symbol">Symbol to check</param>
        /// <param name="localTime">Local time of the algorithm</param>
        /// <returns>null, indicating that it is infinitely shortable</returns>
        public long? ShortableQuantity(Symbol symbol, DateTime localTime)
        {
            return null;
        }
    }
}
