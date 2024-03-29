using System.Collections.Generic;
using QuantConnect.Data;
using QuantConnect.Interfaces;
using System;

namespace QuantConnect.Securities
{
    /// <summary>
    /// This class implements interface <see cref="ISecurityService"/> providing methods for creating new <see cref="Security"/>
    /// </summary>
    public class SecurityService : ISecurityService
    {
        private readonly CashBook _cashBook;
        private readonly MarketHoursDatabase _marketHoursDatabase;
        private readonly SymbolPropertiesDatabase _symbolPropertiesDatabase;
        private readonly IRegisteredSecurityDataTypesProvider _registeredTypes;
        private readonly ISecurityInitializerProvider _securityInitializerProvider;
        private readonly SecurityCacheProvider _cacheProvider;
        private readonly IPrimaryExchangeProvider _primaryExchangeProvider;
        private bool _isLiveMode;

        /// <summary>
        /// Creates a new instance of the SecurityService class
        /// </summary>
        public SecurityService(CashBook cashBook,
            MarketHoursDatabase marketHoursDatabase,
            SymbolPropertiesDatabase symbolPropertiesDatabase,
            ISecurityInitializerProvider securityInitializerProvider,
            IRegisteredSecurityDataTypesProvider registeredTypes,
            SecurityCacheProvider cacheProvider,
            IPrimaryExchangeProvider primaryExchangeProvider=null)
        {
            _cashBook = cashBook;
            _registeredTypes = registeredTypes;
            _marketHoursDatabase = marketHoursDatabase;
            _symbolPropertiesDatabase = symbolPropertiesDatabase;
            _securityInitializerProvider = securityInitializerProvider;
            _cacheProvider = cacheProvider;
            _primaryExchangeProvider = primaryExchangeProvider;
        }

