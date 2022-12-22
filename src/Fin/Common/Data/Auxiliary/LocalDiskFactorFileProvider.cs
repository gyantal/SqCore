using System.IO;
using QuantConnect.Util;
using QuantConnect.Interfaces;
using System.Collections.Concurrent;

namespace QuantConnect.Data.Auxiliary
{
    /// <summary>
    /// Provides an implementation of <see cref="IFactorFileProvider"/> that searches the local disk
    /// </summary>
    public class LocalDiskFactorFileProvider : IFactorFileProvider
    {
        private IMapFileProvider _mapFileProvider;
        private IDataProvider _dataProvider;
        private readonly ConcurrentDictionary<Symbol, IFactorProvider> _cache;

        /// <summary>
        /// Creates a new instance of the <see cref="LocalDiskFactorFileProvider"/>
        /// </summary>
        public LocalDiskFactorFileProvider()
        {
            _cache = new ConcurrentDictionary<Symbol, IFactorProvider>();
        }

        /// <summary>
        /// Initializes our FactorFileProvider by supplying our mapFileProvider
        /// and dataProvider
        /// </summary>
        /// <param name="mapFileProvider">MapFileProvider to use</param>
        /// <param name="dataProvider">DataProvider to use</param>
        public void Initialize(IMapFileProvider mapFileProvider, IDataProvider dataProvider)
        {
            _mapFileProvider = mapFileProvider;
            _dataProvider = dataProvider;
        }

        /// <summary>
        /// Gets a <see cref="FactorFile"/> instance for the specified symbol, or null if not found
        /// </summary>
        /// <param name="symbol">The security's symbol whose factor file we seek</param>
        /// <returns>The resolved factor file, or null if not found</returns>
        public IFactorProvider Get(Symbol symbol)
        {
            symbol = symbol.GetFactorFileSymbol();
            IFactorProvider factorFile;
            if (_cache.TryGetValue(symbol, out factorFile))
            {
                return factorFile;
            }

            // we first need to resolve the map file to get a permtick, that's how the factor files are stored
            var mapFileResolver = _mapFileProvider.Get(AuxiliaryDataKey.Create(symbol));
            if (mapFileResolver == null)
            {
                return GetFactorFile(symbol, symbol.Value);
            }

            var mapFile = mapFileResolver.ResolveMapFile(symbol);
            if (mapFile.IsNullOrEmpty())
            {
                return GetFactorFile(symbol, symbol.Value);
            }

            return GetFactorFile(symbol, mapFile.Permtick);
        }

        /// <summary>
        /// Checks that the factor file exists on disk, and if it does, loads it into memory
        /// </summary>
        private IFactorProvider GetFactorFile(Symbol symbol, string permtick)
        {
            var path = Path.Combine(Globals.CacheDataFolder, symbol.SecurityType.SecurityTypeToLower(), symbol.ID.Market, "factor_files", permtick.ToLowerInvariant() + ".csv");

            var factorFile = PriceScalingExtensions.SafeRead(permtick, _dataProvider.ReadLines(path), symbol.SecurityType);
            _cache.AddOrUpdate(symbol, factorFile, (s, c) => factorFile);
            return factorFile;
        }
    }
}
