namespace QuantConnect.Orders
{
    /// <summary>
    /// Specifies the data in an order to be updated
    /// </summary>
    public class UpdateOrderFields
    {
        /// <summary>
        /// Specify to update the quantity of the order
        /// </summary>
        public decimal? Quantity { get; set; }

        /// <summary>
        /// Specify to update the limit price of the order
        /// </summary>
        public decimal? LimitPrice { get; set; }

        /// <summary>
        /// Specify to update the stop price of the order
        /// </summary>
        public decimal? StopPrice { get; set; }
        
        /// <summary>
        /// Specify to update the trigger price of the order
        /// </summary>
        public decimal? TriggerPrice { get; set; }
        
        /// <summary>
        /// Specify to update the order's tag
        /// </summary>
        public string Tag { get; set; }
    }
}