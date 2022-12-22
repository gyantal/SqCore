namespace QuantConnect.Util.RateLimit
{
    /// <summary>
    /// Provides a strategy for making tokens available for consumption in the <see cref="ITokenBucket"/>
    /// </summary>
    public interface IRefillStrategy
    {
        /// <summary>
        /// Computes the number of new tokens made available, typically via the passing of time.
        /// </summary>
        long Refill();
    }
}