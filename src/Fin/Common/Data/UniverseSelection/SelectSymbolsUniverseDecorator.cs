using System;
using System.Collections.Generic;

namespace QuantConnect.Data.UniverseSelection
{
    /// <summary>
    /// Provides a univese decoration that replaces the implementation of <see cref="SelectSymbols"/>
    /// </summary>
    public class SelectSymbolsUniverseDecorator : UniverseDecorator
    {
        private readonly SelectSymbolsDelegate _selectSymbols;

        /// <summary>
        /// Delegate type for the <see cref="SelectSymbols"/> method
        /// </summary>
        /// <param name="utcTime">The current utc frontier time</param>
        /// <param name="data">The universe selection data</param>
        /// <returns>The symbols selected by the universe</returns>
        public delegate IEnumerable<Symbol> SelectSymbolsDelegate(DateTime utcTime, BaseDataCollection data);

        /// <summary>
        /// Initializes a new instance of the <see cref="SelectSymbolsUniverseDecorator"/> class
        /// </summary>
        /// <param name="universe">The universe to be decorated</param>
        /// <param name="selectSymbols">The new implementation of <see cref="SelectSymbols"/></param>
        public SelectSymbolsUniverseDecorator(Universe universe, SelectSymbolsDelegate selectSymbols)
            : base(universe)
        {
            _selectSymbols = selectSymbols;
        }

        /// <summary>
        /// Performs universe selection using the data specified
        /// </summary>
        /// <param name="utcTime">The current utc time</param>
        /// <param name="data">The symbols to remain in the universe</param>
        /// <returns>The data that passes the filter</returns>
        public override IEnumerable<Symbol> SelectSymbols(DateTime utcTime, BaseDataCollection data)
        {
            return _selectSymbols(utcTime, data);
        }
    }
}