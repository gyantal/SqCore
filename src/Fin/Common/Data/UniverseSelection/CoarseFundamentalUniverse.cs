﻿using System;
using System.Collections.Generic;
using System.Linq;
using Python.Runtime;
using QuantConnect.Securities;

namespace QuantConnect.Data.UniverseSelection
{
    /// <summary>
    /// Defines a universe that reads coarse us equity data
    /// </summary>
    public class CoarseFundamentalUniverse : Universe
    {
        private readonly UniverseSettings _universeSettings;
        private readonly Func<IEnumerable<CoarseFundamental>, IEnumerable<Symbol>> _selector;

        /// <summary>
        /// Gets the settings used for subscriptons added for this universe
        /// </summary>
        public override UniverseSettings UniverseSettings => _universeSettings;

        /// <summary>
        /// Initializes a new instance of the <see cref="CoarseFundamentalUniverse"/> class
        /// </summary>
        /// <param name="universeSettings">The settings used for new subscriptions generated by this universe</param>
        /// <param name="selector">Returns the symbols that should be included in the universe</param>
        public CoarseFundamentalUniverse(UniverseSettings universeSettings, Func<IEnumerable<CoarseFundamental>, IEnumerable<Symbol>> selector)
            : base(CreateConfiguration(CoarseFundamental.CreateUniverseSymbol(QuantConnect.Market.USA)))
        {
            _universeSettings = universeSettings;
            _selector = selector;
        }
        
        /// <summary>
        /// Initializes a new instance of the <see cref="CoarseFundamentalUniverse"/> class
        /// </summary>
        /// <param name="universeSettings">The settings used for new subscriptions generated by this universe</param>
        /// <param name="selector">Returns the symbols that should be included in the universe</param>
        public CoarseFundamentalUniverse(UniverseSettings universeSettings, PyObject selector)
            : this(CoarseFundamental.CreateUniverseSymbol(QuantConnect.Market.USA), universeSettings, selector)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CoarseFundamentalUniverse"/> class
        /// </summary>
        /// <param name="symbol">Defines the symbol to use for this universe</param>
        /// <param name="universeSettings">The settings used for new subscriptions generated by this universe</param>
        /// <param name="selector">Returns the symbols that should be included in the universe</param>
        public CoarseFundamentalUniverse(Symbol symbol, UniverseSettings universeSettings, Func<IEnumerable<CoarseFundamental>, IEnumerable<Symbol>> selector)
            : base(CreateConfiguration(symbol))
        {
            _universeSettings = universeSettings;
            _selector = selector;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CoarseFundamentalUniverse"/> class
        /// </summary>
        /// <param name="symbol">Defines the symbol to use for this universe</param>
        /// <param name="universeSettings">The settings used for new subscriptions generated by this universe</param>
        /// <param name="selector">Returns the symbols that should be included in the universe</param>
        public CoarseFundamentalUniverse(Symbol symbol, UniverseSettings universeSettings, PyObject selector)
            : base(CreateConfiguration(symbol))
        {
            _universeSettings = universeSettings;
            Func<IEnumerable<CoarseFundamental>, object> func;
            if (selector.TryConvertToDelegate(out func))
            {
                _selector = func.ConvertToUniverseSelectionSymbolDelegate();
            }
        }

        /// <summary>
        /// Performs universe selection using the data specified
        /// </summary>
        /// <param name="utcTime">The current utc time</param>
        /// <param name="data">The symbols to remain in the universe</param>
        /// <returns>The data that passes the filter</returns>
        public override IEnumerable<Symbol> SelectSymbols(DateTime utcTime, BaseDataCollection data)
        {
            return _selector(data.Data.OfType<CoarseFundamental>());
        }

        /// <summary>
        /// Creates a <see cref="CoarseFundamental"/> subscription configuration for the US-equity market
        /// </summary>
        /// <param name="symbol">The symbol used in the returned configuration</param>
        /// <returns>A coarse fundamental subscription configuration with the specified symbol</returns>
        public static SubscriptionDataConfig CreateConfiguration(Symbol symbol)
        {
            return new SubscriptionDataConfig(typeof (CoarseFundamental),
                symbol: symbol,
                resolution: Resolution.Daily,
                dataTimeZone: TimeZones.NewYork,
                exchangeTimeZone: TimeZones.NewYork,
                fillForward: false,
                extendedHours: false,
                isInternalFeed: true,
                isCustom: false,
                tickType: null,
                isFilteredSubscription: false
                );
        }
    }
}