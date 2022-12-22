using System;
using System.Threading;

namespace QuantConnect.Util.RateLimit
{
    /// <summary>
    /// Provides extension methods for interacting with <see cref="ITokenBucket"/> instances as well
    /// as access to the <see cref="NullTokenBucket"/> via <see cref="TokenBucket.Null"/>
    /// </summary>
    public static class TokenBucket
    {
        /// <summary>
        /// Gets an <see cref="ITokenBucket"/> that always permits consumption
        /// </summary>
        public static ITokenBucket Null = new NullTokenBucket();

        /// <summary>
        /// Provides an overload of <see cref="ITokenBucket.Consume"/> that accepts a <see cref="TimeSpan"/> timeout
        /// </summary>
        public static void Consume(this ITokenBucket bucket, long tokens, TimeSpan timeout)
        {
            bucket.Consume(tokens, (long) timeout.TotalMilliseconds);
        }

        /// <summary>
        /// Provides an implementation of <see cref="ITokenBucket"/> that does not enforce rate limiting
        /// </summary>
        private class NullTokenBucket : ITokenBucket
        {
            public long Capacity => long.MaxValue;
            public long AvailableTokens => long.MaxValue;
            public bool TryConsume(long tokens) { return true; }
            public void Consume(long tokens, long timeout = Timeout.Infinite) { }
        }
    }
}