using System;

namespace QuantConnect.Interfaces
{
    /// <summary>
    /// Defines live brokerage cash synchronization operations.
    /// </summary>
    public interface IBrokerageCashSynchronizer
    {
        /// <summary>
        /// Gets the datetime of the last sync (UTC)
        /// </summary>
        DateTime LastSyncDateTimeUtc { get; }

        /// <summary>
        /// Returns whether the brokerage should perform the cash synchronization
        /// </summary>
        /// <param name="currentTimeUtc">The current time (UTC)</param>
        /// <returns>True if the cash sync should be performed</returns>
        bool ShouldPerformCashSync(DateTime currentTimeUtc);

        /// <summary>
        /// Synchronizes the cashbook with the brokerage account
        /// </summary>
        /// <param name="algorithm">The algorithm instance</param>
        /// <param name="currentTimeUtc">The current time (UTC)</param>
        /// <param name="getTimeSinceLastFill">A function which returns the time elapsed since the last fill</param>
        /// <returns>True if the cash sync was performed successfully</returns>
        bool PerformCashSync(IAlgorithm algorithm, DateTime currentTimeUtc, Func<TimeSpan> getTimeSinceLastFill);
    }
}
