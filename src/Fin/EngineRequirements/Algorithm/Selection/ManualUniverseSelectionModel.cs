﻿using System;
using System.Collections.Generic;
using System.Linq;
using QuantConnect.Data;
using QuantConnect.Data.Market;
using QuantConnect.Data.UniverseSelection;
using QuantConnect.Securities;

namespace QuantConnect.Algorithm.Framework.Selection
{
    /// <summary>
    /// Provides an implementation of <see cref="IUniverseSelectionModel"/> that simply
    /// subscribes to the specified set of symbols
    /// </summary>
    public class ManualUniverseSelectionModel : UniverseSelectionModel
    {
        private static readonly MarketHoursDatabase MarketHours = MarketHoursDatabase.FromDataFolder();

        private readonly IReadOnlyList<Symbol> _symbols;
        private readonly UniverseSettings _universeSettings;

        /// <summary>
        /// Initializes a new instance of the <see cref="ManualUniverseSelectionModel"/> class using the algorithm's
        /// security initializer and universe settings
        /// </summary>
        public ManualUniverseSelectionModel()
            : this(Enumerable.Empty<Symbol>())
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ManualUniverseSelectionModel"/> class using the algorithm's
        /// security initializer and universe settings
        /// </summary>
        /// <param name="symbols">The symbols to subscribe to.
        /// Should not send in symbols at <see cref="QCAlgorithm.Securities"/> since those will be managed by the <see cref="UserDefinedUniverse"/></param>
        public ManualUniverseSelectionModel(IEnumerable<Symbol> symbols)
            : this(symbols.ToArray())
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ManualUniverseSelectionModel"/> class using the algorithm's
        /// security initializer and universe settings
        /// </summary>
        /// <param name="symbols">The symbols to subscribe to
        /// Should not send in symbols at <see cref="QCAlgorithm.Securities"/> since those will be managed by the <see cref="UserDefinedUniverse"/></param>
        public ManualUniverseSelectionModel(params Symbol[] symbols)
            : this (symbols?.AsEnumerable(), null)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ManualUniverseSelectionModel"/> class
        /// </summary>
        /// <param name="symbols">The symbols to subscribe to
        /// Should not send in symbols at <see cref="QCAlgorithm.Securities"/> since those will be managed by the <see cref="UserDefinedUniverse"/></param>
        /// <param name="universeSettings">The settings used when adding symbols to the algorithm, specify null to use algorithm.UniverseSettings</param>
        public ManualUniverseSelectionModel(IEnumerable<Symbol> symbols, UniverseSettings universeSettings)
        {
            if (symbols == null)
            {
                throw new ArgumentNullException(nameof(symbols));
            }

            _symbols = symbols.Where(s => !s.IsCanonical()).ToList();
            _universeSettings = universeSettings;

            foreach (var symbol in _symbols)
            {
                SymbolCache.Set(symbol.Value, symbol);
            }
        }

        /// <summary>
        /// Creates the universes for this algorithm.
        /// Called at algorithm start.
        /// </summary>
        /// <returns>The universes defined by this model</returns>
        public override IEnumerable<Universe> CreateUniverses(QCAlgorithm algorithm)
        {
            var universeSettings = _universeSettings ?? algorithm.UniverseSettings;
            var resolution = universeSettings.Resolution;
            var type = resolution == Resolution.Tick ? typeof(Tick) : typeof(TradeBar);

            // universe per security type/market
            foreach (var grp in _symbols.GroupBy(s => new { s.ID.Market, s.SecurityType }))
            {
                MarketHoursDatabase.Entry entry;

                var market = grp.Key.Market;
                var securityType = grp.Key.SecurityType;
                var hashCode = 1;
                foreach (var symbol in grp)
                {
                    hashCode = hashCode * 31 + symbol.GetHashCode();
                }
                var universeSymbol = Symbol.Create($"manual-universe-selection-model-{securityType}-{market}-{hashCode}", securityType, market);
                if (securityType == SecurityType.Base)
                {
                    // add an entry for this custom universe symbol -- we don't really know the time zone for sure,
                    // but we set it to TimeZones.NewYork in AddData, also, since this is a manual universe, the time
                    // zone doesn't actually matter since this universe specifically doesn't do anything with data.
                    var symbolString = MarketHoursDatabase.GetDatabaseSymbolKey(universeSymbol);
                    var alwaysOpen = SecurityExchangeHours.AlwaysOpen(TimeZones.NewYork);
                    entry = MarketHours.SetEntry(market, symbolString, securityType, alwaysOpen, TimeZones.NewYork);
                }
                else
                {
                    entry = MarketHours.GetEntry(market, (string) null, securityType);
                }

                var config = new SubscriptionDataConfig(type, universeSymbol, resolution, entry.DataTimeZone, entry.ExchangeHours.TimeZone, false, false, true);
                yield return new ManualUniverse(config, universeSettings, grp);
            }
        }
    }
}
