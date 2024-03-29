using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;

namespace QuantConnect.Data.Auxiliary
{
    /// <summary>
    /// Provides methods for reading factor file zips
    /// </summary>
    public static class FactorFileZipHelper
    {
        /// <summary>
        /// Gets the factor file zip filename for the specified date
        /// </summary>
        public static string GetFactorFileZipFileName(string market, DateTime date, SecurityType securityType)
        {
            return Path.Combine(Globals.DataFolder, $"{securityType.SecurityTypeToLower()}/{market}/factor_files/factor_files_{date:yyyyMMdd}.zip");
        }

        /// <summary>
        /// Reads the zip bytes as text and parses as FactorFileRows to create FactorFiles
        /// </summary>
        public static IEnumerable<KeyValuePair<Symbol, IFactorProvider>> ReadFactorFileZip(Stream file, MapFileResolver mapFileResolver, string market, SecurityType securityType)
        {
            if (file == null || file.Length == 0)
            {
                return new Dictionary<Symbol, IFactorProvider>();
            }

            var keyValuePairs = (
                    from kvp in Compression.Unzip(file)
                    let filename = kvp.Key
                    let lines = kvp.Value
                    let factorFile = PriceScalingExtensions.SafeRead(Path.GetFileNameWithoutExtension(filename), lines, securityType)
                    let mapFile = mapFileResolver.GetByPermtick(factorFile.Permtick)
                    where mapFile != null
                    select new KeyValuePair<Symbol, IFactorProvider>(GetSymbol(mapFile, market, securityType), factorFile)
                );

            return keyValuePairs;
        }

        private static Symbol GetSymbol(MapFile mapFile, string market, SecurityType securityType)
        {
            SecurityIdentifier sid;
            switch (securityType)
            {
                case SecurityType.Equity:
                    sid = SecurityIdentifier.GenerateEquity(mapFile.FirstDate, mapFile.FirstTicker, market);
                    break;
                case SecurityType.Future:
                    sid = SecurityIdentifier.GenerateFuture(SecurityIdentifier.DefaultDate, mapFile.Permtick, market);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(securityType), securityType, null);
            }
            return new Symbol(sid, mapFile.Permtick);
        }
    }
}
