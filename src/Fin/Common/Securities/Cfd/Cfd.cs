﻿using System;
using QuantConnect.Data;
using QuantConnect.Orders.Fees;
using QuantConnect.Orders.Fills;
using QuantConnect.Orders.Slippage;

namespace QuantConnect.Securities.Cfd
{
    /// <summary>
    /// CFD Security Object Implementation for CFD Assets
    /// </summary>
    /// <seealso cref="Security"/>
    public class Cfd : Security
    {
        /// <summary>
        /// Constructor for the CFD security
        /// </summary>
        /// <param name="exchangeHours">Defines the hours this exchange is open</param>
        /// <param name="quoteCurrency">The cash object that represent the quote currency</param>
        /// <param name="config">The subscription configuration for this security</param>
        /// <param name="symbolProperties">The symbol properties for this security</param>
        /// <param name="currencyConverter">Currency converter used to convert <see cref="CashAmount"/>
        /// instances into units of the account currency</param>
        /// <param name="registeredTypes">Provides all data types registered in the algorithm</param>
        public Cfd(SecurityExchangeHours exchangeHours,
            Cash quoteCurrency,
            SubscriptionDataConfig config,
            SymbolProperties symbolProperties,
            ICurrencyConverter currencyConverter,
            IRegisteredSecurityDataTypesProvider registeredTypes)
            : base(config,
                quoteCurrency,
                symbolProperties,
                new CfdExchange(exchangeHours),
                new CfdCache(),
                new SecurityPortfolioModel(),
                new ImmediateFillModel(),
                new ConstantFeeModel(0),
                new ConstantSlippageModel(0),
                new ImmediateSettlementModel(),
                Securities.VolatilityModel.Null,
                new SecurityMarginModel(50m),
                new CfdDataFilter(),
                new SecurityPriceVariationModel(),
                currencyConverter,
                registeredTypes
                )
        {
            Holdings = new CfdHolding(this, currencyConverter);
        }

        /// <summary>
        /// Constructor for the CFD security
        /// </summary>
        /// <param name="symbol">The security's symbol</param>
        /// <param name="exchangeHours">Defines the hours this exchange is open</param>
        /// <param name="quoteCurrency">The cash object that represent the quote currency</param>
        /// <param name="symbolProperties">The symbol properties for this security</param>
        /// <param name="currencyConverter">Currency converter used to convert <see cref="CashAmount"/>
        /// instances into units of the account currency</param>
        /// <param name="registeredTypes">Provides all data types registered in the algorithm</param>
        /// <param name="securityCache">Cache for storing Security data</param>
        public Cfd(Symbol symbol,
            SecurityExchangeHours exchangeHours,
            Cash quoteCurrency,
            SymbolProperties symbolProperties,
            ICurrencyConverter currencyConverter,
            IRegisteredSecurityDataTypesProvider registeredTypes,
            SecurityCache securityCache)
            : base(symbol,
                quoteCurrency,
                symbolProperties,
                new CfdExchange(exchangeHours),
                securityCache,
                new SecurityPortfolioModel(),
                new ImmediateFillModel(),
                new ConstantFeeModel(0),
                new ConstantSlippageModel(0),
                new ImmediateSettlementModel(),
                Securities.VolatilityModel.Null,
                new SecurityMarginModel(50m),
                new CfdDataFilter(),
                new SecurityPriceVariationModel(),
                currencyConverter,
                registeredTypes
                )
        {
            Holdings = new CfdHolding(this, currencyConverter);
        }

        /// <summary>
        /// Gets the contract multiplier for this CFD security
        /// </summary>
        public decimal ContractMultiplier
        {
            get { return SymbolProperties.ContractMultiplier; }
        }

        /// <summary>
        /// Gets the minimum price variation for this CFD security
        /// </summary>
        public decimal MinimumPriceVariation
        {
            get { return SymbolProperties.MinimumPriceVariation; }
        }

        /// <summary>
        /// Decomposes the specified currency pair into a base and quote currency provided as out parameters
        /// </summary>
        /// <param name="symbol">The input symbol to be decomposed</param>
        /// <param name="symbolProperties">The symbol properties for this security</param>
        /// <param name="baseCurrency">The output base currency</param>
        /// <param name="quoteCurrency">The output quote currency</param>
        public static void DecomposeCurrencyPair(Symbol symbol, SymbolProperties symbolProperties, out string baseCurrency, out string quoteCurrency)
        {
            quoteCurrency = symbolProperties.QuoteCurrency;
            if (symbol.Value.EndsWith(quoteCurrency))
            {
                baseCurrency = symbol.Value.RemoveFromEnd(quoteCurrency);
            }
            else
            {
                throw new InvalidOperationException($"Symbol doesn't end with {quoteCurrency}");
            }
        }
    }
}
