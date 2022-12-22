using System;
using System.Collections.Generic;
using System.Linq;
using QuantConnect.Util;

namespace QuantConnect.Securities.Positions
{
    /// <summary>
    /// Provides extension methods for <see cref="IPositionGroup"/>
    /// </summary>
    public static class PositionGroupExtensions
    {
        /// <summary>
        /// Gets the position side (long/short/none) of the specified <paramref name="group"/>
        /// </summary>
        public static PositionSide GetPositionSide(this IPositionGroup group)
        {
            if (group.Quantity > 0)
            {
                return PositionSide.Long;
            }

            if (group.Quantity < 0)
            {
                return PositionSide.Short;
            }

            return PositionSide.None;
        }

        /// <summary>
        /// Gets the position in the <paramref name="group"/> matching the provided <param name="symbol"></param>
        /// </summary>
        public static IPosition GetPosition(this IPositionGroup group, Symbol symbol)
        {
            IPosition position;
            if (!group.TryGetPosition(symbol, out position))
            {
                throw new KeyNotFoundException($"No position with symbol '{symbol}' exists in the group: {group}");
            }

            return position;
        }

        /// <summary>
        /// Creates a new <see cref="IPositionGroup"/> with the specified <paramref name="groupQuantity"/>.
        /// If the quantity provided equals the template's quantity then the template is returned.
        /// </summary>
        /// <param name="template">The group template</param>
        /// <param name="groupQuantity">The quantity of the new group</param>
        /// <returns>A position group with the same position ratios as the template but with the specified group quantity</returns>
        public static IPositionGroup WithQuantity(this IPositionGroup template, decimal groupQuantity)
        {
            if (template.Quantity == groupQuantity)
            {
                return template;
            }

            var positions = template.ToArray(p => p.WithLots(groupQuantity));
            return new PositionGroup(template.Key, positions);
        }

        /// <summary>
        /// Gets a user friendly name for the provided <paramref name="group"/>
        /// </summary>
        public static string GetUserFriendlyName(this IPositionGroup group)
        {
            if (group.Count == 1)
            {
                return group.Single().Symbol.ToString();
            }

            return string.Join("|", group.Select(p => p.Symbol.ToString()));
        }
    }
}
