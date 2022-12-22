using System;

namespace QuantConnect.Lean.Engine.DataFeeds.WorkScheduling
{
    /// <summary>
    /// Base work scheduler abstraction
    /// </summary>
    public abstract class WorkScheduler
    {
        /// <summary>
        /// The quantity of workers to be used
        /// </summary>
        public static int WorkersCount = Configuration.Config.GetInt("data-feed-workers-count", Environment.ProcessorCount);

        /// <summary>
        /// Add a new work item to the queue
        /// </summary>
        /// <param name="symbol">The symbol associated with this work</param>
        /// <param name="workFunc">The work function to run</param>
        /// <param name="weightFunc">The weight function.
        /// Work will be sorted in ascending order based on this weight</param>
        public abstract void QueueWork(Symbol symbol, Func<int, bool> workFunc, Func<int> weightFunc);
    }
}