        /// <summary>
        /// Creates a new security
        /// </summary>
        /// <remarks>Following the obsoletion of Security.Subscriptions,
        /// both overloads will be merged removing <see cref="SubscriptionDataConfig"/> arguments</remarks>
        public Security CreateSecurity(Symbol symbol,
            List<SubscriptionDataConfig> subscriptionDataConfigList,
            decimal leverage = 0,
            bool addToSymbolCache = true,
            Security underlying = null)
        {
            var configList = new SubscriptionDataConfigList(symbol);
            configList.AddRange(subscriptionDataConfigList);

            var exchangeHours = _marketHoursDatabase.GetEntry(symbol.ID.Market, symbol, symbol.ID.SecurityType).ExchangeHours;

            var defaultQuoteCurrency = _cashBook.AccountCurrency;
            if (symbol.ID.SecurityType == SecurityType.Forex)
            {
                defaultQuoteCurrency = symbol.Value.Substring(3);
            }

            if (symbol.ID.SecurityType == SecurityType.Crypto && !_symbolPropertiesDatabase.ContainsKey(symbol.ID.Market, symbol, symbol.ID.SecurityType))
            {
                throw new ArgumentException($"Symbol can't be found in the Symbol Properties Database: {symbol.Value}");
            }

            // For Futures Options that don't have a SPDB entry, the futures entry will be used instead.
            var symbolProperties = _symbolPropertiesDatabase.GetSymbolProperties(
                symbol.ID.Market,
                symbol,
                symbol.SecurityType,
                defaultQuoteCurrency);

            // add the symbol to our cache
            if (addToSymbolCache)
            {
                SymbolCache.Set(symbol.Value, symbol);
            }

            // verify the cash book is in a ready state
            var quoteCurrency = symbolProperties.QuoteCurrency;
            if (!_cashBook.ContainsKey(quoteCurrency))
            {
                // since we have none it's safe to say the conversion is zero
                _cashBook.Add(quoteCurrency, 0, 0);
            }
            if (symbol.ID.SecurityType == SecurityType.Forex || symbol.ID.SecurityType == SecurityType.Crypto)
            {
                // decompose the symbol into each currency pair
                string baseCurrency;
                if (symbol.ID.SecurityType == SecurityType.Forex)
                {
                    Forex.Forex.DecomposeCurrencyPair(symbol.Value, out baseCurrency, out quoteCurrency);
                }
                else
                {
                    Crypto.Crypto.DecomposeCurrencyPair(symbol, symbolProperties, out baseCurrency, out quoteCurrency);
                }

                if (!_cashBook.ContainsKey(baseCurrency))
                {
                    // since we have none it's safe to say the conversion is zero
                    _cashBook.Add(baseCurrency, 0, 0);
                }
                if (!_cashBook.ContainsKey(quoteCurrency))
                {
                    // since we have none it's safe to say the conversion is zero
                    _cashBook.Add(quoteCurrency, 0, 0);
                }
            }

            var quoteCash = _cashBook[symbolProperties.QuoteCurrency];
            var cache = _cacheProvider.GetSecurityCache(symbol);

            Security security;
            switch (symbol.ID.SecurityType)
            {
                case SecurityType.Equity:
                    var primaryExchange =
                        _primaryExchangeProvider?.GetPrimaryExchange(symbol.ID) ??
                        Exchange.UNKNOWN;
                    security = new Equity.Equity(symbol, exchangeHours, quoteCash, symbolProperties, _cashBook, _registeredTypes, cache, primaryExchange);
                    break;

                case SecurityType.Option:
                    if (addToSymbolCache) SymbolCache.Set(symbol.Underlying.Value, symbol.Underlying);
                    security = new Option.Option(symbol, exchangeHours, quoteCash, new Option.OptionSymbolProperties(symbolProperties), _cashBook, _registeredTypes, cache, underlying);
                    break;

                case SecurityType.IndexOption:
                    if (addToSymbolCache) SymbolCache.Set(symbol.Underlying.Value, symbol.Underlying);
                    security = new IndexOption.IndexOption(symbol, exchangeHours, quoteCash, new IndexOption.IndexOptionSymbolProperties(symbolProperties), _cashBook, _registeredTypes, cache, underlying);
                    break;

                case SecurityType.FutureOption:
                    if (addToSymbolCache) SymbolCache.Set(symbol.Underlying.Value, symbol.Underlying);
                    var optionSymbolProperties = new Option.OptionSymbolProperties(symbolProperties);

                    // Future options exercised only gives us one contract back, rather than the
                    // 100x seen in equities.
                    optionSymbolProperties.SetContractUnitOfTrade(1);

                    security = new FutureOption.FutureOption(symbol, exchangeHours, quoteCash, optionSymbolProperties, _cashBook, _registeredTypes, cache, underlying);
                    break;

                case SecurityType.Future:
                    security = new Future.Future(symbol, exchangeHours, quoteCash, symbolProperties, _cashBook, _registeredTypes, cache, underlying);
                    break;

                case SecurityType.Forex:
                    security = new Forex.Forex(symbol, exchangeHours, quoteCash, symbolProperties, _cashBook, _registeredTypes, cache);
                    break;

                case SecurityType.Cfd:
                    security = new Cfd.Cfd(symbol, exchangeHours, quoteCash, symbolProperties, _cashBook, _registeredTypes, cache);
                    break;

                case SecurityType.Index:
                    security = new Index.Index(symbol, exchangeHours, quoteCash, symbolProperties, _cashBook, _registeredTypes, cache);
                    break;

                case SecurityType.Crypto:
                    security = new Crypto.Crypto(symbol, exchangeHours, quoteCash, symbolProperties, _cashBook, _registeredTypes, cache);
                    break;

                default:
                case SecurityType.Base:
                    security = new Security(symbol, exchangeHours, quoteCash, symbolProperties, _cashBook, _registeredTypes, cache);
                    break;
            }

            // if we're just creating this security and it only has an internal
            // feed, mark it as non-tradable since the user didn't request this data
            if (!configList.IsInternalFeed)
            {
                security.IsTradable = true;
            }

            security.AddData(configList);

            // invoke the security initializer
            _securityInitializerProvider.SecurityInitializer.Initialize(security);

            // if leverage was specified then apply to security after the initializer has run, parameters of this
            // method take precedence over the intializer
            if (leverage != Security.NullLeverage)
            {
                security.SetLeverage(leverage);
            }

            var isNotNormalized = configList.DataNormalizationMode() == DataNormalizationMode.Raw;

            // In live mode and non normalized data, equity assumes specific price variation model
            if ((_isLiveMode || isNotNormalized) && security.Type == SecurityType.Equity)
            {
                security.PriceVariationModel = new EquityPriceVariationModel();
            }

            return security;
        }

        /// <summary>
        /// Creates a new security
        /// </summary>
        /// <remarks>Following the obsoletion of Security.Subscriptions,
        /// both overloads will be merged removing <see cref="SubscriptionDataConfig"/> arguments</remarks>
        public Security CreateSecurity(Symbol symbol, SubscriptionDataConfig subscriptionDataConfig, decimal leverage = 0, bool addToSymbolCache = true, Security underlying = null)
        {
            return CreateSecurity(symbol, new List<SubscriptionDataConfig> { subscriptionDataConfig }, leverage, addToSymbolCache, underlying);
        }

        /// <summary>
        /// Set live mode state of the algorithm
        /// </summary>
        /// <param name="isLiveMode">True, live mode is enabled</param>
        public void SetLiveMode(bool isLiveMode)
        {
            _isLiveMode = isLiveMode;
        }
    }
}
