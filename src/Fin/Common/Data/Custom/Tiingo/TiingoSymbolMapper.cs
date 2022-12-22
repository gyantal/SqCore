namespace QuantConnect.Data.Custom.Tiingo
{
    /// <summary>
    /// Helper class to map a Lean format ticker to Tiingo format
    /// </summary>
    /// <remarks>To be used when performing direct queries to Tiingo API</remarks>
    /// <remarks>https://api.tiingo.com/documentation/appendix/symbology</remarks>
    public static class TiingoSymbolMapper
    {
        /// <summary>
        /// Maps a given <see cref="Symbol"/> instance to it's Tiingo equivalent
        /// </summary>
        public static string GetTiingoTicker(Symbol symbol)
        {
            return symbol.Value.Replace(".", "-");
        }

        /// <summary>
        /// Maps a given Tiingo ticker to Lean equivalent
        /// </summary>
        public static string GetLeanTicker(string ticker)
        {
            return ticker.Replace("-", ".");
        }
    }
}
