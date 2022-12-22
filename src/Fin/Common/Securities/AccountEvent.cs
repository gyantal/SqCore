using static QuantConnect.StringExtensions;

namespace QuantConnect.Securities
{
    /// <summary>
    /// Messaging class signifying a change in a user's account
    /// </summary>
    public class AccountEvent
    {
        /// <summary>
        /// Gets the total cash balance of the account in units of <see cref="CurrencySymbol"/>
        /// </summary>
        public decimal CashBalance { get; private set; }

        /// <summary>
        /// Gets the currency symbol
        /// </summary>
        public string CurrencySymbol { get; private set; }

        /// <summary>
        /// Creates an AccountEvent
        /// </summary>
        /// <param name="currencySymbol">The currency's symbol</param>
        /// <param name="cashBalance">The total cash balance of the account</param>
        public AccountEvent(string currencySymbol, decimal cashBalance)
        {
            CashBalance = cashBalance;
            CurrencySymbol = currencySymbol;
        }

        /// <summary>
        /// Returns a string that represents the current object.
        /// </summary>
        /// <returns>
        /// A string that represents the current object.
        /// </returns>
        /// <filterpriority>2</filterpriority>
        public override string ToString()
        {
            return Invariant($"Account {CurrencySymbol} Balance: {CashBalance:0.00}");
        }
    }
}
