using QuantConnect.Data;
using System;

namespace QuantConnect.Lean.Engine.DataFeeds
{
    /// <summary>
    /// Store data both raw and adjusted and the time at which it should be synchronized
    /// </summary>
    public class PrecalculatedSubscriptionData : SubscriptionData
    {
        private BaseData _normalizedData;
        private SubscriptionDataConfig _config;
        private readonly DataNormalizationMode _mode;

        /// <summary>
        /// Gets the data
        /// </summary>
        public override BaseData Data
        {
            get
            {
                if (_config.DataNormalizationMode == DataNormalizationMode.Raw)
                {
                    return _data;
                }
                else if (_config.DataNormalizationMode == _mode)
                {
                    return _normalizedData;
                }
                else
                {
                    throw new ArgumentException($"DataNormalizationMode.{_config.DataNormalizationMode} was requested for " 
                                                + $"symbol {_data.Symbol} but only {_mode} and Raw DataNormalizationMode are available. " 
                                                + "Please configure the desired DataNormalizationMode initially when adding the Symbol");
                }
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="PrecalculatedSubscriptionData"/> class
        /// </summary>
        /// <param name="configuration">The subscription's configuration</param>
        /// <param name="rawData">The base data</param>
        /// <param name="normalizedData">The normalized calculated based on raw data</param>
        /// <param name="normalizationMode">Specifies how data is normalized</param>
        /// <param name="emitTimeUtc">The emit time for the data</param>
        public PrecalculatedSubscriptionData(SubscriptionDataConfig configuration, BaseData rawData, BaseData normalizedData, DataNormalizationMode normalizationMode, DateTime emitTimeUtc)
            : base(rawData, emitTimeUtc)
        {
            _config = configuration;
            _normalizedData = normalizedData;
            _mode = normalizationMode;
        }
    }
}