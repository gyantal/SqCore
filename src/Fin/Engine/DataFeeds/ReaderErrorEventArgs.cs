using System;

namespace QuantConnect.Lean.Engine.DataFeeds
{
    /// <summary>
    /// Event arguments for the <see cref="TextSubscriptionDataSourceReader.ReaderError"/> event.
    /// </summary>
    public sealed class ReaderErrorEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the line that caused the error
        /// </summary>
        public string Line
        {
            get; private set;
        }

        /// <summary>
        /// Gets the exception that was caught
        /// </summary>
        public Exception Exception
        {
            get; private set;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ReaderErrorEventArgs"/> class
        /// </summary>
        /// <param name="line">The line that caused the error</param>
        /// <param name="exception">The exception that was caught during the read</param>
        public ReaderErrorEventArgs(string line, Exception exception)
        {
            Line = line;
            Exception = exception;
        }
    }
}