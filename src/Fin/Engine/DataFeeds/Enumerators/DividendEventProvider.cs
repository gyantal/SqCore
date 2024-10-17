using System;
using System.Collections.Generic;
using QuantConnect.Data;
using QuantConnect.Data.Auxiliary;
using QuantConnect.Data.Market;
using QuantConnect.Interfaces;
using QuantConnect.Parameters;

namespace QuantConnect.Lean.Engine.DataFeeds.Enumerators
{
    /// <summary>
    /// Event provider who will emit <see cref="Dividend"/> events
    /// </summary>
    public class DividendEventProvider : ITradableDateEventProvider
    {
        // we set the price factor ratio when we encounter a dividend in the factor file
        // and on the next trading day we use this data to produce the dividend instance
        private decimal? _priceFactorRatio;
        private decimal _referencePrice;
        private CorporateFactorProvider _factorFile;
        private MapFile _mapFile;
        private SubscriptionDataConfig _config;

        /// <summary>
        /// Initializes this instance
        /// </summary>
        /// <param name="config">The <see cref="SubscriptionDataConfig"/></param>
        /// <param name="factorFileProvider">The factor file provider to use</param>
        /// <param name="mapFileProvider">The <see cref="Data.Auxiliary.MapFile"/> provider to use</param>
        /// <param name="startTime">Start date for the data request</param>
        public void Initialize(
            SubscriptionDataConfig config,
            IFactorFileProvider factorFileProvider,
            IMapFileProvider mapFileProvider,
            DateTime startTime)
        {
            _config = config;
            _mapFile = mapFileProvider.ResolveMapFile(_config);
            _factorFile = factorFileProvider.Get(_config.Symbol) as CorporateFactorProvider;
        }

        /// <summary>
        /// Check for dividends and returns them
        /// </summary>
        /// <param name="eventArgs">The new tradable day event arguments</param>
        /// <returns>New Dividend event if any</returns>
        public IEnumerable<BaseData> GetEvents(NewTradableDateEventArgs eventArgs)
        {
            if (_config.Symbol == eventArgs.Symbol
                && _factorFile != null
                && _mapFile.HasData(eventArgs.Date))
            {
                if (_priceFactorRatio != null)
                {
                    if (_referencePrice == 0)
                    {
                        throw new InvalidOperationException($"Zero reference price for {_config.Symbol} dividend at {eventArgs.Date}");
                    }

                    var baseData = Dividend.Create(
                        _config.Symbol,
                        eventArgs.Date,
                        _referencePrice,
                        _priceFactorRatio.Value
                    );

                    // SqCore Change NEW:
                    if (_config.Resolution == Resolution.Daily && SqBacktestConfig.SqDailyTradingAtMOC)
                        baseData.Time = baseData.Time.AddHours(-8);
                    // SqCore Change END

                    // let the config know about it for normalization
                    _config.SumOfDividends += baseData.Distribution;
                    _priceFactorRatio = null;
                    _referencePrice = 0;
                    // if (eventArgs.Date > new DateTime(2020, 03, 29) && eventArgs.Date < new DateTime(2020, 04, 05))
                    // {
                    //     SqBacktestConfig.g_quickDebugLog.AppendLine($"DivOcc created. Time: {eventArgs.Date}, EndTime: {eventArgs.Date}, Dividend: {_config.SumOfDividends}, Close: {_referencePrice}");
                    // }
                    yield return baseData;
                }

                // check the factor file to see if we have a dividend event tomorrow
                decimal priceFactorRatio;
                decimal referencePrice;
                if (_factorFile.HasDividendEventOnNextTradingDay(eventArgs.Date, out priceFactorRatio, out referencePrice))
                {
                    _priceFactorRatio = priceFactorRatio;
                    _referencePrice = referencePrice;
                    // if (eventArgs.Date > new DateTime(2020, 03, 29) && eventArgs.Date < new DateTime(2020, 04, 05))
                    // {
                    //     SqBacktestConfig.g_quickDebugLog.AppendLine($"DivWarning created. Time: {eventArgs.Date}, EndTime: {eventArgs.Date}, FactorValue: {priceFactorRatio}, Close: {referencePrice}");
                    // }
                }
            }
        }
    }
}
