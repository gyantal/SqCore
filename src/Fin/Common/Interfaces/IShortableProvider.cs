using System;
using System.Collections.Generic;

namespace QuantConnect.Interfaces
{
    /// <summary>
    /// Defines a short list/easy-to-borrow provider
    /// </summary>
    public interface IShortableProvider
    {
        /// <summary>
        /// Gets all shortable Symbols at the given time
        /// </summary>
        /// <param name="localTime">Local time of the algorithm</param>
        /// <returns>All shortable Symbols including the quantity shortable as a positive number at the given time. Null if all Symbols are shortable without restrictions.</returns>
        Dictionary<Symbol, long> AllShortableSymbols(DateTime localTime);

        /// <summary>
        /// Gets the quantity shortable for a <see cref="Symbol"/>.
        /// </summary>
        /// <param name="symbol">Symbol to check shortable quantity</param>
        /// <param name="localTime">Local time of the algorithm</param>
        /// <returns>The quantity shortable for the given Symbol as a positive number. Null if the Symbol is shortable without restrictions.</returns>
        long? ShortableQuantity(Symbol symbol, DateTime localTime);
    }
}
