namespace QuantConnect.Securities.Positions
{
    /// <summary>
    /// Result type for <see cref="IPositionGroupBuyingPowerModel.GetMaximumLotsForDeltaBuyingPower"/>
    /// and <see cref="IPositionGroupBuyingPowerModel.GetMaximumLotsForTargetBuyingPower"/>
    /// </summary>
    public class GetMaximumLotsResult
    {
        /// <summary>
        /// Returns the maximum number of lots of the position group that can be
        /// ordered. This is a whole number and is the <see cref="IPositionGroup.Quantity"/>
        /// </summary>
        public decimal NumberOfLots { get; }

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
        /// <param name="numberOfLots">Returns the maximum number of lots of the position group that can be ordered</param>
        /// <param name="reason">The reason for which the maximum order quantity is zero</param>
        public GetMaximumLotsResult(decimal numberOfLots, string reason = null)
        {
            NumberOfLots = numberOfLots;
            Reason = reason ?? string.Empty;
            IsError = Reason != string.Empty;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="GetMaximumOrderQuantityResult"/> class
        /// </summary>
        /// <param name="numberOfLots">Returns the maximum number of lots of the position group that can be ordered</param>
        /// <param name="reason">The reason for which the maximum order quantity is zero</param>
        /// <param name="isError">True if the zero order quantity is an error condition</param>
        public GetMaximumLotsResult(decimal numberOfLots, string reason, bool isError = true)
        {
            IsError = isError;
            NumberOfLots = numberOfLots;
            Reason = reason ?? string.Empty;
        }
    }
}
