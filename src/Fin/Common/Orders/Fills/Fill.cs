namespace QuantConnect.Orders.Fills
{
    /// <summary>
    /// Defines the result for <see cref="IFillModel.Fill"/>
    /// </summary>
    public class Fill
    {
        /// <summary>
        /// The order event associated to this <see cref="Fill"/> instance
        /// </summary>
        public OrderEvent OrderEvent { get; }

        /// <summary>
        /// Creates a new <see cref="Fill"/> instance
        /// </summary>
        /// <param name="orderEvent"></param>
        public Fill(OrderEvent orderEvent)
        {
            OrderEvent = orderEvent;
        }
    }
}
