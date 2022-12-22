using System.Collections.Concurrent;
using System.Linq;
using QuantConnect.Interfaces;

namespace QuantConnect.Data.Auxiliary
{
    /// <summary>
    /// Implementation of IPrimaryExchangeProvider from map files. 
    /// </summary>
    public class MapFilePrimaryExchangeProvider : IPrimaryExchangeProvider
    {
        private readonly IMapFileProvider _mapFileProvider;
        private readonly ConcurrentDictionary<SecurityIdentifier, Exchange> _primaryExchangeBySid;

        /// <summary>
        /// Constructor for Primary Exchange Provider from MapFiles
        /// </summary>
        /// <param name="mapFileProvider">MapFile to use</param>
        public MapFilePrimaryExchangeProvider(IMapFileProvider mapFileProvider)
        {
            _mapFileProvider = mapFileProvider;
            _primaryExchangeBySid = new ConcurrentDictionary<SecurityIdentifier, Exchange>();
        }

        /// <summary>
        /// Gets the primary exchange for a given security identifier
        /// </summary>
        /// <param name="securityIdentifier">The security identifier to get the primary exchange for</param>
        /// <returns>Returns the primary exchange or null if not found</returns>
        public Exchange GetPrimaryExchange(SecurityIdentifier securityIdentifier)
        {
            Exchange primaryExchange;
            if (!_primaryExchangeBySid.TryGetValue(securityIdentifier, out primaryExchange))
            {
                var mapFile = _mapFileProvider.Get(AuxiliaryDataKey.Create(securityIdentifier))
                    .ResolveMapFile(securityIdentifier.Symbol, securityIdentifier.Date);
                if (mapFile != null && mapFile.Any())
                {
                    primaryExchange = mapFile.Last().PrimaryExchange;
                }
                _primaryExchangeBySid[securityIdentifier] = primaryExchange;
            }

            return primaryExchange;
        }
    }
}
