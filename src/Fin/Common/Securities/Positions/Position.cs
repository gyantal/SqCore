namespace QuantConnect.Securities.Positions
{
    /// <summary>
    /// Defines a quantity of a security's holdings for inclusion in a position group
    /// </summary>
    public class Position : IPosition
    {
        /// <summary>
        /// The symbol
        /// </summary>
        public Symbol Symbol { get; }

        /// <summary>
        /// The quantity
        /// </summary>
        public decimal Quantity { get; }

        /// <summary>
        /// The unit quantity. The unit quantities of a group define the group. For example, a covered
        /// call has 100 units of stock and -1 units of call contracts.
        /// </summary>
        public decimal UnitQuantity { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="Position"/> class
        /// </summary>
        /// <param name="symbol">The symbol</param>
        /// <param name="quantity">The quantity</param>
        /// <param name="unitQuantity">The position's unit quantity within its group</param>
        public Position(Symbol symbol, decimal quantity, decimal unitQuantity)
        {
            Symbol = symbol;
            Quantity = quantity;
            UnitQuantity = unitQuantity;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Position"/> class using the security's lot size
        /// as it's unit quantity. If quantity is null, then the security's holdings quantity is used.
        /// </summary>
        /// <param name="security">The security</param>
        /// <param name="quantity">The quantity, if null, the security's holdings quantity is used</param>
        public Position(Security security, decimal? quantity = null)
            : this(security.Symbol, quantity ?? security.Holdings.Quantity, security.SymbolProperties.LotSize)
        {
        }

        /// <summary>Returns a string that represents the current object.</summary>
        /// <returns>A string that represents the current object.</returns>
        public override string ToString()
        {
            return $"{Symbol}: {Quantity}";
        }
    }
}
