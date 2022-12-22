using System;
using System.Collections.Generic;

namespace QuantConnect.Interfaces
{
    /// <summary>
    /// Provides the full option chain for a given underlying.
    /// </summary>
    public interface IOptionChainProvider
    {
        /// <summary>
        /// Gets the list of option contracts for a given underlying symbol
        /// </summary>
        /// <param name="symbol">The underlying symbol</param>
        /// <param name="date">The date for which to request the option chain (only used in backtesting)</param>
        /// <returns>The list of option contracts</returns>
        IEnumerable<Symbol> GetOptionContractList(Symbol symbol, DateTime date);
    }
}
