namespace QuantConnect.Orders
{
    /// <summary>
    /// The purpose of this class is to store time and price information
    /// available at the time an order was submitted.
    /// </summary>
    public class OrderSubmissionData
    {
        /// <summary>
        /// The bid price at order submission time
        /// </summary>
        public decimal BidPrice { get; }

        /// <summary>
        /// The ask price at order submission time
        /// </summary>
        public decimal AskPrice { get; }

        /// <summary>
        /// The current price at order submission time
        /// </summary>
        public decimal LastPrice { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="OrderSubmissionData"/> class
        /// </summary>
        /// <remarks>This method is currently only used for testing.</remarks>
        public OrderSubmissionData(decimal bidPrice, decimal askPrice, decimal lastPrice)
        {
            BidPrice = bidPrice;
            AskPrice = askPrice;
            LastPrice = lastPrice;
        }

        /// <summary>
        /// Return a new instance clone of this object
        /// </summary>
        public OrderSubmissionData Clone()
        {
            return (OrderSubmissionData)MemberwiseClone();
        }
    }
}
