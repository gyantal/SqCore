using QuantConnect.Data;
using QuantConnect.Orders.Fees;
using QuantConnect.Orders.Fills;
using QuantConnect.Orders.Slippage;
using QuantConnect.Securities.Option;

namespace QuantConnect.Securities.IndexOption
{
    /// <summary>
    /// Index Options security
    /// </summary>
    public class IndexOption : Option.Option
    {
        /// <summary>
        /// Constructor for the index option security
        /// </summary>
        /// <param name="symbol">Symbol of the index option</param>
        /// <param name="exchangeHours">Exchange hours of the index option</param>
        /// <param name="quoteCurrency">Quoted currency of the index option</param>
        /// <param name="symbolProperties">Symbol properties of the index option</param>
        /// <param name="currencyConverter">Currency converter</param>
        /// <param name="registeredTypes">Provides all data types registered to the algorithm</param>
        /// <param name="securityCache">Cache of security objects</param>
        /// <param name="underlying">Future underlying security</param>
        /// <param name="settlementType">Settlement type for the index option. Most index options are cash-settled.</param>
        public IndexOption(Symbol symbol,
            SecurityExchangeHours exchangeHours,
            Cash quoteCurrency,
            IndexOptionSymbolProperties symbolProperties,
            ICurrencyConverter currencyConverter,
            IRegisteredSecurityDataTypesProvider registeredTypes,
            SecurityCache securityCache,
            Security underlying,
            SettlementType settlementType = SettlementType.Cash)
            : base(symbol,
                quoteCurrency,
                symbolProperties,
                new OptionExchange(exchangeHours),
                securityCache,
                new OptionPortfolioModel(),
                new ImmediateFillModel(),
                new InteractiveBrokersFeeModel(),
                new ConstantSlippageModel(0),
                new ImmediateSettlementModel(),
                Securities.VolatilityModel.Null,
                new OptionMarginModel(),
                new OptionDataFilter(),
                new SecurityPriceVariationModel(),
                currencyConverter,
                registeredTypes,
                underlying
            )
        {
            ExerciseSettlement = settlementType;
        }

        /// <summary>
        /// Consumes market price data and updates the minimum price variation
        /// </summary>
        /// <param name="data">Market price data</param>
        /// <remarks>
        /// Index options have variable sized minimum price variations.
        /// For prices greater than or equal to $3.00 USD, the minimum price variation is $0.10 USD.
        /// For prices less than $3.00 USD, the minimum price variation is $0.05 USD.
        /// </remarks>
        protected override void UpdateConsumersMarketPrice(BaseData data)
        {
            base.UpdateConsumersMarketPrice(data);
            ((IndexOptionSymbolProperties)SymbolProperties).UpdateMarketPrice(data);
        }
    }
}
