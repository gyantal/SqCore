using System;

namespace QuantConnect.Interfaces
{
    /// <summary>
    /// Event arguments for the <see cref="IObjectStore.ErrorRaised"/> event
    /// </summary>
    public class ObjectStoreErrorRaisedEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the <see cref="Exception"/> that was raised
        /// </summary>
        public Exception Error { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="ObjectStoreErrorRaisedEventArgs"/> class
        /// </summary>
        /// <param name="error">The error that was raised</param>
        public ObjectStoreErrorRaisedEventArgs(Exception error)
        {
            Error = error;
        }
    }
}