using System;
using QuantConnect.Data;
using QuantConnect.Interfaces;
using System.Collections.Generic;

namespace QuantConnect.Lean.Engine.DataFeeds
{
    /// <summary>
    /// Provides an implementation of <see cref="ISubscriptionDataSourceReader"/> that reads zip entry names
    /// </summary>
    public class ZipEntryNameSubscriptionDataSourceReader : ISubscriptionDataSourceReader
    {
        private readonly IDataCacheProvider _dataProvider;
        private readonly SubscriptionDataConfig _config;
        private readonly DateTime _date;
        private readonly bool _isLiveMode;
        private readonly BaseData _factory;

        /// <summary>
        /// Event fired when the specified source is considered invalid, this may
        /// be from a missing file or failure to download a remote source
        /// </summary>
        public event EventHandler<InvalidSourceEventArgs> InvalidSource;

        /// <summary>
        /// Initializes a new instance of the <see cref="ZipEntryNameSubscriptionDataSourceReader"/> class
        /// </summary>
        /// <param name="dataProvider">Used to fetch data</param>
        /// <param name="config">The subscription's configuration</param>
        /// <param name="date">The date this factory was produced to read data for</param>
        /// <param name="isLiveMode">True if we're in live mode, false for backtesting</param>
        public ZipEntryNameSubscriptionDataSourceReader(IDataCacheProvider dataProvider, SubscriptionDataConfig config, DateTime date, bool isLiveMode)
        {
            _date = date;
            _config = config;
            _isLiveMode = isLiveMode;
            _dataProvider = dataProvider;
            _factory = config.GetBaseDataInstance();
        }

        /// <summary>
        /// Reads the specified <paramref name="source"/>
        /// </summary>
        /// <param name="source">The source to be read</param>
        /// <returns>An <see cref="IEnumerable{BaseData}"/> that contains the data in the source</returns>
        public IEnumerable<BaseData> Read(SubscriptionDataSource source)
        {
            List<string> entryNames;
            try
            {
                entryNames = _dataProvider.GetZipEntries(source.Source);
            }
            catch (Exception err)
            {
                OnInvalidSource(source, err);
                yield break;
            }

            foreach (var entryFileName in entryNames)
            {
                var instance = _factory.Reader(_config, entryFileName, _date, _isLiveMode);
                if (instance != null && instance.EndTime != default(DateTime))
                {
                    yield return instance;
                }
            }
        }

        /// <summary>
        /// Event invocator for the <see cref="InvalidSource"/> event
        /// </summary>
        /// <param name="source">The <see cref="SubscriptionDataSource"/> that was invalid</param>
        /// <param name="exception">The exception if one was raised, otherwise null</param>
        private void OnInvalidSource(SubscriptionDataSource source, Exception exception)
        {
            InvalidSource?.Invoke(this, new InvalidSourceEventArgs(source, exception));
        }
    }
}
