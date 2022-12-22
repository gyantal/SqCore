using System;
using QuantConnect.Interfaces;
using System.Collections.Generic;

namespace QuantConnect.Lean.Engine.DataFeeds
{
    /// <summary>
    /// An implementation of <see cref="IFutureChainProvider"/> that reads the list of contracts from open interest zip data files
    /// </summary>
    public class BacktestingFutureChainProvider : BacktestingChainProvider, IFutureChainProvider
    {
        /// <summary>
        /// Creates a new instance
        /// </summary>
        /// <param name="dataCacheProvider">The data cache provider instance to use</param>
        public BacktestingFutureChainProvider(IDataCacheProvider dataCacheProvider)
            : base(dataCacheProvider)
        {
        }

        /// <summary>
        /// Gets the list of future contracts for a given underlying symbol
        /// </summary>
        /// <param name="symbol">The underlying symbol</param>
        /// <param name="date">The date for which to request the future chain (only used in backtesting)</param>
        /// <returns>The list of future contracts</returns>
        public virtual IEnumerable<Symbol> GetFutureContractList(Symbol symbol, DateTime date)
        {
            return GetSymbols(GetSymbol(symbol), date);
        }

        /// <summary>
        /// Helper method to get the symbol to use
        /// </summary>
        protected static Symbol GetSymbol(Symbol symbol)
        {
            if (symbol.SecurityType != SecurityType.Future)
            {
                if (symbol.SecurityType == SecurityType.FutureOption && symbol.Underlying != null)
                {
                    // be user friendly and take the underlying
                    symbol = symbol.Underlying;
                }
                else
                {
                    throw new NotSupportedException($"BacktestingFutureChainProvider.GetFutureContractList():" +
                        $" {nameof(SecurityType.Future)} or {nameof(SecurityType.FutureOption)} is expected but was {symbol.SecurityType}");
                }
            }

            return symbol.Canonical;
        }
    }
}
