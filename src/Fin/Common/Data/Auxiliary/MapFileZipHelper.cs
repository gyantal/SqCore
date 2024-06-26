using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;

namespace QuantConnect.Data.Auxiliary
{
    /// <summary>
    /// Helper class for handling mapfile zip files
    /// </summary>
    public static class MapFileZipHelper
    {
        /// <summary>
        /// Gets the mapfile zip filename for the specified date
        /// </summary>
        public static string GetMapFileZipFileName(string market, DateTime date, SecurityType securityType)
        {
            return Path.Combine(Globals.DataFolder, $"{securityType.SecurityTypeToLower()}/{market}/map_files/map_files_{date:yyyyMMdd}.zip");
        }

        /// <summary>
        /// Reads the zip bytes as text and parses as MapFileRows to create MapFiles
        /// </summary>
        public static IEnumerable<MapFile> ReadMapFileZip(Stream file, string market, SecurityType securityType)
        {
            if (file == null || file.Length == 0)
            {
                return Enumerable.Empty<MapFile>();
            }

            var result = from kvp in Compression.Unzip(file)
                   let filename = kvp.Key
                   where filename.EndsWith(".csv", StringComparison.InvariantCultureIgnoreCase)
                   let lines = kvp.Value.Where(line => !string.IsNullOrEmpty(line))
                   let mapFile = SafeRead(filename, lines, market, securityType)
                   select mapFile;
            return result;
        }

        /// <summary>
        /// Parses the contents as a MapFile, if error returns a new empty map file
        /// </summary>
        private static MapFile SafeRead(string filename, IEnumerable<string> contents, string market, SecurityType securityType)
        {
            var permtick = Path.GetFileNameWithoutExtension(filename);
            try
            {
                return new MapFile(permtick, contents.Select(s => MapFileRow.Parse(s, market, securityType)));
            }
            catch
            {
                return new MapFile(permtick, Enumerable.Empty<MapFileRow>());
            }
        }
    }
}
