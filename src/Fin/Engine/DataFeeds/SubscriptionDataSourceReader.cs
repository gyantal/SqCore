using System;
using System.IO;
using QuantConnect.Data;
using QuantConnect.Interfaces;
using QuantConnect.Configuration;
using SqCommon;

namespace QuantConnect.Lean.Engine.DataFeeds
{
    /// <summary>
    /// Provides a factory method for creating <see cref="ISubscriptionDataSourceReader"/> instances
    /// </summary>
    public static class SubscriptionDataSourceReader
    {
        private static readonly bool ShowMissingDataLogs = Config.GetBool("show-missing-data-logs", false);

        /// <summary>
        /// Creates a new <see cref="ISubscriptionDataSourceReader"/> capable of handling the specified <paramref name="source"/>
        /// </summary>
        /// <param name="source">The subscription data source to create a factory for</param>
        /// <param name="dataCacheProvider">Used to cache data</param>
        /// <param name="config">The configuration of the subscription</param>
        /// <param name="date">The date to be processed</param>
        /// <param name="isLiveMode">True for live mode, false otherwise</param>
        /// <param name="factory">The base data instance factory</param>
        /// <param name="dataProvider">The data provider to use</param>
        /// <returns>A new <see cref="ISubscriptionDataSourceReader"/> that can read the specified <paramref name="source"/></returns>
        public static ISubscriptionDataSourceReader ForSource(SubscriptionDataSource source, IDataCacheProvider dataCacheProvider, SubscriptionDataConfig config, DateTime date, bool isLiveMode, BaseData factory, IDataProvider dataProvider)
        {
            ISubscriptionDataSourceReader reader;
            switch (source.Format)
            {
                case FileFormat.Csv:
                    reader = new TextSubscriptionDataSourceReader(dataCacheProvider, config, date, isLiveMode);
                    break;

                case FileFormat.UnfoldingCollection:
                    reader = new CollectionSubscriptionDataSourceReader(dataCacheProvider, config, date, isLiveMode);
                    break;

                case FileFormat.ZipEntryName:
                    reader = new ZipEntryNameSubscriptionDataSourceReader(dataCacheProvider, config, date, isLiveMode);
                    break;

                case FileFormat.Index:
                    return new IndexSubscriptionDataSourceReader(dataCacheProvider, config, date, isLiveMode, dataProvider);

                case FileFormat.FoldingCollection:
                    reader = new BaseDataCollectionAggregatorReader(dataCacheProvider, config, date, isLiveMode);
                    break;

                default:
                    throw new NotImplementedException("SubscriptionFactory.ForSource(" + source + ") has not been implemented yet.");
            }

            // wire up event handlers for logging missing files
            if (ShowMissingDataLogs && source.TransportMedium == SubscriptionTransportMedium.LocalFile)
            {
                if (!factory.IsSparseData())
                {
                    reader.InvalidSource += (sender, args) => Utils.Logger.Error($"SubscriptionDataSourceReader.InvalidSource(): File not found: {args.Source.Source}");
                }
            }

            return reader;
        }

        /// <summary>
        /// Creates cache directory if not existing and deletes old files from the cache
        /// </summary>
        public static void CheckRemoteFileCache()
        {
            // create cache directory if not existing
            if (!Directory.Exists(Globals.Cache)) Directory.CreateDirectory(Globals.Cache);

            var frontierToDelete = DateTime.Now.AddHours(-24);
            // clean old files out of the cache
            foreach (var file in Directory.EnumerateFiles(Globals.Cache))
            {
                if (File.GetCreationTime(file) < frontierToDelete) File.Delete(file);
            }
        }
    }
}
