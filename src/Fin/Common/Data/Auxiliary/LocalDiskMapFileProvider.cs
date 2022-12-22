using System.IO;
using System.Threading;
using QuantConnect.Interfaces;
using System.Collections.Concurrent;
using SqCommon;
using System;

namespace QuantConnect.Data.Auxiliary
{
    /// <summary>
    /// Provides a default implementation of <see cref="IMapFileProvider"/> that reads from
    /// the local disk
    /// </summary>
    public class LocalDiskMapFileProvider : IMapFileProvider
    {
        private static int _wroteTraceStatement;
        private readonly ConcurrentDictionary<AuxiliaryDataKey, MapFileResolver> _cache;
        private IDataProvider _dataProvider;

        /// <summary>
        /// Creates a new instance of the <see cref="LocalDiskFactorFileProvider"/>
        /// </summary>
        public LocalDiskMapFileProvider()
        {
            _cache = new ConcurrentDictionary<AuxiliaryDataKey, MapFileResolver>();
        }

        /// <summary>
        /// Initializes our MapFileProvider by supplying our dataProvider
        /// </summary>
        /// <param name="dataProvider">DataProvider to use</param>
        public void Initialize(IDataProvider dataProvider)
        {
            _dataProvider = dataProvider;
        }

        /// <summary>
        /// Gets a <see cref="MapFileResolver"/> representing all the map
        /// files for the specified market
        /// </summary>
        /// <param name="auxiliaryDataKey">Key used to fetch a map file resolver. Specifying market and security type</param>
        /// <returns>A <see cref="MapFileRow"/> containing all map files for the specified market</returns>
        public MapFileResolver Get(AuxiliaryDataKey auxiliaryDataKey)
        {
            return _cache.GetOrAdd(auxiliaryDataKey, GetMapFileResolver);
        }

        private MapFileResolver GetMapFileResolver(AuxiliaryDataKey key)
        {
            var securityType = key.SecurityType;
            var market = key.Market;

            var mapFileDirectory = MapFile.GetMapFilePath(market, securityType);

            // running in VsCode:
            // Directory.GetCurrentDirectory();  "C:\\agy\\GitHub\\SqCore\\src\\WebServer\\SqCoreWeb"
            // AppDomain.CurrentDomain.BaseDirectory: "C:\\agy\\GitHub\\SqCore\\src\\WebServer\\SqCoreWeb\\bin\\Debug\\net7.0\\"
            if (!Directory.Exists(mapFileDirectory))
            {
                // only write this message once per application instance
                if (Interlocked.CompareExchange(ref _wroteTraceStatement, 1, 0) == 0)
                {
                    Utils.Logger.Error($"LocalDiskMapFileProvider.GetMapFileResolver({market}): " +
                        $"The specified directory does not exist: {mapFileDirectory}"
                    );
                }
                return MapFileResolver.Empty;
            }
            return new MapFileResolver(MapFile.GetMapFiles(mapFileDirectory, market, securityType, _dataProvider));
        }
    }
}
