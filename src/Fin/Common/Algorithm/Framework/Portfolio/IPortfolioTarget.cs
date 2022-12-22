namespace QuantConnect.Algorithm.Framework.Portfolio
{
    /// <summary>
    /// Represents a portfolio target. This may be a percentage of total portfolio value
    /// or it may be a fixed number of shares.
    /// </summary>
    public interface IPortfolioTarget
    {
        /// <summary>
        /// Gets the symbol of this target
        /// </summary>
        Symbol Symbol { get; }

        /// <summary>
        /// Gets the quantity of this symbol the algorithm should hold
        /// </summary>
        decimal Quantity { get; }
    }
}