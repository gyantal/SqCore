using System;
using System.Collections.Generic;
using QuantConnect.Data;

namespace QuantConnect.Securities
{
    /// <summary>
    /// Provides a functional implementation of <see cref="IDerivativeSecurityFilter"/>
    /// </summary>
    public class FuncSecurityDerivativeFilter : IDerivativeSecurityFilter
    {
        private readonly Func<IDerivativeSecurityFilterUniverse, IDerivativeSecurityFilterUniverse> _filter;

        /// <summary>
        /// Initializes a new instance of the <see cref="FuncSecurityDerivativeFilter"/> class
        /// </summary>
        /// <param name="filter">The functional implementation of the <see cref="Filter"/> method</param>
        public FuncSecurityDerivativeFilter(Func<IDerivativeSecurityFilterUniverse, IDerivativeSecurityFilterUniverse> filter)
        {
            _filter = filter;
        }

        /// <summary>
        /// Filters the input set of symbols represented by the universe 
        /// </summary>
        /// <param name="universe">Derivative symbols universe used in filtering</param>
        /// <returns>The filtered set of symbols</returns>
        public IDerivativeSecurityFilterUniverse Filter(IDerivativeSecurityFilterUniverse universe)
        {
            return _filter(universe);
        }
    }
}