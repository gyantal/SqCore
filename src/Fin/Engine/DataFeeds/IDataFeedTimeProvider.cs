namespace QuantConnect.Lean.Engine.DataFeeds
{
    /// <summary>
    /// Reduced interface which exposes required <see cref="ITimeProvider"/> for <see cref="IDataFeed"/> implementations
    /// </summary>
    public interface IDataFeedTimeProvider
    {
        /// <summary>
        /// Continuous UTC time provider
        /// </summary>
        ITimeProvider TimeProvider { get; }

        /// <summary>
        /// Time provider which returns current UTC frontier time
        /// </summary>
        ITimeProvider FrontierTimeProvider { get; }
    }
}
