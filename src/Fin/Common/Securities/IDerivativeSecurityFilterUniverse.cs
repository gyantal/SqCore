using System.Collections.Generic;
using QuantConnect.Data;

namespace QuantConnect.Securities
{
    /// <summary>
    /// Represents derivative symbols universe used in filtering.
    /// </summary>
    public interface IDerivativeSecurityFilterUniverse : IEnumerable<Symbol>
    {
        /// <summary>
        /// The underlying price data
        /// </summary>
        BaseData Underlying { get; }

        /// <summary>
        /// True if the universe is dynamic and filter needs to be reapplied during trading day
        /// </summary>
        bool IsDynamic { get; }
    }
}
