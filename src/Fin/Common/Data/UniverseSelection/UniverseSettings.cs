using System;
using System.Collections.Generic;

namespace QuantConnect.Data.UniverseSelection
{
    /// <summary>
    /// Defines settings required when adding a subscription
    /// </summary>
    public class UniverseSettings
    {
        /// <summary>
        /// The resolution to be used
        /// </summary>
        public Resolution Resolution;

        /// <summary>
        /// The leverage to be used
        /// </summary>
        public decimal Leverage;

        /// <summary>
        /// True to fill data forward, false otherwise
        /// </summary>
        public bool FillForward;

        /// <summary>
        /// True to allow extended market hours data, false otherwise
        /// </summary>
        public bool ExtendedMarketHours;

        /// <summary>
        /// Defines the minimum amount of time a security must be in
        /// the universe before being removed.
        /// </summary>
        /// <remarks>When selection takes place, the actual members time in the universe
        /// will be rounded based on this TimeSpan, so that relative small differences do not
        /// cause an unexpected behavior <see cref="Universe.CanRemoveMember"/></remarks>
        public TimeSpan MinimumTimeInUniverse;

        /// <summary>
        /// Defines how universe data is normalized before being send into the algorithm
        /// </summary>
        public DataNormalizationMode DataNormalizationMode;

        /// <summary>
        /// Defines how universe data is mapped together
        /// </summary>
        /// <remarks>This is particular useful when generating continuous futures</remarks>
        public DataMappingMode DataMappingMode;

        /// <summary>
        /// The continuous contract desired offset from the current front month.
        /// For example, 0 (default) will use the front month, 1 will use the back month contra
        /// </summary>
        public int ContractDepthOffset;

        /// <summary>
        /// Allows a universe to specify which data types to add for a selected symbol
        /// </summary>
        public List<Tuple<Type, TickType>> SubscriptionDataTypes;

        /// <summary>
        /// Initializes a new instance of the <see cref="UniverseSettings"/> class
        /// </summary>
        /// <param name="resolution">The resolution</param>
        /// <param name="leverage">The leverage to be used</param>
        /// <param name="fillForward">True to fill data forward, false otherwise</param>
        /// <param name="extendedMarketHours">True to allow extended market hours data, false otherwise</param>
        /// <param name="minimumTimeInUniverse">Defines the minimum amount of time a security must remain in the universe before being removed</param>
        /// <param name="dataNormalizationMode">Defines how universe data is normalized before being send into the algorithm</param>
        /// <param name="dataMappingMode">The contract mapping mode to use for the security</param>
        /// <param name="contractDepthOffset">The continuous contract desired offset from the current front month.
        /// For example, 0 (default) will use the front month, 1 will use the back month contract</param>
        public UniverseSettings(Resolution resolution, decimal leverage, bool fillForward, bool extendedMarketHours, TimeSpan minimumTimeInUniverse, DataNormalizationMode dataNormalizationMode = DataNormalizationMode.Adjusted,
            DataMappingMode dataMappingMode = DataMappingMode.OpenInterest, int contractDepthOffset = 0)
        {
            Resolution = resolution;
            Leverage = leverage;
            FillForward = fillForward;
            DataMappingMode = dataMappingMode;
            ContractDepthOffset = contractDepthOffset;
            ExtendedMarketHours = extendedMarketHours;
            MinimumTimeInUniverse = minimumTimeInUniverse;
            DataNormalizationMode = dataNormalizationMode;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="UniverseSettings"/> class
        /// </summary>
        public UniverseSettings(UniverseSettings universeSettings)
        {
            Resolution = universeSettings.Resolution;
            Leverage = universeSettings.Leverage;
            FillForward = universeSettings.FillForward;
            DataMappingMode = universeSettings.DataMappingMode;
            ContractDepthOffset = universeSettings.ContractDepthOffset;
            ExtendedMarketHours = universeSettings.ExtendedMarketHours;
            MinimumTimeInUniverse = universeSettings.MinimumTimeInUniverse;
            DataNormalizationMode = universeSettings.DataNormalizationMode;
            SubscriptionDataTypes = universeSettings.SubscriptionDataTypes;
        }
    }
}
