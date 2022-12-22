using System;
using System.Collections.Generic;

namespace QuantConnect.Securities.Positions
{
    /// <summary>
    /// Provides extension methods for <see cref="IPosition"/>
    /// </summary>
    public static class PositionExtensions
    {
        /// <summary>
        /// Deducts the specified <paramref name="quantityToDeduct"/> from the specified <paramref name="position"/>
        /// </summary>
        /// <param name="position">The source position</param>
        /// <param name="quantityToDeduct">The quantity to deduct</param>
        /// <returns>A new position with the same properties but quantity reduced by the specified amount</returns>
        public static IPosition Deduct(this IPosition position, decimal quantityToDeduct)
        {
            var newQuantity = position.Quantity - quantityToDeduct;
            return new Position(position.Symbol, newQuantity, position.UnitQuantity);
        }

        /// <summary>
        /// Combines the provided positions into a single position with the quantities added and the minimum unit quantity.
        /// </summary>
        /// <param name="position">The position</param>
        /// <param name="other">The other position to add</param>
        /// <returns>The combined position</returns>
        public static IPosition Combine(this IPosition position, IPosition other)
        {
            if (!position.Symbol.Equals(other.Symbol))
            {
                throw new ArgumentException($"Position symbols must match in order to combine quantities.");
            }

            return new Position(position.Symbol,
                position.Quantity + other.Quantity,
                Math.Min(position.UnitQuantity, other.UnitQuantity)
            );
        }

        /// <summary>
        /// Consolidates the provided <paramref name="positions"/> into a dictionary
        /// </summary>
        /// <param name="positions">The positions to be consolidated</param>
        /// <returns>A dictionary containing the consolidated positions</returns>
        public static Dictionary<Symbol, IPosition> Consolidate(this IEnumerable<IPosition> positions)
        {
            var consolidated = new Dictionary<Symbol, IPosition>();
            foreach (var position in positions)
            {
                IPosition existing;
                if (consolidated.TryGetValue(position.Symbol, out existing))
                {
                    // if it already exists then combine it with the existing
                    consolidated[position.Symbol] = existing.Combine(position);
                }
                else
                {
                    consolidated[position.Symbol] = position;
                }
            }

            return consolidated;
        }

        /// <summary>
        /// Creates a new <see cref="IPosition"/> with quantity equal to <paramref name="numberOfLots"/> times its unit quantity
        /// </summary>
        /// <param name="position">The position</param>
        /// <param name="numberOfLots">The number of lots for the new position</param>
        /// <returns>A new position with the specified number of lots</returns>
        public static IPosition WithLots(this IPosition position, decimal numberOfLots)
        {
            return new Position(position.Symbol, numberOfLots * position.UnitQuantity, position.UnitQuantity);
        }
    }
}
