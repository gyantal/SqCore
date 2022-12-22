using System;
using System.Collections.Generic;
using System.Linq;
using Python.Runtime;

namespace QuantConnect.Data.UniverseSelection
{
    /// <summary>
    /// Creates a universe based on an ETF's holdings at a given date
    /// </summary>
    public class ETFConstituentsUniverse : ConstituentsUniverse<ETFConstituentData>
    {
        private const string _etfConstituentsUniverseIdentifier = "qc-universe-etf-constituents";
        
        /// <summary>
        /// Creates a new universe for the constituents of the ETF provided as <paramref name="symbol"/>
        /// </summary>
        /// <param name="symbol">The ETF to load constituents for</param>
        /// <param name="universeSettings">Universe settings</param>
        /// <param name="constituentsFilter">The filter function used to filter out ETF constituents from the universe</param>
        public ETFConstituentsUniverse(Symbol symbol, UniverseSettings universeSettings, Func<IEnumerable<ETFConstituentData>, IEnumerable<Symbol>> constituentsFilter = null)
            : base(CreateConstituentUniverseETFSymbol(symbol), universeSettings, constituentsFilter ?? (constituents => constituents.Select(c => c.Symbol)))
        {
        }

        /// <summary>
        /// Creates a new universe for the constituents of the ETF provided as <paramref name="symbol"/>
        /// </summary>
        /// <param name="symbol">The ETF to load constituents for</param>
        /// <param name="universeSettings">Universe settings</param>
        /// <param name="constituentsFilter">The filter function used to filter out ETF constituents from the universe</param>
        public ETFConstituentsUniverse(Symbol symbol, UniverseSettings universeSettings, PyObject constituentsFilter = null)
            : this(symbol, universeSettings, constituentsFilter.ConvertPythonUniverseFilterFunction<ETFConstituentData>())
        {
        }

        /// <summary>
        /// Creates a universe Symbol for constituent ETFs
        /// </summary>
        /// <param name="compositeSymbol">The Symbol of the ETF</param>
        /// <returns>Universe Symbol with ETF set as underlying</returns>
        private static Symbol CreateConstituentUniverseETFSymbol(Symbol compositeSymbol)
        {
            var guid = Guid.NewGuid().ToString();
            var universeTicker = _etfConstituentsUniverseIdentifier + '-' + guid;
            
            return new Symbol(
                SecurityIdentifier.GenerateConstituentIdentifier(
                    universeTicker,
                    compositeSymbol.SecurityType,
                    compositeSymbol.ID.Market),
                universeTicker,
                compositeSymbol);
        }
    }
}
