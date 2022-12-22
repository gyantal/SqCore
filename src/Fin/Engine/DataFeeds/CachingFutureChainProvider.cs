using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using QuantConnect.Interfaces;

namespace QuantConnect.Lean.Engine.DataFeeds
{
    /// <summary>
    /// An implementation of <see cref="IFutureChainProvider"/> that will cache by date future contracts returned by another future chain provider.
    /// </summary>
    public class CachingFutureChainProvider : IFutureChainProvider
    {
        private readonly ConcurrentDictionary<Symbol, FutureChainCacheEntry> _cache = new ConcurrentDictionary<Symbol, FutureChainCacheEntry>();
        private readonly IFutureChainProvider _futureChainProvider;

        /// <summary>
        /// Initializes a new instance of the <see cref="CachingFutureChainProvider"/> class
        /// </summary>
        /// <param name="futureChainProvider"></param>
        public CachingFutureChainProvider(IFutureChainProvider futureChainProvider)
        {
            _futureChainProvider = futureChainProvider;
        }

        /// <summary>
        /// Gets the list of future contracts for a given underlying symbol
        /// </summary>
        /// <param name="symbol">The underlying symbol</param>
        /// <param name="date">The date for which to request the future chain (only used in backtesting)</param>
        /// <returns>The list of future contracts</returns>
        public IEnumerable<Symbol> GetFutureContractList(Symbol symbol, DateTime date)
        {
            List<Symbol> symbols;

            FutureChainCacheEntry entry;
            if (!_cache.TryGetValue(symbol, out entry) || date.Date != entry.Date)
            {
                symbols = _futureChainProvider.GetFutureContractList(symbol, date.Date).ToList();
                _cache[symbol] = new FutureChainCacheEntry(date.Date, symbols);
            }
            else
            {
                symbols = entry.Symbols;
            }

            return symbols;
        }

        private class FutureChainCacheEntry
        {
            public DateTime Date { get; }
            public List<Symbol> Symbols { get; }

            public FutureChainCacheEntry(DateTime date, List<Symbol> symbols)
            {
                Date = date;
                Symbols = symbols;
            }
        }
    }
}