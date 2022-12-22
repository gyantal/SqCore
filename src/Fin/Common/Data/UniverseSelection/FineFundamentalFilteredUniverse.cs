using System;
using Python.Runtime;
using System.Collections.Generic;
using QuantConnect.Data.Fundamental;

namespace QuantConnect.Data.UniverseSelection
{
    /// <summary>
    /// Provides a universe that can be filtered with a <see cref="FineFundamental"/> selection function
    /// </summary>
    public class FineFundamentalFilteredUniverse : SelectSymbolsUniverseDecorator
    {
        /// <summary>
        /// The universe that will be used for fine universe selection
        /// </summary>
        public FineFundamentalUniverse FineFundamentalUniverse { get; private set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="FineFundamentalFilteredUniverse"/> class
        /// </summary>
        /// <param name="universe">The universe to be filtered</param>
        /// <param name="fineSelector">The fine selection function</param>
        public FineFundamentalFilteredUniverse(Universe universe, Func<IEnumerable<FineFundamental>, IEnumerable<Symbol>> fineSelector)
            : base(universe, universe.SelectSymbols)
        {
            FineFundamentalUniverse = new FineFundamentalUniverse(universe.UniverseSettings, fineSelector);
            FineFundamentalUniverse.SelectionChanged += (sender, args) => OnSelectionChanged(((SelectionEventArgs) args).CurrentSelection);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="FineFundamentalFilteredUniverse"/> class
        /// </summary>
        /// <param name="universe">The universe to be filtered</param>
        /// <param name="fineSelector">The fine selection function</param>
        public FineFundamentalFilteredUniverse(Universe universe, PyObject fineSelector)
            : base(universe, universe.SelectSymbols)
        {
            var func = fineSelector.ConvertToDelegate<Func< IEnumerable<FineFundamental>, object>>();
            FineFundamentalUniverse = new FineFundamentalUniverse(universe.UniverseSettings, func.ConvertToUniverseSelectionSymbolDelegate());
            FineFundamentalUniverse.SelectionChanged += (sender, args) => OnSelectionChanged(((SelectionEventArgs)args).CurrentSelection);
        }
    }
}
