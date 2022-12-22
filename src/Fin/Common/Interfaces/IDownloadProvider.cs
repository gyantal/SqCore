using System.Collections.Generic;

namespace QuantConnect.Interfaces
{
    /// <summary>
    /// Wrapper on the API for downloading data for an algorithm.
    /// </summary>
    public interface IDownloadProvider
    {
        /// <summary>
        /// Method for downloading data for an algorithm
        /// </summary>
        /// <param name="address">Source URL to download from</param>
        /// <param name="headers">Headers to pass to the site</param>
        /// <param name="userName">Username for basic authentication</param>
        /// <param name="password">Password for basic authentication</param>
        /// <returns>String contents of file</returns>
        string Download(string address, IEnumerable<KeyValuePair<string, string>> headers, string userName, string password);
    }
}
