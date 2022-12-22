namespace QuantConnect.Securities
{
    /// <summary>
    /// Provides the ability to convert cash amounts to the account currency
    /// </summary>
    public interface ICurrencyConverter
    {
        /// <summary>
        /// Gets account currency
        /// </summary>
        string AccountCurrency { get; }

        /// <summary>
        /// Converts a cash amount to the account currency
        /// </summary>
        /// <param name="cashAmount">The <see cref="CashAmount"/> instance to convert</param>
        /// <returns>A new <see cref="CashAmount"/> instance denominated in the account currency</returns>
        CashAmount ConvertToAccountCurrency(CashAmount cashAmount);
    }
}
