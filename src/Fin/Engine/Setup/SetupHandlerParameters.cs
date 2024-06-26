using QuantConnect.Interfaces;
using QuantConnect.Lean.Engine.DataFeeds;
using QuantConnect.Lean.Engine.RealTime;
using QuantConnect.Lean.Engine.Results;
using QuantConnect.Lean.Engine.TransactionHandlers;
using QuantConnect.Packets;

namespace QuantConnect.Lean.Engine.Setup
{
    /// <summary>
    /// Defines the parameters for <see cref="ISetupHandler"/>
    /// </summary>
    public class SetupHandlerParameters
    {
        /// <summary>
        /// Gets the universe selection
        /// </summary>
        public UniverseSelection UniverseSelection { get; }

        /// <summary>
        /// Gets the algorithm
        /// </summary>
        public IAlgorithm Algorithm { get; }

        /// <summary>
        /// Gets the Brokerage
        /// </summary>
        public IBrokerage Brokerage { get; }

        /// <summary>
        /// Gets the algorithm node packet
        /// </summary>
        public AlgorithmNodePacket AlgorithmNodePacket { get; }

        /// <summary>
        /// Gets the algorithm node packet
        /// </summary>
        public IResultHandler ResultHandler { get; }

        /// <summary>
        /// Gets the TransactionHandler
        /// </summary>
        public ITransactionHandler TransactionHandler { get; }

        /// <summary>
        /// Gets the RealTimeHandler
        /// </summary>
        public IRealTimeHandler RealTimeHandler { get; }

        /// <summary>
        /// Gets the ObjectStore
        /// </summary>
        public IObjectStore ObjectStore { get; }

        /// <summary>
        /// Gets the DataCacheProvider
        /// </summary>
        public IDataCacheProvider DataCacheProvider { get; }

        /// <summary>
        /// The map file provider instance of the algorithm
        /// </summary>
        public IMapFileProvider MapFileProvider { get; }

        /// <summary>
        /// Creates a new instance
        /// </summary>
        /// <param name="universeSelection">The universe selection instance</param>
        /// <param name="algorithm">Algorithm instance</param>
        /// <param name="brokerage">New brokerage output instance</param>
        /// <param name="algorithmNodePacket">Algorithm job task</param>
        /// <param name="resultHandler">The configured result handler</param>
        /// <param name="transactionHandler">The configured transaction handler</param>
        /// <param name="realTimeHandler">The configured real time handler</param>
        /// <param name="objectStore">The configured object store</param>
        /// <param name="dataCacheProvider">The configured data cache provider</param>
        /// <param name="mapFileProvider">The map file provider</param>
        public SetupHandlerParameters(UniverseSelection universeSelection,
            IAlgorithm algorithm,
            IBrokerage brokerage,
            AlgorithmNodePacket algorithmNodePacket,
            IResultHandler resultHandler,
            ITransactionHandler transactionHandler,
            IRealTimeHandler realTimeHandler,
            IObjectStore objectStore,
            IDataCacheProvider dataCacheProvider,
            IMapFileProvider mapFileProvider
            )
        {
            UniverseSelection = universeSelection;
            Algorithm = algorithm;
            Brokerage = brokerage;
            AlgorithmNodePacket = algorithmNodePacket;
            ResultHandler = resultHandler;
            TransactionHandler = transactionHandler;
            RealTimeHandler = realTimeHandler;
            ObjectStore = objectStore;
            DataCacheProvider = dataCacheProvider;
            MapFileProvider = mapFileProvider;
        }
    }
}
