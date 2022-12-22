namespace QuantConnect.Brokerages
{
    /// <summary>
    /// Specifies the type of message received from an IBrokerage implementation
    /// </summary>
    public enum BrokerageMessageType
    {
        /// <summary>
        /// Informational message (0)
        /// </summary>
        Information,

        /// <summary>
        /// Warning message (1)
        /// </summary>
        Warning,

        /// <summary>
        /// Fatal error message, the algo will be stopped (2)
        /// </summary>
        Error,

        /// <summary>
        /// Brokerage reconnected with remote server (3)
        /// </summary>
        Reconnect,

        /// <summary>
        /// Brokerage disconnected from remote server (4)
        /// </summary>
        Disconnect
    }
}