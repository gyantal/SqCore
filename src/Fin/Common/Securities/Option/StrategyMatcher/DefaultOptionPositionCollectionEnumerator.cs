using System.Collections.Generic;

namespace QuantConnect.Securities.Option.StrategyMatcher
{
    /// <summary>
    /// Provides a default implementation of the <see cref="IOptionPositionCollectionEnumerator"/> abstraction.
    /// </summary>
    public class DefaultOptionPositionCollectionEnumerator : IOptionPositionCollectionEnumerator
    {
        /// <summary>
        /// Enumerates <paramref name="positions"/> according to its default enumerator implementation.
        /// </summary>
        public IEnumerable<OptionPosition> Enumerate(OptionPositionCollection positions)
        {
            return positions;
        }
    }
}