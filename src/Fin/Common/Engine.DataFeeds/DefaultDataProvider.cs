using System;
using System.IO;
using QuantConnect.Interfaces;

namespace QuantConnect.Lean.Engine.DataFeeds
{
    /// <summary>
    /// Default file provider functionality that retrieves data from disc to be used in an algorithm
    /// </summary>
    public class DefaultDataProvider : IDataProvider, IDisposable
    {
        /// <summary>
        /// Event raised each time data fetch is finished (successfully or not)
        /// </summary>
        public event EventHandler<DataProviderNewDataRequestEventArgs> NewDataRequest;

        /// <summary>
        /// Retrieves data from disc to be used in an algorithm
        /// </summary>
        /// <param name="key">A string representing where the data is stored</param>
        /// <returns>A <see cref="Stream"/> of the data requested</returns>
        public virtual Stream Fetch(string key)
        {
            var success = true;
            try
            {
                return new FileStream(key, FileMode.Open, FileAccess.Read, FileShare.Read);
            }
            catch (Exception exception)
            {
                success = false;
                if (exception is DirectoryNotFoundException
                    || exception is FileNotFoundException)
                {
                    return null;
                }

                throw;
            }
            finally
            {
                OnNewDataRequest(new DataProviderNewDataRequestEventArgs(key, success));
            }
        }

        /// <summary>
        /// The stream created by this type is passed up the stack to the IStreamReader
        /// The stream is closed when the StreamReader that wraps this stream is disposed</summary>
        public void Dispose()
        {
            //
        }

        /// <summary>
        /// Event invocator for the <see cref="NewDataRequest"/> event
        /// </summary>
        protected virtual void OnNewDataRequest(DataProviderNewDataRequestEventArgs e)
        {
            NewDataRequest?.Invoke(this, e);
        }
    }
}
