using System;
using QuantConnect.Interfaces;
using QuantConnect.Securities;
using System.Collections.Generic;
using QuantConnect.Securities.Positions;
using static QuantConnect.StringExtensions;

namespace QuantConnect.Algorithm.Framework.Portfolio
{
    /// <summary>
    /// Provides an implementation of <see cref="IPortfolioTarget"/> that specifies a
    /// specified quantity of a security to be held by the algorithm
    /// </summary>
    public class PortfolioTarget : IPortfolioTarget
    {
        /// <summary>
        /// Gets the symbol of this target
        /// </summary>
        public Symbol Symbol { get; }

        /// <summary>
        /// Gets the target quantity for the symbol
        /// </summary>
        public decimal Quantity { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="PortfolioTarget"/> class
        /// </summary>
        /// <param name="symbol">The symbol this target is for</param>
        /// <param name="quantity">The target quantity</param>
        public PortfolioTarget(Symbol symbol, decimal quantity)
        {
            Symbol = symbol;
            Quantity = quantity;
        }

        /// <summary>
        /// Creates a new target for the specified percent
        /// </summary>
        /// <param name="algorithm">The algorithm instance, used for getting total portfolio value and current security price</param>
        /// <param name="symbol">The symbol the target is for</param>
        /// <param name="percent">The requested target percent of total portfolio value</param>
        /// <returns>A portfolio target for the specified symbol/percent</returns>
        public static IPortfolioTarget Percent(IAlgorithm algorithm, Symbol symbol, double percent)
        {
            return Percent(algorithm, symbol, percent.SafeDecimalCast());
        }

        /// <summary>
        /// Creates a new target for the specified percent
        /// </summary>
        /// <param name="algorithm">The algorithm instance, used for getting total portfolio value and current security price</param>
        /// <param name="symbol">The symbol the target is for</param>
        /// <param name="percent">The requested target percent of total portfolio value</param>
        /// <param name="returnDeltaQuantity">True, result quantity will be the Delta required to reach target percent.
        /// False, the result quantity will be the Total quantity to reach the target percent, including current holdings</param>
        /// <returns>A portfolio target for the specified symbol/percent</returns>
        public static IPortfolioTarget Percent(IAlgorithm algorithm, Symbol symbol, decimal percent, bool returnDeltaQuantity = false)
        {
            var absolutePercentage = Math.Abs(percent);
            if (absolutePercentage > algorithm.Settings.MaxAbsolutePortfolioTargetPercentage
                || absolutePercentage != 0 && absolutePercentage < algorithm.Settings.MinAbsolutePortfolioTargetPercentage)
            {
                algorithm.Error(
                    Invariant($"The portfolio target percent: {percent}, does not comply with the current ") +
                    Invariant($"'Algorithm.Settings' 'MaxAbsolutePortfolioTargetPercentage': {algorithm.Settings.MaxAbsolutePortfolioTargetPercentage}") +
                    Invariant($" or 'MinAbsolutePortfolioTargetPercentage': {algorithm.Settings.MinAbsolutePortfolioTargetPercentage}. Skipping")
                );
                return null;
            }

            Security security;
            try
            {
                security = algorithm.Securities[symbol];
            }
            catch (KeyNotFoundException)
            {
                algorithm.Error(Invariant($"{symbol} not found in portfolio. Request this data when initializing the algorithm."));
                return null;
            }

            if (security.Price == 0)
            {
                algorithm.Error(symbol.GetZeroPriceMessage());
                return null;
            }

            // Factoring in FreePortfolioValuePercentage.
            var adjustedPercent = percent * (algorithm.Portfolio.TotalPortfolioValue - algorithm.Settings.FreePortfolioValue)
                                  / algorithm.Portfolio.TotalPortfolioValue;

            // we normalize the target buying power by the leverage so we work in the land of margin
            var targetFinalMarginPercentage = adjustedPercent / security.BuyingPowerModel.GetLeverage(security);

            var positionGroup = algorithm.Portfolio.Positions.GetOrCreateDefaultGroup(security);
            var result = positionGroup.BuyingPowerModel.GetMaximumLotsForTargetBuyingPower(
                new GetMaximumLotsForTargetBuyingPowerParameters(algorithm.Portfolio, positionGroup,
                    targetFinalMarginPercentage, algorithm.Settings.MinimumOrderMarginPortfolioPercentage));

            if (result.IsError)
            {
                algorithm.Error(Invariant(
                    $"Unable to compute order quantity of {symbol}. Reason: {result.Reason} Returning null."
                ));

                return null;
            }

            // be sure to back out existing holdings quantity since the buying power model yields
            // the required delta quantity to reach a final target portfolio value for a symbol
            var lotSize = security.SymbolProperties.LotSize;
            var quantity = result.NumberOfLots * lotSize + (returnDeltaQuantity ? 0 : security.Holdings.Quantity);

            return new PortfolioTarget(symbol, quantity);
        }

        /// <summary>Returns a string that represents the current object.</summary>
        /// <returns>A string that represents the current object.</returns>
        /// <filterpriority>2</filterpriority>
        public override string ToString()
        {
            return $"{Symbol}: {Quantity.Normalize()}";
        }
    }
}
