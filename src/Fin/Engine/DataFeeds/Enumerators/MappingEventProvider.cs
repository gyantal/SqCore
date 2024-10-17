using System;
using QuantConnect.Data;
using QuantConnect.Interfaces;
using QuantConnect.Data.Market;
using System.Collections.Generic;
using QuantConnect.Data.Auxiliary;
using QuantConnect.Parameters;

namespace QuantConnect.Lean.Engine.DataFeeds.Enumerators
{
    /// <summary>
    /// Event provider who will emit <see cref="SymbolChangedEvent"/> events
    /// </summary>
    public class MappingEventProvider : ITradableDateEventProvider
    {
        private IMapFileProvider _mapFileProvider;

        /// <summary>
        /// The associated configuration
        /// </summary>
        protected SubscriptionDataConfig Config { get; private set; }

        /// <summary>
        /// The current instance being used
        /// </summary>
        protected MapFile MapFile { get; private set; }

        /// <summary>
        /// Initializes this instance
        /// </summary>
        /// <param name="config">The <see cref="SubscriptionDataConfig"/></param>
        /// <param name="factorFileProvider">The factor file provider to use</param>
        /// <param name="mapFileProvider">The <see cref="Data.Auxiliary.MapFile"/> provider to use</param>
        /// <param name="startTime">Start date for the data request</param>
        public virtual void Initialize(
            SubscriptionDataConfig config,
            IFactorFileProvider factorFileProvider,
            IMapFileProvider mapFileProvider,
            DateTime startTime)
        {
            _mapFileProvider = mapFileProvider;
            Config = config;
            InitializeMapFile();

            if (MapFile.HasData(startTime.Date))
            {
                // initialize mapped symbol using request start date
                Config.MappedSymbol = MapFile.GetMappedSymbol(startTime.Date, Config.MappedSymbol);
            }
        }

        /// <summary>
        /// Check for new mappings
        /// </summary>
        /// <param name="eventArgs">The new tradable day event arguments</param>
        /// <returns>New mapping event if any</returns>
        public virtual IEnumerable<BaseData> GetEvents(NewTradableDateEventArgs eventArgs)
        {
            if (Config.Symbol == eventArgs.Symbol
                && MapFile.HasData(eventArgs.Date))
            {
                var old = Config.MappedSymbol;
                var newSymbol = MapFile.GetMappedSymbol(eventArgs.Date, Config.MappedSymbol);
                Config.MappedSymbol = newSymbol;

                // check to see if the symbol was remapped
                if (old != Config.MappedSymbol)
                {
                    var changed = new SymbolChangedEvent(
                        Config.Symbol,
                        eventArgs.Date,
                        old,
                        Config.MappedSymbol);
                    // SqCore Change NEW:
                    if (Config.Resolution == Resolution.Daily && SqBacktestConfig.SqDailyTradingAtMOC) // don't change the original QC method if it is per Minute resolution
                        changed.Time = changed.Time.AddHours(-8);
                    // SqCore Change END
                    yield return changed;
                }
            }
        }

        /// <summary>
        /// Initializes the map file to use
        /// </summary>
        protected void InitializeMapFile()
        {
            MapFile = _mapFileProvider.ResolveMapFile(Config);
        }
    }
}
