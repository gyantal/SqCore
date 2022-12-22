namespace QuantConnect.Util.RateLimit
{
    /// <summary>
    /// Defines a strategy for sleeping the current thread of execution. This is currently used via the
    /// <see cref="ITokenBucket.Consume"/> in order to wait for new tokens to become available for consumption.
    /// </summary>
    public interface ISleepStrategy
    {
        /// <summary>
        /// Sleeps the current thread in an implementation specific way
        /// and for an implementation specific amount of time
        /// </summary>
        void Sleep();
    }
}