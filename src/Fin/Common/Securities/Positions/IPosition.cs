namespace QuantConnect.Securities.Positions
{
    /// <summary>
    /// Defines a position for inclusion in a group
    /// </summary>
    public interface IPosition
    {
        /// <summary>
        /// The symbol
        /// </summary>
        Symbol Symbol { get; }

        /// <summary>
        /// The quantity
        /// </summary>
        decimal Quantity { get; }

        /// <summary>
        /// The unit quantity. The unit quantities of a group define the group. For example, a covered
        /// call has 100 units of stock and -1 units of call contracts.
        /// </summary>
        decimal UnitQuantity { get; }
    }
}
