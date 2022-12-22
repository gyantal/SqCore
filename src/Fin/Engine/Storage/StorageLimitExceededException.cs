using System;

namespace QuantConnect.Lean.Engine.Storage
{
    /// <summary>
    /// Exception thrown when the object store storage limit has been exceeded
    /// </summary>
    public class StorageLimitExceededException : Exception
    {
        /// <summary>
        /// Creates a new instance of the storage limit exceeded exception
        /// </summary>
        /// <param name="message">The associated message</param>
        public StorageLimitExceededException(string message)
            : base(message)
        {
        }
    }
}
