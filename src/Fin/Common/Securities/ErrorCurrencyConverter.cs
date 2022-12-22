using System;

namespace QuantConnect.Securities
{
    /// <summary>
    /// Provides an implementation of <see cref="ICurrencyConverter"/> for use in
    /// tests that don't depend on this behavior.
    /// </summary>
    public class ErrorCurrencyConverter : ICurrencyConverter
    {
        /// <summary>
        /// Gets account currency
        /// </summary>
        public string AccountCurrency
        {
            get
            {
                throw new InvalidOperationException(
                    "Unexpected usage of ErrorCurrencyConverter.AccountCurrency");
            }
        }

        /// <summary>
        /// Provides access to the single instance of <see cref="ErrorCurrencyConverter"/>.
        /// This is done this way to ensure usage is explicit.
        /// </summary>
        public static ICurrencyConverter Instance = new ErrorCurrencyConverter();

        private ErrorCurrencyConverter()
        {
        }

        /// <summary>
        /// Converts a cash amount to the account currency
        /// </summary>
        /// <param name="cashAmount">The <see cref="CashAmount"/> instance to convert</param>
        /// <returns>A new <see cref="CashAmount"/> instance denominated in the account currency</returns>
        public CashAmount ConvertToAccountCurrency(CashAmount cashAmount)
        {
            throw new InvalidOperationException($"This method purposefully throws as a proof that a " +
                $"test does not depend on {nameof(ICurrencyConverter)}.If this exception is encountered, " +
                $"it means the test DOES depend on {nameof(ICurrencyConverter)} and should be properly " +
                $"updated to use a real implementation of {nameof(ICurrencyConverter)}.");
        }
    }
}
