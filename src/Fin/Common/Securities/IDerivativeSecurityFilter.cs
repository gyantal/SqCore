using System.Collections.Generic;
using QuantConnect.Data;
using System;

namespace QuantConnect.Securities
{
    /// <summary>
    /// Filters a set of derivative symbols using the underlying price data.
    /// </summary>
    public interface IDerivativeSecurityFilter
    {
        /// <summary>
        /// Filters the input set of symbols represented by the universe 
        /// </summary>
        /// <param name="universe">derivative symbols universe used in filtering</param>
        /// <returns>The filtered set of symbols</returns>
        IDerivativeSecurityFilterUniverse Filter(IDerivativeSecurityFilterUniverse universe);
    }
}
