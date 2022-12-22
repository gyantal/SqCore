using System.Collections.Generic;
using System.Linq;

namespace QuantConnect.Securities.Future
{
    /// <summary>
    /// Provides conversions from a GLOBEX Futures ticker to a GLOBEX Futures Options ticker
    /// </summary>
    public static class FuturesOptionsSymbolMappings
    {
        /// <summary>
        /// Defines Futures GLOBEX Ticker -> Futures Options GLOBEX Ticker
        /// </summary>
        private static Dictionary<string, string> _futureToFutureOptionsGLOBEX = new Dictionary<string, string>
        {
            { "EH", "OEH" },
            { "KE", "OKE" },
            { "TN", "OTN" },
            { "UB", "OUB" },
            { "YM", "OYM" },
            { "ZB", "OZB" },
            { "ZC", "OZC" },
            { "ZF", "OZF" },
            { "ZL", "OZL" },
            { "ZM", "OZM" },
            { "ZN", "OZN" },
            { "ZO", "OZO" },
            { "ZS", "OZS" },
            { "ZT", "OZT" },
            { "ZW", "OZW" },
            { "RTY", "RTO" },
            { "GC", "OG" },
            { "HG", "HXE" },
            { "SI", "SO" },
            { "CL", "LO" },
            { "HCL", "HCO" },
            { "HO", "OH" },
            { "NG", "ON" },
            { "PA", "PAO" },
            { "PL", "PO" },
            { "RB", "OB" },
            { "YG", "OYG" },
            { "ZG", "OZG" },
            { "ZI", "OZI" }
        };

        private static Dictionary<string, string> _futureOptionsToFutureGLOBEX = _futureToFutureOptionsGLOBEX
            .ToDictionary(kvp => kvp.Value, kvp => kvp.Key); 
        
        /// <summary>
        /// Returns the futures options ticker for the given futures ticker.
        /// </summary>
        /// <param name="futureTicker">Future GLOBEX ticker to get Future Option GLOBEX ticker for</param>
        /// <returns>Future option ticker. Defaults to future ticker provided if no entry is found</returns>
        public static string Map(string futureTicker)
        {
            futureTicker = futureTicker.ToUpperInvariant();

            string result;
            if (!_futureToFutureOptionsGLOBEX.TryGetValue(futureTicker, out result))
            {
                return futureTicker;
            }

            return result;
        }

        /// <summary>
        /// Maps a futures options ticker to its underlying future's ticker
        /// </summary>
        /// <param name="futureOptionTicker">Future option ticker to map to the underlying</param>
        /// <returns>Future ticker</returns>
        public static string MapFromOption(string futureOptionTicker)
        {
            futureOptionTicker = futureOptionTicker.ToUpperInvariant();

            string result;
            if (!_futureOptionsToFutureGLOBEX.TryGetValue(futureOptionTicker, out result))
            {
                return futureOptionTicker;
            }

            return result;
        }
    }
}
