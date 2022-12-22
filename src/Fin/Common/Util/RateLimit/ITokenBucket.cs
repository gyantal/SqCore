using System;
using System.Threading;

namespace QuantConnect.Util.RateLimit
{
    /// <summary>
    /// Defines a token bucket for rate limiting
    /// See: https://en.wikipedia.org/wiki/Token_bucket
    /// </summary>
    /// <remarks>
    /// This code is ported from https://github.com/mxplusb/TokenBucket - since it's a dotnet core
    /// project, there were issued importing the nuget package directly. The referenced repository
    /// is provided under the Apache V2 license.
    /// </remarks>
    public interface ITokenBucket
    {
        /// <summary>
        /// Gets the maximum capacity of tokens this bucket can hold.
        /// </summary>
        long Capacity { get; }

        /// <summary>
        /// Gets the total number of currently available tokens for consumption
        /// </summary>
        long AvailableTokens { get; }

        /// <summary>
        /// Blocks until the specified number of tokens are available for consumption
        /// and then consumes that number of tokens.
        /// </summary>
        /// <param name="tokens">The number of tokens to consume</param>
        /// <param name="timeout">The maximum amount of time, in milliseconds, to block. A <see cref="TimeoutException"/>
        /// is throw in the event it takes longer than the stated timeout to consume the requested number of tokens.
        /// The default timeout is set to infinite, which will block forever.</param>
        void Consume(long tokens, long timeout = Timeout.Infinite);

        /// <summary>
        /// Attempts to consume the specified number of tokens from the bucket. If the
        /// requested number of tokens are not immediately available, then this method
        /// will return false to indicate that zero tokens have been consumed.
        /// </summary>
        bool TryConsume(long tokens);
    }
}