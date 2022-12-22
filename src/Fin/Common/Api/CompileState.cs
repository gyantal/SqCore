namespace QuantConnect.Api
{
    /// <summary>
    /// State of the compilation request
    /// </summary>
    public enum CompileState
    {
        /// <summary>
        /// Compile waiting in the queue to be processed.
        /// </summary>
        InQueue,

        /// <summary>
        /// Compile was built successfully
        /// </summary>
        BuildSuccess,

        /// <summary>
        /// Build error, check logs for more information
        /// </summary>
        BuildError
    }
}