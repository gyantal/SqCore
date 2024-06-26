﻿using System.Collections;
using System.Collections.Generic;
using QuantConnect.Data;
using QuantConnect.Securities;

namespace QuantConnect.Lean.Engine.DataFeeds.Enumerators
{
    /// <summary>
    /// An <see cref="IEnumerator{SubscriptionData}"/> which wraps an existing <see cref="IEnumerator{BaseData}"/>.
    /// </summary>
    /// <remarks>Using this class is important, versus directly yielding, because we setup the <see cref="Dispose"/> chain</remarks>
    public class SubscriptionDataEnumerator : IEnumerator<SubscriptionData>
    {
        private readonly IEnumerator<BaseData> _enumerator;
        private readonly SubscriptionDataConfig _configuration;
        private readonly SecurityExchangeHours _exchangeHours;
        private readonly TimeZoneOffsetProvider _offsetProvider;
        private readonly bool _isUniverse;

        object IEnumerator.Current => Current;

        /// <summary>
        /// Gets the element in the collection at the current position of the enumerator.
        /// </summary>
        public SubscriptionData Current { get; private set; }

        /// <summary>
        /// Creates a new instance
        /// </summary>
        /// <param name="configuration">The subscription's configuration</param>
        /// <param name="exchangeHours">The security's exchange hours</param>
        /// <param name="offsetProvider">The subscription's time zone offset provider</param>
        /// <param name="enumerator">The underlying data enumerator</param>
        /// <param name="isUniverse">The subscription is a universe subscription</param>
        /// <returns>A subscription data enumerator</returns>
        public SubscriptionDataEnumerator(SubscriptionDataConfig configuration,
            SecurityExchangeHours exchangeHours,
            TimeZoneOffsetProvider offsetProvider,
            IEnumerator<BaseData> enumerator,
            bool isUniverse)
        {
            _enumerator = enumerator;
            _offsetProvider = offsetProvider;
            _exchangeHours = exchangeHours;
            _configuration = configuration;
            _isUniverse = isUniverse;
        }

        /// <summary>
        /// Advances the enumerator to the next element of the collection.
        /// </summary>
        /// <returns>True if the enumerator was successfully advanced to the next element;
        /// False if the enumerator has passed the end of the collection.</returns>
        public bool MoveNext()
        {
            var result = _enumerator.MoveNext();
            if (result)
            {
                // Use our config filter to see if we should emit this
                // This currently catches Auxiliary data that we don't want to emit
                if (_enumerator.Current != null && !_configuration.ShouldEmitData(_enumerator.Current, _isUniverse))
                {
                    // We shouldn't emit this data, so we will MoveNext() again.
                    return MoveNext();
                }

                Current = SubscriptionData.Create(_configuration, _exchangeHours, _offsetProvider, _enumerator.Current, _configuration.DataNormalizationMode);
            }
            return result;
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            _enumerator.Dispose();
        }

        /// <summary>
        /// Sets the enumerator to its initial position, which is before the first element in the collection.
        /// </summary>
        public void Reset()
        {
            _enumerator.Reset();
        }
    }
}
