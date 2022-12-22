using System;

namespace QuantConnect.Interfaces
{
    /// <summary>
    /// Event arguments for the <see cref="IDataProvider.NewDataRequest"/> event
    /// </summary>
    public class DataProviderNewDataRequestEventArgs : EventArgs
    {
        /// <summary>
        /// Path to the fetched data
        /// </summary>
        public string Path { get; }

        /// <summary>
        /// Whether the data was fetched successfully
        /// </summary>
        public bool Succeded { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="DataProviderNewDataRequestEventArgs"/> class
        /// </summary>
        /// <param name="path">The path to the fetched data</param>
        /// <param name="succeded">Whether the data was fetched successfully</param>
        public DataProviderNewDataRequestEventArgs(string path, bool succeded)
        {
            Path = path;
            Succeded = succeded;
        }
    }
}
