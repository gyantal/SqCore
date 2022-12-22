namespace QuantConnect.Orders.Fills
{
    /// <summary>
    /// Represents a model that simulates order fill events
    /// </summary>
    /// <remarks>Please use<see cref="FillModel"/> as the base class for
    /// any implementations of<see cref="IFillModel"/></remarks>
    public interface IFillModel
    {
        /// <summary>
        /// Return an order event with the fill details
        /// </summary>
        /// <param name="parameters">A <see cref="FillModelParameters"/> object containing the security and order</param>
        /// <returns>Order fill information detailing the average price and quantity filled.</returns>
        Fill Fill(FillModelParameters parameters);
    }
}