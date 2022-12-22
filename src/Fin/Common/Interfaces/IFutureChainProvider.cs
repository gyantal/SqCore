using System;
using System.Collections.Generic;

namespace QuantConnect.Interfaces
{
    /// <summary>
    /// Provides the full future chain for a given underlying.
    /// </summary>
    public interface IFutureChainProvider
    {
        /// <summary>
        /// Gets the list of future contracts for a given underlying symbol
        /// </summary>
        /// <param name="symbol">The underlying symbol</param>
        /// <param name="date">The date for which to request the future chain (only used in backtesting)</param>
        /// <returns>The list of future contracts</returns>
        IEnumerable<Symbol> GetFutureContractList(Symbol symbol, DateTime date);
    }
}
