using System;
using System.Collections.Generic;
using QuantConnect.Data;
using QuantConnect.Securities;

namespace QuantConnect.Benchmarks
{
    /// <summary>
    /// Creates a benchmark defined by the closing price of a <see cref="Security"/> instance
    /// </summary>
    public class SecurityBenchmark : IBenchmark
    {
        /// <summary>
        /// The benchmark security
        /// </summary>
        public Security Security { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="SecurityBenchmark"/> class
        /// </summary>
        public SecurityBenchmark(Security security)
        {
            Security = security;
        }

        /// <summary>
        /// Evaluates this benchmark at the specified time in units of the account's currency.
        /// </summary>
        /// <param name="time">The time to evaluate the benchmark at</param>
        /// <returns>The value of the benchmark at the specified time
        /// in units of the account's currency.</returns>
        public decimal Evaluate(DateTime time)
        {
            return Security.Price * Security.QuoteCurrency.ConversionRate;
        }

        /// <summary>
        /// Helper function that will create a security with the given SecurityManager
        /// for a specific symbol and then create a SecurityBenchmark for it
        /// </summary>
        /// <param name="securities">SecurityService to create the security</param>
        /// <param name="symbol">The symbol to create a security benchmark with</param>
        /// <returns>The new SecurityBenchmark</returns>
        public static SecurityBenchmark CreateInstance(SecurityManager securities, Symbol symbol)
        {
            // Create the security from this symbol
            var security = securities.CreateSecurity(symbol,
                new List<SubscriptionDataConfig>(),
                leverage: 1,
                addToSymbolCache: false);

            return new SecurityBenchmark(security);
        }
    }
}