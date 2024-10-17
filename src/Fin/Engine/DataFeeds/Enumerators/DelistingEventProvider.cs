using System;
using System.Collections.Generic;
using QuantConnect.Data;
using QuantConnect.Data.Auxiliary;
using QuantConnect.Data.Market;
using QuantConnect.Interfaces;
using QuantConnect.Parameters;
using QuantConnect.Util;

namespace QuantConnect.Lean.Engine.DataFeeds.Enumerators
{
    /// <summary>
    /// Event provider who will emit <see cref="Delisting"/> events
    /// </summary>
    public class DelistingEventProvider : ITradableDateEventProvider
    {
        // we'll use these flags to denote we've already fired off the DelistingType.Warning
        // and a DelistedType.Delisted Delisting object, the _delistingType object is save here
        // since we need to wait for the next trading day before emitting
        private bool _delisted;
        private bool _delistedWarning;

        private SubscriptionDataConfig _config;

        /// <summary>
        /// The delisting date
        /// </summary>
        protected ReferenceWrapper<DateTime> DelistingDate { get; set; }

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
            _config = config;
            var mapFile = mapFileProvider.ResolveMapFile(_config);
            DelistingDate = new ReferenceWrapper<DateTime>(config.Symbol.GetDelistingDate(mapFile));
        }

        /// <summary>
        /// Check for delistings
        /// </summary>
        /// <param name="eventArgs">The new tradable day event arguments</param>
        /// <returns>New delisting event if any</returns>
        public IEnumerable<BaseData> GetEvents(NewTradableDateEventArgs eventArgs)
        {
            if (_config.Symbol == eventArgs.Symbol)
            {
                // we send the delisting warning when we reach the delisting date, here we make sure we compare using the date component
                // of the delisting date since for example some futures can trade a few hours in their delisting date, else we would skip on
                // emitting the delisting warning, which triggers us to handle liquidation once delisted
                if (!_delistedWarning && eventArgs.Date >= DelistingDate.Value.Date)
                {
                    _delistedWarning = true;
                    var price = eventArgs.LastBaseData?.Price ?? 0;
                    // SqCore Change ORIGINAL:

                    // yield return new Delisting(
                    //     eventArgs.Symbol,
                    //     DelistingDate.Value.Date,
                    //     price,
                    //     DelistingType.Warning);
                    // SqCore Change NEW:
                    Delisting delisting = new Delisting(
                        eventArgs.Symbol,
                        DelistingDate.Value.Date,
                        price,
                        DelistingType.Warning);
                    if (_config.Resolution == Resolution.Daily && SqBacktestConfig.SqDailyTradingAtMOC)
                        delisting.Time = delisting.Time.AddHours(-8);
                    yield return delisting;
                    // SqCore Change END
                }
                if (!_delisted && eventArgs.Date > DelistingDate.Value)
                {
                    _delisted = true;
                    var price = eventArgs.LastBaseData?.Price ?? 0;
                    // delisted at EOD
                    // SqCore Change ORIGINAL:
                    // yield return new Delisting(
                    //     eventArgs.Symbol,
                    //     DelistingDate.Value.AddDays(1),
                    //     price,
                    //     DelistingType.Delisted);
                    // SqCore Change NEW:
                    Delisting delisting = new Delisting(
                        eventArgs.Symbol,
                        DelistingDate.Value.AddDays(1),
                        price,
                        DelistingType.Delisted);
                    if (_config.Resolution == Resolution.Daily && SqBacktestConfig.SqDailyTradingAtMOC)
                        delisting.Time = delisting.Time.AddHours(-8);
                    yield return delisting;
                    // SqCore Change END
                }
            }
        }
    }
}
