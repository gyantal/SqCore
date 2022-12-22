using System;
using System.Collections.Generic;
using QuantConnect.Algorithm.Framework.Portfolio;

namespace QuantConnect.Algorithm.Framework.Risk
{
    /// <summary>
    /// Provides an implementation of <see cref="IRiskManagementModel"/> that limits the drawdown
    /// per holding to the specified percentage
    /// </summary>
    public class MaximumDrawdownPercentPerSecurity : RiskManagementModel
    {
        private readonly decimal _maximumDrawdownPercent;

        /// <summary>
        /// Initializes a new instance of the <see cref="MaximumDrawdownPercentPerSecurity"/> class
        /// </summary>
        /// <param name="maximumDrawdownPercent">The maximum percentage drawdown allowed for any single security holding,
        /// defaults to 5% drawdown per security</param>
        public MaximumDrawdownPercentPerSecurity(
            decimal maximumDrawdownPercent = 0.05m
            )
        {
            _maximumDrawdownPercent = -Math.Abs(maximumDrawdownPercent);
        }

        /// <summary>
        /// Manages the algorithm's risk at each time step
        /// </summary>
        /// <param name="algorithm">The algorithm instance</param>
        /// <param name="targets">The current portfolio targets to be assessed for risk</param>
        public override IEnumerable<IPortfolioTarget> ManageRisk(QCAlgorithm algorithm, IPortfolioTarget[] targets)
        {
            foreach (var kvp in algorithm.Securities)
            {
                var security = kvp.Value;

                if (!security.Invested)
                {
                    continue;
                }

                var pnl = security.Holdings.UnrealizedProfitPercent;
                if (pnl < _maximumDrawdownPercent)
                {
                    // liquidate
                    yield return new PortfolioTarget(security.Symbol, 0);
                }
            }
        }
    }
}