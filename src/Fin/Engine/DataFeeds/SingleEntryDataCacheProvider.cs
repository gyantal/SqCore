﻿using System;
using System.IO;
using Ionic.Zip;
using System.Linq;
using QuantConnect.Util;
using QuantConnect.Interfaces;
using System.Collections.Generic;
using SqCommon;

namespace QuantConnect.Lean.Engine.DataFeeds
{
    /// <summary>
    /// Default implementation of the <see cref="IDataCacheProvider"/>
    /// Does not cache data.  If the data is a zip, the first entry is returned
    /// </summary>
    public class SingleEntryDataCacheProvider : IDataCacheProvider
    {
        private readonly IDataProvider _dataProvider;
        private ZipFile _zipFile;
        private Stream _zipFileStream;

        /// <summary>
        /// Property indicating the data is temporary in nature and should not be cached.
        /// </summary>
        public bool IsDataEphemeral { get; }

        /// <summary>
        /// Constructor that takes the <see cref="IDataProvider"/> to be used to retrieve data
        /// </summary>
        public SingleEntryDataCacheProvider(IDataProvider dataProvider, bool isDataEphemeral = true)
        {
            _dataProvider = dataProvider;
            IsDataEphemeral = isDataEphemeral;
        }

        /// <summary>
        /// Fetch data from the cache
        /// </summary>
        /// <param name="key">A string representing the key of the cached data</param>
        /// <returns>An <see cref="Stream"/> of the cached data</returns>
        public Stream Fetch(string key)
        {
            LeanData.ParseKey(key, out var filePath, out var entryName);
            var stream = _dataProvider.Fetch(filePath);

            if (filePath.EndsWith(".zip") && stream != null)
            {
                // get the first entry from the zip file
                try
                {
                    var entryStream = Compression.UnzipStream(stream, out _zipFile, entryName);

                    // save the file stream so it can be disposed later
                    _zipFileStream = stream;

                    return entryStream;
                }
                catch (ZipException exception)
                {
                    Utils.Logger.Error("SingleEntryDataCacheProvider.Fetch(): Corrupt file: " + key + " Error: " + exception);
                    stream.DisposeSafely();
                    return null;
                }
            }

            return stream;
        }

        /// <summary>
        /// Not implemented
        /// </summary>
        /// <param name="key">The source of the data, used as a key to retrieve data in the cache</param>
        /// <param name="data">The data to cache as a byte array</param>
        public void Store(string key, byte[] data)
        {
            //
        }

        /// <summary>
        /// Returns a list of zip entries in a provided zip file
        /// </summary>
        public List<string> GetZipEntries(string zipFile)
        {
            var stream = _dataProvider.Fetch(zipFile);
            if (stream == null)
            {
                throw new ArgumentException($"Failed to create source stream {zipFile}");
            }
            var entryNames = Compression.GetZipEntryFileNames(stream).ToList();
            stream.DisposeSafely();

            return entryNames;
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            _zipFile?.DisposeSafely();
            _zipFileStream?.DisposeSafely();
        }
    }
}
