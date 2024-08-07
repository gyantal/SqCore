using System;
using QuantConnect.Data;
using QuantConnect.Util;
using QuantConnect.Interfaces;
using System.Collections.Generic;

namespace QuantConnect.Lean.Engine.DataFeeds
{
    /// <summary>
    /// This <see cref="ISubscriptionDataSourceReader"/> implementation supports
    /// the <see cref="FileFormat.Index"/> and <see cref="IndexedBaseData"/> types.
    /// Handles the layer of indirection for the index data source and forwards
    /// the target source to the corresponding <see cref="ISubscriptionDataSourceReader"/>
    /// </summary>
    public class IndexSubscriptionDataSourceReader : BaseSubscriptionDataSourceReader
    {
        private readonly SubscriptionDataConfig _config;
        private readonly DateTime _date;
        private IDataProvider _dataProvider;
        private readonly IndexedBaseData _factory;

        /// <summary>
        /// Creates a new instance of this <see cref="ISubscriptionDataSourceReader"/>
        /// </summary>
        public IndexSubscriptionDataSourceReader(IDataCacheProvider dataCacheProvider,
            SubscriptionDataConfig config,
            DateTime date,
            bool isLiveMode,
            IDataProvider dataProvider)
        : base(dataCacheProvider, isLiveMode)
        {
            _config = config;
            _date = date;
            _dataProvider = dataProvider;
            _factory = config.Type.GetBaseDataInstance() as IndexedBaseData;
            if (_factory == null)
            {
                throw new ArgumentException($"{nameof(IndexSubscriptionDataSourceReader)} should be used" +
                                            $"with a data type which implements {nameof(IndexedBaseData)}");
            }
        }

        /// <summary>
        /// Reads the specified <paramref name="source"/>
        /// </summary>
        /// <param name="source">The source to be read</param>
        /// <returns>An <see cref="IEnumerable{BaseData}"/> that contains the data in the source</returns>
        public override IEnumerable<BaseData> Read(SubscriptionDataSource source)
        {
            // handles zip or text files
            using (var reader = CreateStreamReader(source))
            {
                // if the reader doesn't have data then we're done with this subscription
                if (reader == null || reader.EndOfStream)
                {
                    OnInvalidSource(source, new Exception($"The reader was empty for source: ${source.Source}"));
                    yield break;
                }

                // while the reader has data
                while (!reader.EndOfStream)
                {
                    // read a line and pass it to the base data factory
                    var line = reader.ReadLine();
                    if (line.IsNullOrEmpty())
                    {
                        continue;
                    }

                    SubscriptionDataSource dataSource;
                    try
                    {
                        dataSource = _factory.GetSourceForAnIndex(_config, _date, line, IsLiveMode);
                    }
                    catch
                    {
                        OnInvalidSource(source, new Exception("Factory.GetSourceForAnIndex() failed to return a valid source"));
                        yield break;
                    }

                    if (dataSource != null)
                    {
                        var dataReader = SubscriptionDataSourceReader.ForSource(
                            dataSource,
                            DataCacheProvider,
                            _config,
                            _date,
                            IsLiveMode,
                            _factory,
                            _dataProvider);

                        var enumerator = dataReader.Read(dataSource).GetEnumerator();
                        while (enumerator.MoveNext())
                        {
                            yield return enumerator.Current;
                        }
                        enumerator.DisposeSafely();
                    }
                }
            }
        }
    }
}
