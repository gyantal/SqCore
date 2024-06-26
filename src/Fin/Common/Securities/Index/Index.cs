﻿using QuantConnect.Data;
using QuantConnect.Orders.Fees;
using QuantConnect.Orders.Fills;
using QuantConnect.Orders.Slippage;

namespace QuantConnect.Securities.Index
{
    /// <summary>
    /// INDEX Security Object Implementation for INDEX Assets
    /// </summary>
    /// <seealso cref="Security"/>
    public class Index : Security
    {
        /// <summary>
        /// Gets or sets whether or not this security should be considered tradable
        /// </summary>
        /// <remarks>Index are non tradable always</remarks>
        public override bool IsTradable => false;

        /// <summary>
        /// Constructor for the INDEX security
        /// </summary>
        /// <param name="exchangeHours">Defines the hours this exchange is open</param>
        /// <param name="quoteCurrency">The cash object that represent the quote currency</param>
        /// <param name="config">The subscription configuration for this security</param>
        /// <param name="symbolProperties">The symbol properties for this security</param>
        /// <param name="currencyConverter">Currency converter used to convert <see cref="CashAmount"/>
        /// instances into units of the account currency</param>
        /// <param name="registeredTypes">Provides all data types registered in the algorithm</param>
        public Index(SecurityExchangeHours exchangeHours,
            Cash quoteCurrency,
            SubscriptionDataConfig config,
            SymbolProperties symbolProperties,
            ICurrencyConverter currencyConverter,
            IRegisteredSecurityDataTypesProvider registeredTypes)
            : base(config,
                quoteCurrency,
                symbolProperties,
                new IndexExchange(exchangeHours),
                new IndexCache(),
                new SecurityPortfolioModel(),
                new ImmediateFillModel(),
                new ConstantFeeModel(0),
                new ConstantSlippageModel(0),
                new ImmediateSettlementModel(),
                Securities.VolatilityModel.Null,
                new SecurityMarginModel(50m),
                new IndexDataFilter(),
                new SecurityPriceVariationModel(),
                currencyConverter,
                registeredTypes
                )
        {
            Holdings = new IndexHolding(this, currencyConverter);
        }

        /// <summary>
        /// Constructor for the INDEX security
        /// </summary>
        /// <param name="symbol">The security's symbol</param>
        /// <param name="exchangeHours">Defines the hours this exchange is open</param>
        /// <param name="quoteCurrency">The cash object that represent the quote currency</param>
        /// <param name="symbolProperties">The symbol properties for this security</param>
        /// <param name="currencyConverter">Currency converter used to convert <see cref="CashAmount"/>
        /// instances into units of the account currency</param>
        /// <param name="registeredTypes">Provides all data types registered in the algorithm</param>
        /// <param name="securityCache">Cache to store security information</param>
        public Index(Symbol symbol,
            SecurityExchangeHours exchangeHours,
            Cash quoteCurrency,
            SymbolProperties symbolProperties,
            ICurrencyConverter currencyConverter,
            IRegisteredSecurityDataTypesProvider registeredTypes,
            SecurityCache securityCache)
            : base(symbol,
                quoteCurrency,
                symbolProperties,
                new IndexExchange(exchangeHours),
                securityCache,
                new SecurityPortfolioModel(),
                new ImmediateFillModel(),
                new ConstantFeeModel(0),
                new ConstantSlippageModel(0),
                new ImmediateSettlementModel(),
                Securities.VolatilityModel.Null,
                new SecurityMarginModel(50m),
                new IndexDataFilter(),
                new SecurityPriceVariationModel(),
                currencyConverter,
                registeredTypes
                )
        {
            Holdings = new IndexHolding(this, currencyConverter);
        }
    }
}
