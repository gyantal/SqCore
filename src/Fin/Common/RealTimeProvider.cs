using System;

namespace QuantConnect
{
    /// <summary>
    /// Provides an implementation of <see cref="ITimeProvider"/> that
    /// uses <see cref="DateTime.UtcNow"/> to provide the current time
    /// </summary>
    public sealed class RealTimeProvider : ITimeProvider
    {
        /// <summary>
        /// Provides a static instance of the <see cref="RealTimeProvider"/>
        /// </summary>
        /// <remarks>
        /// Since this implementation is stateless, it doesn't make sense to have multiple instances.
        /// </remarks>
        public static readonly ITimeProvider Instance = new RealTimeProvider();

        /// <summary>
        /// Gets the current time in UTC
        /// </summary>
        /// <returns>The current time in UTC</returns>
        public DateTime GetUtcNow()
        {
            return DateTime.UtcNow;
        }
    }
}