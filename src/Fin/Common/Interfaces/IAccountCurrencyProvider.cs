namespace QuantConnect.Interfaces
{
    /// <summary>
    /// A reduced interface for an account currency provider
    /// </summary>
    public interface IAccountCurrencyProvider
    {
        /// <summary>
        /// Gets the account currency
        /// </summary>
        string AccountCurrency { get; }
    }
}
