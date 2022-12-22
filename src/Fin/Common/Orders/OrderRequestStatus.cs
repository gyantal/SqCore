namespace QuantConnect.Orders
{
    /// <summary>
    /// Specifies the status of a request
    /// </summary>
    public enum OrderRequestStatus
    {
        /// <summary>
        /// This is an unprocessed request (0)
        /// </summary>
        Unprocessed,

        /// <summary>
        /// This request is partially processed (1)
        /// </summary>
        Processing,

        /// <summary>
        /// This request has been completely processed (2)
        /// </summary>
        Processed,

        /// <summary>
        /// This request encountered an error (3)
        /// </summary>
        Error
    }
}