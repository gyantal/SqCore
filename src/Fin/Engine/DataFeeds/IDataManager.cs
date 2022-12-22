namespace QuantConnect.Lean.Engine.DataFeeds
{
    /// <summary>
    /// IDataManager is the engines view of the Data Manager.
    /// </summary>
    public interface IDataManager
    {
        /// <summary>
        /// Get the universe selection instance
        /// </summary>
        UniverseSelection UniverseSelection { get; }
    }
}
