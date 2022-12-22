using System.IO;
using System.ComponentModel.Composition;
using System;

namespace QuantConnect.Interfaces
{
    /// <summary>
    /// Fetches a remote file for a security.
    /// Must save the file to Globals.DataFolder.
    /// </summary>
    [InheritedExport(typeof(IDataProvider))]
    public interface IDataProvider
    {
        /// <summary>
        /// Event raised each time data fetch is finished (successfully or not)
        /// </summary>
        event EventHandler<DataProviderNewDataRequestEventArgs> NewDataRequest;

        /// <summary>
        /// Retrieves data to be used in an algorithm
        /// </summary>
        /// <param name="key">A string representing where the data is stored</param>
        /// <returns>A <see cref="Stream"/> of the data requested</returns>
        Stream Fetch(string key);
    }
}
