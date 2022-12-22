namespace QuantConnect.Optimizer
{
    /// <summary>
    /// The different optimization status
    /// </summary>
    public enum OptimizationStatus
    {
        /// <summary>
        /// Just created and not running optimization (0)
        /// </summary>
        New,

        /// <summary>
        /// We failed or we were aborted (1)
        /// </summary>
        Aborted,

        /// <summary>
        /// We are running (2)
        /// </summary>
        Running,

        /// <summary>
        /// Optimization job has completed (3)
        /// </summary>
        Completed
    }
}
