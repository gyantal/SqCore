namespace QuantConnect.Securities
{
    /// <summary>
    /// Event arguments for the <see cref="SecurityHolding.QuantityChanged"/> event.
    /// The event data contains the previous quantity/price. The current quantity/price
    /// can be accessed via the <see cref="SecurityEventArgs.Security"/> property
    /// </summary>
    public class SecurityHoldingQuantityChangedEventArgs : SecurityEventArgs
    {
        /// <summary>
        /// Gets the holdings quantity before this change
        /// </summary>
        public decimal PreviousQuantity { get; }

        /// <summary>
        /// Gets the average holdings price before this change
        /// </summary>
        public decimal PreviousAveragePrice { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="SecurityHoldingQuantityChangedEventArgs"/> class
        /// </summary>
        /// <param name="security">The security</param>
        /// <param name="previousAveragePrice">The security's previous average holdings price</param>
        /// <param name="previousQuantity">The security's previous holdings quantity</param>
        public SecurityHoldingQuantityChangedEventArgs(
            Security security,
            decimal previousAveragePrice,
            decimal previousQuantity
            )
            : base(security)
        {
            PreviousQuantity = previousQuantity;
            PreviousAveragePrice = previousAveragePrice;
        }
    }
}
