using System.Collections.Generic;

namespace QuantConnect.Securities.CurrencyConversion
{
    /// <summary>
    /// Represents a type capable of calculating the conversion rate between two currencies
    /// </summary>
    public interface ICurrencyConversion
    {
        /// <summary>
        /// The currency this conversion converts from
        /// </summary>
        string SourceCurrency { get; }

        /// <summary>
        /// The currency this conversion converts to
        /// </summary>
        string DestinationCurrency { get; }

        /// <summary>
        /// The current conversion rate between <see cref="SourceCurrency"/> and <see cref="DestinationCurrency"/>
        /// </summary>
        decimal ConversionRate { get; }

        /// <summary>
        /// The securities which the conversion rate is based on
        /// </summary>
        IEnumerable<Security> ConversionRateSecurities { get; }

        /// <summary>
        /// Updates the internal conversion rate based on the latest data, and returns the new conversion rate
        /// </summary>
        /// <returns>The new conversion rate</returns>
        decimal Update();
    }
}
