using System;
using System.IO;
using System.Collections.Concurrent;

namespace QuantConnect.Lean.Engine.DataFeeds
{
    /// <summary>
    /// Base downloader implementation with some helper methods
    /// </summary>
    public abstract class BaseDownloaderDataProvider : DefaultDataProvider
    {
        private readonly ConcurrentDictionary<string, string> _currentDownloads = new ConcurrentDictionary<string, string>();

        /// <summary>
        /// Helper method which guarantees each requested key is downloaded only once concurrently if required based on <see cref="NeedToDownload"/>
        /// </summary>
        /// <param name="key">A string representing where the data is stored</param>
        /// <param name="download">The download operation we want to perform once concurrently per key</param>
        /// <returns>A <see cref="Stream"/> of the data requested</returns>
        protected Stream DownloadOnce(string key, Action<string> download)
        {
            // If we don't already have this file or its out of date, download it
            if (NeedToDownload(key))
            {
                lock (key)
                {
                    // only one thread can add a path at the same time
                    // - The thread that adds the path, downloads the file and removes the path from the collection after it finishes.
                    // - Threads that don't add the path, will get the value in the collection and try taking a lock on it, since the downloading
                    // thread takes the lock on it first, they will wait till he finishes.
                    // This will allow different threads to download different paths at the same time.
                    if (_currentDownloads.TryAdd(key, key))
                    {
                        download(key);
                        _currentDownloads.TryRemove(key, out _);
                        return base.Fetch(key);
                    }
                }
            }

            // even though we should not download in this path, we need to wait for any download in progress to be finished
            // in this case it would be present in the '_currentDownloads' collection with it's lock taken
            _currentDownloads.TryGetValue(key, out var existingKey);
            lock (existingKey ?? new object())
            {
                return base.Fetch(key);
            }
        }

        /// <summary>
        /// Main filter to determine if this file needs to be downloaded
        /// </summary>
        /// <param name="filePath">File we are looking at</param>
        /// <returns>True if should download</returns>
        protected abstract bool NeedToDownload(string filePath);
    }
}
