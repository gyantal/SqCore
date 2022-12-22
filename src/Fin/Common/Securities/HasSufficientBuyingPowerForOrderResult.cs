namespace QuantConnect.Securities
{
    /// <summary>
    /// Contains the information returned by <see cref="IBuyingPowerModel.HasSufficientBuyingPowerForOrder"/>
    /// </summary>
    public class HasSufficientBuyingPowerForOrderResult
    {
        /// <summary>
        /// Gets true if there is sufficient buying power to execute an order
        /// </summary>
        public bool IsSufficient { get; }

        /// <summary>
        /// Gets the reason for insufficient buying power to execute an order
        /// </summary>
        public string Reason { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="HasSufficientBuyingPowerForOrderResult"/> class
        /// </summary>
        /// <param name="isSufficient">True if the order can be executed</param>
        /// <param name="reason">The reason for insufficient buying power</param>
        public HasSufficientBuyingPowerForOrderResult(bool isSufficient, string reason = null)
        {
            IsSufficient = isSufficient;
            Reason = reason ?? string.Empty;
        }
    }
}
