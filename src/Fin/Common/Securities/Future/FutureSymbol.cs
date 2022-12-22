using SqCommon;

namespace QuantConnect.Securities.Future
{
    /// <summary>
    /// Static class contains common utility methods specific to symbols representing the future contracts
    /// </summary>
    public static class FutureSymbol
    {
        /// <summary>
        /// Determine if a given Futures contract is a standard contract.
        /// </summary>
        /// <param name="symbol">Future symbol</param>
        /// <returns>True if symbol expiration matches standard expiration</returns>
        public static bool IsStandard(Symbol symbol)
        {
            var contractExpirationDate = symbol.ID.Date.Date;

            try
            {
                // Use our FutureExpiryFunctions to determine standard contracts dates.
                var expiryFunction = FuturesExpiryFunctions.FuturesExpiryFunction(symbol);
                var monthsToAdd = FuturesExpiryUtilityFunctions.GetDeltaBetweenContractMonthAndContractExpiry(symbol.ID.Symbol, contractExpirationDate);
                var contractMonth = contractExpirationDate.AddDays(-(contractExpirationDate.Day - 1))
                    .AddMonths(monthsToAdd);

                var standardExpirationDate = expiryFunction(contractMonth);

                // Return true if the dates match
                return contractExpirationDate == standardExpirationDate.Date;
            }
            catch
            {
                Utils.Logger.Error($"Could not find standard date for {symbol}, will be classified as standard");
                return true;
            }
        }

        /// <summary>
        /// Returns true if the future contract is a weekly contract
        /// </summary>
        /// <param name="symbol">Future symbol</param>
        /// <returns>True if symbol is non-standard contract</returns>
        public static bool IsWeekly(Symbol symbol)
        {
            return !IsStandard(symbol);
        }
    }
}
