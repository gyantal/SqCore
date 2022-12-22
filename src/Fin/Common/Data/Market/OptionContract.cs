using QuantConnect.Interfaces;
using QuantConnect.Securities;
using QuantConnect.Securities.Option;
using System;

namespace QuantConnect.Data.Market
{
    /// <summary>
    /// Defines a single option contract at a specific expiration and strike price
    /// </summary>
    public class OptionContract
    {
        private Lazy<OptionPriceModelResult> _optionPriceModelResult = new(() => OptionPriceModelResult.None);

        /// <summary>
        /// Gets the option contract's symbol
        /// </summary>
        public Symbol Symbol
        {
            get; private set;
        }

        /// <summary>
        /// Gets the underlying security's symbol
        /// </summary>
        public Symbol UnderlyingSymbol
        {
            get; private set;
        }

        /// <summary>
        /// Gets the strike price
        /// </summary>
        public decimal Strike => Symbol.ID.StrikePrice;

        /// <summary>
        /// Gets the expiration date
        /// </summary>
        public DateTime Expiry => Symbol.ID.Date;

        /// <summary>
        /// Gets the right being purchased (call [right to buy] or put [right to sell])
        /// </summary>
        public OptionRight Right => Symbol.ID.OptionRight;

        /// <summary>
        /// Gets the option style
        /// </summary>
        public OptionStyle Style => Symbol.ID.OptionStyle;

        /// <summary>
        /// Gets the theoretical price of this option contract as computed by the <see cref="IOptionPriceModel"/>
        /// </summary>
        public decimal TheoreticalPrice => _optionPriceModelResult.Value.TheoreticalPrice;

        /// <summary>
        /// Gets the implied volatility of the option contract as computed by the <see cref="IOptionPriceModel"/>
        /// </summary>
        public decimal ImpliedVolatility => _optionPriceModelResult.Value.ImpliedVolatility;

        /// <summary>
        /// Gets the greeks for this contract
        /// </summary>
        public Greeks Greeks => _optionPriceModelResult.Value.Greeks;

        /// <summary>
        /// Gets the local date time this contract's data was last updated
        /// </summary>
        public DateTime Time
        {
            get; set;
        }

        /// <summary>
        /// Gets the open interest
        /// </summary>
        public decimal OpenInterest
        {
            get; set;
        }

        /// <summary>
        /// Gets the last price this contract traded at
        /// </summary>
        public decimal LastPrice
        {
            get; set;
        }

        /// <summary>
        /// Gets the last volume this contract traded at
        /// </summary>
        public long Volume
        {
            get; set;
        }

        /// <summary>
        /// Gets the current bid price
        /// </summary>
        public decimal BidPrice
        {
            get; set;
        }

        /// <summary>
        /// Get the current bid size
        /// </summary>
        public long BidSize
        {
            get; set;
        }

        /// <summary>
        /// Gets the ask price
        /// </summary>
        public decimal AskPrice
        {
            get; set;
        }

        /// <summary>
        /// Gets the current ask size
        /// </summary>
        public long AskSize
        {
            get; set;
        }

        /// <summary>
        /// Gets the last price the underlying security traded at
        /// </summary>
        public decimal UnderlyingLastPrice
        {
            get; set;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="OptionContract"/> class
        /// </summary>
        /// <param name="symbol">The option contract symbol</param>
        /// <param name="underlyingSymbol">The symbol of the underlying security</param>
        public OptionContract(Symbol symbol, Symbol underlyingSymbol)
        {
            Symbol = symbol;
            UnderlyingSymbol = underlyingSymbol;
        }

        /// <summary>
        /// Sets the option price model evaluator function to be used for this contract
        /// </summary>
        /// <param name="optionPriceModelEvaluator">Function delegate used to evaluate the option price model</param>
        internal void SetOptionPriceModel(Func<OptionPriceModelResult> optionPriceModelEvaluator)
        {
            _optionPriceModelResult = new Lazy<OptionPriceModelResult>(optionPriceModelEvaluator);
        }

        /// <summary>
        /// Returns a string that represents the current object.
        /// </summary>
        /// <returns>
        /// A string that represents the current object.
        /// </returns>
        public override string ToString() => Symbol.Value;

        /// <summary>
        /// Creates a <see cref="OptionContract"/> 
        /// </summary>
        /// <param name="baseData"></param>
        /// <param name="security">provides price properties for a <see cref="Security"/></param>
        /// <param name="underlyingLastPrice">last price the underlying security traded at</param>
        /// <returns>Option contract</returns>
        public static OptionContract Create(BaseData baseData, ISecurityPrice security, decimal underlyingLastPrice)
            => Create(baseData.Symbol, baseData.Symbol.Underlying, baseData.EndTime, security, underlyingLastPrice);

        /// <summary>
        /// Creates a <see cref="OptionContract"/>
        /// </summary>
        /// <param name="symbol">The option contract symbol</param>
        /// <param name="underlyingSymbol">The symbol of the underlying security</param>
        /// <param name="endTime">local date time this contract's data was last updated</param>
        /// <param name="security">provides price properties for a <see cref="Security"/></param>
        /// <param name="underlyingLastPrice">last price the underlying security traded at</param>
        /// <returns>Option contract</returns>
        public static OptionContract Create(Symbol symbol, Symbol underlyingSymbol, DateTime endTime, ISecurityPrice security, decimal underlyingLastPrice)
        {
            return new OptionContract(symbol, underlyingSymbol)
            {
                Time = endTime,
                LastPrice = security.Close,
                Volume = (long)security.Volume,
                BidPrice = security.BidPrice,
                BidSize = (long)security.BidSize,
                AskPrice = security.AskPrice,
                AskSize = (long)security.AskSize,
                OpenInterest = security.OpenInterest,
                UnderlyingLastPrice = underlyingLastPrice
            };
        }
    }
}
