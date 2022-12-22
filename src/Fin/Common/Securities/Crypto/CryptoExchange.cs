namespace QuantConnect.Securities.Crypto
{
    /// <summary>
    /// Crypto exchange class - information and helper tools for Crypto exchange properties
    /// </summary>
    /// <seealso cref="SecurityExchange"/>
    public class CryptoExchange : SecurityExchange
    {

        /// <summary>
        /// Initializes a new instance of the <see cref="CryptoExchange"/> class using market hours
        /// derived from the market-hours-database for the Crypto market
        /// </summary>
        public CryptoExchange(string market)
            : base(MarketHoursDatabase.FromDataFolder().GetExchangeHours(market, null, SecurityType.Crypto))
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CryptoExchange"/> class using the specified
        /// exchange hours to determine open/close times
        /// </summary>
        /// <param name="exchangeHours">Contains the weekly exchange schedule plus holidays</param>
        public CryptoExchange(SecurityExchangeHours exchangeHours)
            : base(exchangeHours)
        {
        }
    }
}