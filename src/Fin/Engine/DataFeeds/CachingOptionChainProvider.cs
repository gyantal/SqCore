﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using QuantConnect.Interfaces;

namespace QuantConnect.Lean.Engine.DataFeeds
{
    /// <summary>
    /// An implementation of <see cref="IOptionChainProvider"/> that will cache by date option contracts returned by another option chain provider.
    /// </summary>
    public class CachingOptionChainProvider : IOptionChainProvider
    {
        private readonly ConcurrentDictionary<Symbol, OptionChainCacheEntry> _cache = new ConcurrentDictionary<Symbol, OptionChainCacheEntry>();
        private readonly IOptionChainProvider _optionChainProvider;

        /// <summary>
        /// Initializes a new instance of the <see cref="CachingOptionChainProvider"/> class
        /// </summary>
        /// <param name="optionChainProvider"></param>
        public CachingOptionChainProvider(IOptionChainProvider optionChainProvider)
        {
            _optionChainProvider = optionChainProvider;
        }

        /// <summary>
        /// Gets the list of option contracts for a given underlying symbol
        /// </summary>
        /// <param name="symbol">The underlying symbol</param>
        /// <param name="date">The date for which to request the option chain (only used in backtesting)</param>
        /// <returns>The list of option contracts</returns>
        public IEnumerable<Symbol> GetOptionContractList(Symbol symbol, DateTime date)
        {
            List<Symbol> symbols;

            OptionChainCacheEntry entry;
            if (!_cache.TryGetValue(symbol, out entry) || date.Date != entry.Date)
            {
                symbols = _optionChainProvider.GetOptionContractList(symbol, date.Date).ToList();
                _cache[symbol] = new OptionChainCacheEntry(date.Date, symbols);
            }
            else
            {
                symbols = entry.Symbols;
            }

            return symbols;
        }

        private class OptionChainCacheEntry
        {
            public DateTime Date { get; }
            public List<Symbol> Symbols { get; }

            public OptionChainCacheEntry(DateTime date, List<Symbol> symbols)
            {
                Date = date;
                Symbols = symbols;
            }
        }
    }
}