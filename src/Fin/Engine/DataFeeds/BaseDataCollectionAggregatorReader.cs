using System;
using QuantConnect.Data;
using QuantConnect.Interfaces;
using System.Collections.Generic;
using QuantConnect.Data.UniverseSelection;

namespace QuantConnect.Lean.Engine.DataFeeds
{
    /// <summary>
    /// Data source reader that will aggregate data points into a base data collection
    /// </summary>
    public class BaseDataCollectionAggregatorReader : TextSubscriptionDataSourceReader
    {
        private readonly Type _collectionType;
        private BaseDataCollection _collection;

        /// <summary>
        /// Initializes a new instance of the <see cref="TextSubscriptionDataSourceReader"/> class
        /// </summary>
        /// <param name="dataCacheProvider">This provider caches files if needed</param>
        /// <param name="config">The subscription's configuration</param>
        /// <param name="date">The date this factory was produced to read data for</param>
        /// <param name="isLiveMode">True if we're in live mode, false for backtesting</param>
        public BaseDataCollectionAggregatorReader(IDataCacheProvider dataCacheProvider, SubscriptionDataConfig config, DateTime date, bool isLiveMode)
            : base(dataCacheProvider, config, date, isLiveMode)
        {
            _collectionType = config.Type;
        }

        /// <summary>
        /// Reads the specified <paramref name="source"/>
        /// </summary>
        /// <param name="source">The source to be read</param>
        /// <returns>An <see cref="IEnumerable{BaseData}"/> that contains the data in the source</returns>
        public override IEnumerable<BaseData> Read(SubscriptionDataSource source)
        {
            foreach (var point in base.Read(source))
            {
                if (point is BaseDataCollection)
                {
                    // if underlying already is returning a collection let it through as is
                    yield return point;
                }
                else
                {
                    if (_collection != null && _collection.EndTime != point.EndTime)
                    {
                        // when we get a new time we flush current collection instance, if any
                        yield return _collection;
                        _collection = null;
                    }

                    if (_collection == null)
                    {
                        _collection = (BaseDataCollection)Activator.CreateInstance(_collectionType);
                        _collection.Time = point.Time;
                        _collection.Symbol = point.Symbol;
                        _collection.EndTime = point.EndTime;
                    }
                    // aggregate the data points
                    _collection.Add(point);
                }
            }

            // underlying reader ended, flush current collection instance if any
            if (_collection != null)
            {
                yield return _collection;
                _collection = null;
            }
        }
    }
}
