using System;
using System.IO;
using System.Collections.Generic;

namespace QuantConnect.Interfaces
{
    /// <summary>
    /// Defines a cache for data
    /// </summary>
    public interface IDataCacheProvider : IDisposable
    {
        /// <summary>
        /// Property indicating the data is temporary in nature and should not be cached
        /// </summary>
        bool IsDataEphemeral { get; }

        /// <summary>
        /// Fetch data from the cache
        /// </summary>
        /// <param name="key">A string representing the key of the cached data</param>
        /// <returns>An <see cref="Stream"/> of the cached data</returns>
        Stream Fetch(string key);

        /// <summary>
        /// Store the data in the cache
        /// </summary>
        /// <param name="key">The source of the data, used as a key to retrieve data in the cache</param>
        /// <param name="data">The data to cache as a byte array</param>
        void Store(string key, byte[] data);

        /// <summary>
        /// Returns a list of zip entries in a provided zip file
        /// </summary>
        List<string> GetZipEntries(string zipFile);
    }
}
