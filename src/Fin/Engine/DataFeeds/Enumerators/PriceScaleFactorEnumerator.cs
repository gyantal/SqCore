using System;
using System.Collections;
using System.Collections.Generic;
using QuantConnect.Data;
using QuantConnect.Data.Auxiliary;
using QuantConnect.Interfaces;

namespace QuantConnect.Lean.Engine.DataFeeds.Enumerators
{
    /// <summary>
    /// This enumerator will update the <see cref="SubscriptionDataConfig.PriceScaleFactor"/> when required
    /// and adjust the raw <see cref="BaseData"/> prices based on the provided <see cref="SubscriptionDataConfig"/>.
    /// Assumes the prices of the provided <see cref="IEnumerator"/> are in raw mode.
    /// </summary>
    public class PriceScaleFactorEnumerator : IEnumerator<BaseData>
    {
        private readonly IEnumerator<BaseData> _rawDataEnumerator;
        private readonly SubscriptionDataConfig _config;
        private readonly IFactorFileProvider _factorFileProvider;
        private DateTime _nextTradableDate;
        private IFactorProvider _factorFile;
        private bool _liveMode;

        /// <summary>
        /// Explicit interface implementation for <see cref="Current"/>
        /// </summary>
        object IEnumerator.Current => Current;

        /// <summary>
        /// Last read <see cref="BaseData"/> object from this type and source
        /// </summary>
        public BaseData Current
        {
            get;
            private set;
        }

        /// <summary>
        /// Creates a new instance of the <see cref="PriceScaleFactorEnumerator"/>.
        /// </summary>
        /// <param name="rawDataEnumerator">The underlying raw data enumerator</param>
        /// <param name="config">The <see cref="SubscriptionDataConfig"/> to enumerate for.
        /// Will determine the <see cref="DataNormalizationMode"/> to use.</param>
        /// <param name="factorFileProvider">The <see cref="IFactorFileProvider"/> instance to use</param>
        /// <param name="liveMode">True, is this is a live mode data stream</param>
        public PriceScaleFactorEnumerator(
            IEnumerator<BaseData> rawDataEnumerator,
            SubscriptionDataConfig config,
            IFactorFileProvider factorFileProvider,
            bool liveMode = false)
        {
            _config = config;
            _liveMode = liveMode;
            _nextTradableDate = DateTime.MinValue;
            _rawDataEnumerator = rawDataEnumerator;
            _factorFileProvider = factorFileProvider;
        }

        /// <summary>
        /// Dispose of the underlying enumerator.
        /// </summary>
        public void Dispose()
        {
            _rawDataEnumerator.Dispose();
        }

        /// <summary>
        /// Advances the enumerator to the next element of the collection.
        /// </summary>
        /// <returns>
        /// True if the enumerator was successfully advanced to the next element;
        /// False if the enumerator has passed the end of the collection.
        /// </returns>
        public bool MoveNext()
        {
            var underlyingReturnValue = _rawDataEnumerator.MoveNext();
            Current = _rawDataEnumerator.Current;

            if (underlyingReturnValue
                && Current != null
                && _factorFileProvider != null
                && _config.DataNormalizationMode != DataNormalizationMode.Raw)
            {
                if (Current.Time >= _nextTradableDate)
                {
                    _factorFile = _factorFileProvider.Get(_config.Symbol);
                    _config.PriceScaleFactor = _factorFile.GetPriceScale(Current.Time.Date, _config.DataNormalizationMode, _config.ContractDepthOffset, _config.DataMappingMode);

                    // update factor files every day
                    _nextTradableDate = Current.Time.Date.AddDays(1);
                    if (_liveMode)
                    {
                        // in live trading we add a offset to make sure new factor files are available
                        _nextTradableDate = _nextTradableDate.Add(Time.LiveAuxiliaryDataOffset);
                    }
                }

                Current = Current.Normalize(_config.PriceScaleFactor, _config.DataNormalizationMode, _config.SumOfDividends);
            }

            return underlyingReturnValue;
        }

        /// <summary>
        /// Reset the IEnumeration
        /// </summary>
        /// <remarks>Not used</remarks>
        public void Reset()
        {
            throw new NotImplementedException("Reset method not implemented. Assumes loop will only be used once.");
        }
    }
}
