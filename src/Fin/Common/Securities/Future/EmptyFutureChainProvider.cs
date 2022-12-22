using System;
using System.Collections.Generic;
using System.Linq;
using QuantConnect.Interfaces;

namespace QuantConnect.Securities.Future
{
    /// <summary>
    /// An implementation of <see cref="IFutureChainProvider"/> that always returns an empty list of contracts
    /// </summary>
    public class EmptyFutureChainProvider : IFutureChainProvider
    {
        /// <summary>
        /// Gets the list of future contracts for a given underlying symbol
        /// </summary>
        /// <param name="symbol">The underlying symbol</param>
        /// <param name="date">The date for which to request the future chain (only used in backtesting)</param>
        /// <returns>The list of future contracts</returns>
        public IEnumerable<Symbol> GetFutureContractList(Symbol symbol, DateTime date)
        {
            return Enumerable.Empty<Symbol>();
        }
    }
}
