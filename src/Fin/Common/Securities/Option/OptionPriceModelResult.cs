using QuantConnect.Data.Market;
using System;

namespace QuantConnect.Securities.Option
{
    /// <summary>
    /// Result type for <see cref="IOptionPriceModel.Evaluate"/>
    /// </summary>
    public class OptionPriceModelResult
    {
        /// <summary>
        /// Represents the zero option price and greeks.
        /// </summary>
        public static OptionPriceModelResult None { get; } = new(0, new Greeks());

        private readonly Lazy<Greeks> _greeks;
        private readonly Lazy<decimal> _impliedVolatility;

        /// <summary>
        /// Gets the theoretical price as computed by the <see cref="IOptionPriceModel"/>
        /// </summary>
        public decimal TheoreticalPrice
        {
            get; private set;
        }

        /// <summary>
        /// Gets the implied volatility of the option contract
        /// </summary>
        public decimal ImpliedVolatility
        {
            get
            {
                return _impliedVolatility.Value;
            }
        }

        /// <summary>
        /// Gets the various sensitivities as computed by the <see cref="IOptionPriceModel"/>
        /// </summary>
        public Greeks Greeks
        {
            get
            {
                return _greeks.Value;
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="OptionPriceModelResult"/> class
        /// </summary>
        /// <param name="theoreticalPrice">The theoretical price computed by the price model</param>
        /// <param name="greeks">The sensitivities (greeks) computed by the price model</param>
        public OptionPriceModelResult(decimal theoreticalPrice, Greeks greeks)
        {
            TheoreticalPrice = theoreticalPrice;
            _impliedVolatility = new Lazy<decimal>(() => 0m);
            _greeks = new Lazy<Greeks>(() => greeks);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="OptionPriceModelResult"/> class with lazy calculations of implied volatility and greeks
        /// </summary>
        /// <param name="theoreticalPrice">The theoretical price computed by the price model</param>
        /// <param name="impliedVolatility">The calculated implied volatility</param>
        /// <param name="greeks">The sensitivities (greeks) computed by the price model</param>
        public OptionPriceModelResult(decimal theoreticalPrice, Func<decimal> impliedVolatility, Func<Greeks> greeks)
        {
            TheoreticalPrice = theoreticalPrice;
            _impliedVolatility = new Lazy<decimal>(impliedVolatility);
            _greeks = new Lazy<Greeks>(greeks);
        }
    }
}
