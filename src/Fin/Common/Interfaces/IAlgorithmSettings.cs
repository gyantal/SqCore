using System;
using QuantConnect.Securities;

namespace QuantConnect.Interfaces
{
    /// <summary>
    ///  User settings for the algorithm which can be changed in the <see cref="IAlgorithm.Initialize"/> method
    /// </summary>
    public interface IAlgorithmSettings
    {
        /// <summary>
        /// True if should rebalance portfolio on security changes. True by default
        /// </summary>
        bool? RebalancePortfolioOnSecurityChanges { get; set; }

        /// <summary>
        /// True if should rebalance portfolio on new insights or expiration of insights. True by default
        /// </summary>
        bool? RebalancePortfolioOnInsightChanges { get; set; }

        /// <summary>
        /// The absolute maximum valid total portfolio value target percentage
        /// </summary>
        /// <remarks>This setting is currently being used to filter out undesired target percent values,
        /// caused by the IPortfolioConstructionModel implementation being used.
        /// For example rounding errors, math operations</remarks>
        decimal MaxAbsolutePortfolioTargetPercentage { get; set; }

        /// <summary>
        /// The absolute minimum valid total portfolio value target percentage
        /// </summary>
        /// <remarks>This setting is currently being used to filter out undesired target percent values,
        /// caused by the IPortfolioConstructionModel implementation being used.
        /// For example rounding errors, math operations</remarks>
        decimal MinAbsolutePortfolioTargetPercentage { get; set; }

        /// <summary>
        /// Configurable minimum order margin portfolio percentage to ignore bad orders, or orders with unrealistic sizes
        /// </summary>
        /// <remarks>Default minimum order size is $0 value</remarks>
        decimal MinimumOrderMarginPortfolioPercentage { get; set; }

        /// <summary>
        /// Gets/sets the SetHoldings buffers value.
        /// The buffer is used for orders not to be rejected due to volatility when using SetHoldings and CalculateOrderQuantity
        /// </summary>
        decimal FreePortfolioValue { get; set; }

        /// <summary>
        /// Gets/sets the SetHoldings buffers value percentage.
        /// This percentage will be used to set the <see cref="FreePortfolioValue"/>
        /// based on the <see cref="SecurityPortfolioManager.TotalPortfolioValue"/>
        /// </summary>
        decimal FreePortfolioValuePercentage { get; set; }

        /// <summary>
        /// Gets/sets if Liquidate() is enabled
        /// </summary>
        bool LiquidateEnabled { get; set; }

        /// <summary>
        /// Gets/sets the maximum number of concurrent market data subscriptions available
        /// </summary>
        /// <remarks>
        /// All securities added with <see cref="IAlgorithm.AddSecurity"/> are counted as one,
        /// with the exception of options and futures where every single contract in a chain counts as one.
        /// </remarks>
        int DataSubscriptionLimit { get; set; }

        /// <summary>
        /// Gets the minimum time span elapsed to consider a market fill price as stale (defaults to one hour)
        /// </summary>
        TimeSpan StalePriceTimeSpan { get; set; }

        /// <summary>
        /// The warmup resolution to use if any
        /// </summary>
        /// <remarks>This allows improving the warmup speed by setting it to a lower resolution than the one added in the algorithm</remarks>
        Resolution? WarmupResolution { get; set; }
    }
}
