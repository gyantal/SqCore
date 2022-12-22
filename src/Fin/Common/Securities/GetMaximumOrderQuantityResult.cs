namespace QuantConnect.Securities
{
    /// <summary>
    /// Contains the information returned by <see cref="IBuyingPowerModel.GetMaximumOrderQuantityForTargetBuyingPower"/>
    /// and  <see cref="IBuyingPowerModel.GetMaximumOrderQuantityForDeltaBuyingPower"/>
    /// </summary>
    public class GetMaximumOrderQuantityResult
    {
        /// <summary>
        /// Returns the maximum quantity for the order
        /// </summary>
        public decimal Quantity { get; }

        /// <summary>
        /// Returns the reason for which the maximum order quantity is zero
        /// </summary>
        public string Reason { get; }

        /// <summary>
        /// Returns true if the zero order quantity is an error condition and will be shown to the user.
        /// </summary>
        public bool IsError { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="GetMaximumOrderQuantityResult"/> class
        /// </summary>
        /// <param name="quantity">Returns the maximum quantity for the order</param>
        /// <param name="reason">The reason for which the maximum order quantity is zero</param>
        public GetMaximumOrderQuantityResult(decimal quantity, string reason = null)
        {
            Quantity = quantity;
            Reason = reason ?? string.Empty;
            IsError = Reason != string.Empty;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="GetMaximumOrderQuantityResult"/> class
        /// </summary>
        /// <param name="quantity">Returns the maximum quantity for the order</param>
        /// <param name="reason">The reason for which the maximum order quantity is zero</param>
        /// <param name="isError">True if the zero order quantity is an error condition</param>
        public GetMaximumOrderQuantityResult(decimal quantity, string reason, bool isError = true)
        {
            Quantity = quantity;
            Reason = reason ?? string.Empty;
            IsError = isError;
        }
    }
}
