using System;
using System.Collections.Generic;
using System.Linq;
using QuantConnect.Interfaces;

namespace QuantConnect.Securities.Option
{
    /// <summary>
    /// An implementation of <see cref="IOptionChainProvider"/> that always returns an empty list of contracts
    /// </summary>
    public class EmptyOptionChainProvider : IOptionChainProvider
    {
        /// <summary>
        /// Gets the list of option contracts for a given underlying symbol
        /// </summary>
        /// <param name="symbol">The underlying symbol</param>
        /// <param name="date">The date for which to request the option chain (only used in backtesting)</param>
        /// <returns>The list of option contracts</returns>
        public IEnumerable<Symbol> GetOptionContractList(Symbol symbol, DateTime date)
        {
            return Enumerable.Empty<Symbol>();
        }
    }
}
