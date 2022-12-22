using System;
using System.IO;

namespace QuantConnect.Interfaces
{
    /// <summary>
    /// Defines a transport mechanism for data from its source into various reader methods
    /// </summary>
    public interface IStreamReader : IDisposable
    {
        /// <summary>
        /// Gets the transport medium of this stream reader
        /// </summary>
        SubscriptionTransportMedium TransportMedium { get; }

        /// <summary>
        /// Gets whether or not there's more data to be read in the stream
        /// </summary>
        bool EndOfStream { get; }

        /// <summary>
        /// Gets the next line/batch of content from the stream
        /// </summary>
        string ReadLine();

        /// <summary>
        /// Direct access to the StreamReader instance
        /// </summary>
        StreamReader StreamReader { get; }

        /// <summary>
        /// Gets whether or not this stream reader should be rate limited
        /// </summary>
        bool ShouldBeRateLimited { get; }
    }
}
