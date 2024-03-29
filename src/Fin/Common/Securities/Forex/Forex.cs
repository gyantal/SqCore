﻿using System;
using QuantConnect.Data;
using QuantConnect.Orders.Fees;
using QuantConnect.Orders.Fills;
using QuantConnect.Orders.Slippage;

namespace QuantConnect.Securities.Forex
{
    /// <summary>
    /// FOREX Security Object Implementation for FOREX Assets
    /// </summary>
    /// <seealso cref="Security"/>
    public class Forex : Security, IBaseCurrencySymbol
    {
        /// <summary>
        /// Constructor for the forex security
        /// </summary>
        /// <param name="exchangeHours">Defines the hours this exchange is open</param>
        /// <param name="quoteCurrency">The cash object that represent the quote currency</param>
        /// <param name="config">The subscription configuration for this security</param>
        /// <param name="symbolProperties">The symbol properties for this security</param>
        /// <param name="currencyConverter">Currency converter used to convert <see cref="CashAmount"/>
        /// instances into units of the account currency</param>
        /// <param name="registeredTypes">Provides all data types registered in the algorithm</param>
        public Forex(SecurityExchangeHours exchangeHours,
            Cash quoteCurrency,
            SubscriptionDataConfig config,
            SymbolProperties symbolProperties,
            ICurrencyConverter currencyConverter,
            IRegisteredSecurityDataTypesProvider registeredTypes)
            : base(config,
                quoteCurrency,
                symbolProperties,
                new ForexExchange(exchangeHours),
                new ForexCache(),
                new SecurityPortfolioModel(),
                new ImmediateFillModel(),
                new InteractiveBrokersFeeModel(),
                new ConstantSlippageModel(0),
                new ImmediateSettlementModel(),
                Securities.VolatilityModel.Null,
                new SecurityMarginModel(50m),
                new ForexDataFilter(),
                new SecurityPriceVariationModel(),
                currencyConverter,
                registeredTypes
                )
        {
            Holdings = new ForexHolding(this, currencyConverter);

            // decompose the symbol into each currency pair
            string baseCurrencySymbol, quoteCurrencySymbol;
            DecomposeCurrencyPair(config.Symbol.Value, out baseCurrencySymbol, out quoteCurrencySymbol);
            BaseCurrencySymbol = baseCurrencySymbol;
        }

        /// <summary>
        /// Constructor for the forex security
        /// </summary>
        /// <param name="symbol">The security's symbol</param>
        /// <param name="exchangeHours">Defines the hours this exchange is open</param>
        /// <param name="quoteCurrency">The cash object that represent the quote currency</param>
        /// <param name="symbolProperties">The symbol properties for this security</param>
        /// <param name="currencyConverter">Currency converter used to convert <see cref="CashAmount"/>
        /// instances into units of the account currency</param>
        /// <param name="registeredTypes">Provides all data types registered in the algorithm</param>
        /// <param name="securityCache">Cache for storing Security data</param>
        public Forex(Symbol symbol,
            SecurityExchangeHours exchangeHours,
            Cash quoteCurrency,
            SymbolProperties symbolProperties,
            ICurrencyConverter currencyConverter,
            IRegisteredSecurityDataTypesProvider registeredTypes,
            SecurityCache securityCache)
            : base(symbol,
                quoteCurrency,
                symbolProperties,
                new ForexExchange(exchangeHours),
                securityCache,
                new SecurityPortfolioModel(),
                new ImmediateFillModel(),
                new InteractiveBrokersFeeModel(),
                new ConstantSlippageModel(0),
                new ImmediateSettlementModel(),
                Securities.VolatilityModel.Null,
                new SecurityMarginModel(50m),
                new ForexDataFilter(),
                new SecurityPriceVariationModel(),
                currencyConverter,
                registeredTypes
                )
        {
            Holdings = new ForexHolding(this, currencyConverter);

            // decompose the symbol into each currency pair
            string baseCurrencySymbol, quoteCurrencySymbol;
            DecomposeCurrencyPair(symbol.Value, out baseCurrencySymbol, out quoteCurrencySymbol);
            BaseCurrencySymbol = baseCurrencySymbol;
        }

        /// <summary>
        /// Gets the currency acquired by going long this currency pair
        /// </summary>
        /// <remarks>
        /// For example, the EUR/USD has a base currency of the euro, and as a result
        /// of going long the EUR/USD a trader is acquiring euros in exchange for US dollars
        /// </remarks>
        public string BaseCurrencySymbol { get; private set; }

        /// <summary>
        /// Decomposes the specified currency pair into a base and quote currency provided as out parameters
        /// </summary>
        /// <param name="currencyPair">The input currency pair to be decomposed, for example, "EURUSD"</param>
        /// <param name="baseCurrency">The output base currency</param>
        /// <param name="quoteCurrency">The output quote currency</param>
        public static void DecomposeCurrencyPair(string currencyPair, out string baseCurrency, out string quoteCurrency)
        {
            if (currencyPair == null || currencyPair.Length != 6)
            {
                throw new ArgumentException($"Currency pairs must be exactly 6 characters: {currencyPair}");
            }

            baseCurrency = currencyPair.Substring(0, 3);
            quoteCurrency = currencyPair.Substring(3);
        }
    }
}
