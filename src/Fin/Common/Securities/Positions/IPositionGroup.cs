using System.Collections.Generic;

namespace QuantConnect.Securities.Positions
{
    /// <summary>
    /// Defines a group of positions allowing for more efficient use of portfolio margin
    /// </summary>
    public interface IPositionGroup : IReadOnlyCollection<IPosition>
    {
        /// <summary>
        /// Gets the key identifying this group
        /// </summary>
        PositionGroupKey Key { get; }

        /// <summary>
        /// Gets the whole number of units in this position group
        /// </summary>
        decimal Quantity { get; }

        /// <summary>
        /// Gets the positions in this group
        /// </summary>
        IEnumerable<IPosition> Positions { get; }

        /// <summary>
        /// Gets the buying power model defining how margin works in this group
        /// </summary>
        IPositionGroupBuyingPowerModel BuyingPowerModel { get; }

        /// <summary>
        /// Attempts to retrieve the position with the specified symbol
        /// </summary>
        /// <param name="symbol">The symbol</param>
        /// <param name="position">The position, if found</param>
        /// <returns>True if the position was found, otherwise false</returns>
        bool TryGetPosition(Symbol symbol, out IPosition position);
    }
}
