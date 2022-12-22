using System;
using QuantConnect.Data;
using QuantConnect.Interfaces;
using System.Collections.Generic;
using QuantConnect.Data.Auxiliary;
using QuantConnect.Data.UniverseSelection;

namespace QuantConnect.Lean.Engine.DataFeeds.Enumerators.Factories
{
    /// <summary>
    /// Provides a default implementation of <see cref="ISubscriptionEnumeratorFactory"/> that uses
    /// <see cref="BaseData"/> factory methods for reading sources
    /// </summary>
    public class BaseDataSubscriptionEnumeratorFactory : ISubscriptionEnumeratorFactory
    {
        private readonly IOptionChainProvider _optionChainProvider;
        private readonly IFutureChainProvider _futureChainProvider;
        private readonly Func<SubscriptionRequest, IEnumerable<DateTime>> _tradableDaysProvider;

        /// <summary>
        /// Initializes a new instance of the <see cref="BaseDataSubscriptionEnumeratorFactory"/> class
        /// </summary>
        /// <param name="optionChainProvider">The option chain provider</param>
        /// <param name="futureChainProvider">The future chain provider</param>
        public BaseDataSubscriptionEnumeratorFactory(IOptionChainProvider optionChainProvider, IFutureChainProvider futureChainProvider)
        {
            _futureChainProvider = futureChainProvider;
            _optionChainProvider = optionChainProvider;
            _tradableDaysProvider = (request => request.TradableDays);
        }

        /// <summary>
        /// Creates an enumerator to read the specified request
        /// </summary>
        /// <param name="request">The subscription request to be read</param>
        /// <param name="dataProvider">Provider used to get data when it is not present on disk</param>
        /// <returns>An enumerator reading the subscription request</returns>
        public IEnumerator<BaseData> CreateEnumerator(SubscriptionRequest request, IDataProvider dataProvider)
        {
            // We decide to use the ZipDataCacheProvider instead of the SingleEntryDataCacheProvider here
            // for resiliency and as a fix for an issue preventing us from reading non-equity options data.
            // It has the added benefit of caching any zip files that we request from the filesystem, and reading
            // files contained within the zip file, which the SingleEntryDataCacheProvider does not support.
            var sourceFactory = request.Configuration.GetBaseDataInstance();
            foreach (var date in _tradableDaysProvider(request))
            {
                IEnumerable<Symbol> symbols;
                if (request.Configuration.SecurityType.IsOption())
                {
                    symbols = _optionChainProvider.GetOptionContractList(request.Configuration.Symbol.Underlying, date);
                }
                else if (request.Configuration.SecurityType == SecurityType.Future)
                {
                    symbols = _futureChainProvider.GetFutureContractList(request.Configuration.Symbol, date);
                }
                else
                {
                    throw new NotImplementedException($"{request.Configuration.SecurityType} is not supported");
                }

                foreach (var symbol in symbols)
                {
                    yield return new ZipEntryName { Symbol = symbol, Time = date };
                }
            }
        }
    }
}
