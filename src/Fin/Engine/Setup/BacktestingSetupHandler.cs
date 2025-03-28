using System;
using QuantConnect.Util;
using QuantConnect.Packets;
using QuantConnect.Algorithm;
using QuantConnect.Interfaces;
using QuantConnect.Configuration;
using System.Collections.Generic;
using QuantConnect.AlgorithmFactory;
using QuantConnect.Lean.Engine.DataFeeds;
using QuantConnect.Brokerages.Backtesting;
using SqCommon;
using QuantConnect.Parameters;
using QuantConnect.Algorithm.CSharp;

namespace QuantConnect.Lean.Engine.Setup
{
    /// <summary>
    /// Backtesting setup handler processes the algorithm initialize method and sets up the internal state of the algorithm class.
    /// </summary>
    public class BacktestingSetupHandler : ISetupHandler
    {
        /// <summary>
        /// The worker thread instance the setup handler should use
        /// </summary>
        public WorkerThread WorkerThread { get; set; }

        /// <summary>
        /// Internal errors list from running the setup procedures.
        /// </summary>
        public List<Exception> Errors { get; set; }

        /// <summary>
        /// Maximum runtime of the algorithm in seconds.
        /// </summary>
        /// <remarks>Maximum runtime is a formula based on the number and resolution of symbols requested, and the days backtesting</remarks>
        public TimeSpan MaximumRuntime { get; protected set; }

        /// <summary>
        /// Starting capital according to the users initialize routine.
        /// </summary>
        /// <remarks>Set from the user code.</remarks>
        /// <seealso cref="QCAlgorithm.SetCash(decimal)"/>
        public decimal StartingPortfolioValue { get; protected set; }

        /// <summary>
        /// Start date for analysis loops to search for data.
        /// </summary>
        /// <seealso cref="QCAlgorithm.SetStartDate(DateTime)"/>
        public DateTime StartingDate { get; protected set; }

        /// <summary>
        /// Maximum number of orders for this backtest.
        /// </summary>
        /// <remarks>To stop algorithm flooding the backtesting system with hundreds of megabytes of order data we limit it to 100 per day</remarks>
        public int MaxOrders { get; protected set; }

        /// <summary>
        /// Initialize the backtest setup handler.
        /// </summary>
        public BacktestingSetupHandler()
        {
            MaximumRuntime = TimeSpan.FromSeconds(300);
            Errors = new List<Exception>();
            StartingDate = new DateTime(1998, 01, 01);
        }

        /// <summary>
        /// Create a new instance of an algorithm from a physical dll path.
        /// </summary>
        /// <param name="assemblyPath">The path to the assembly's location</param>
        /// <param name="algorithmNodePacket">Details of the task required</param>
        /// <returns>A new instance of IAlgorithm, or throws an exception if there was an error</returns>
        public virtual IAlgorithm CreateAlgorithmInstance(AlgorithmNodePacket algorithmNodePacket, string assemblyPath)
        {
            // SqCore Change ORIGINAL:
            // string error;
            // IAlgorithm algorithm;

            // var debugNode = algorithmNodePacket as BacktestNodePacket;
            // var debugging = debugNode != null && debugNode.IsDebugging || Config.GetBool("debugging", false);

            // if (debugging && !BaseSetupHandler.InitializeDebugging(algorithmNodePacket, WorkerThread)) // using isolator.ExecuteWithTimeLimit()
            // {
            //     throw new AlgorithmSetupException("Failed to initialize debugging");
            // }

            // // Limit load times to 90 seconds and force the assembly to have exactly one derived type
            // var loader = new Loader(debugging, algorithmNodePacket.Language, BaseSetupHandler.AlgorithmCreationTimeout, names => names.SingleOrAlgorithmTypeName(Config.Get("algorithm-type-name")), WorkerThread);
            // var complete = loader.TryCreateAlgorithmInstanceWithIsolator(assemblyPath, algorithmNodePacket.RamAllocation, out algorithm, out error); // using isolator.ExecuteWithTimeLimit()
            // if (!complete) throw new AlgorithmSetupException($"During the algorithm initialization, the following exception has occurred: {error}");

            // return algorithm;

            // SqCore Change NEW:
            // In QcCloud, the WorkerThread is used to run BaseSetupHandler.InitializeDebugging() (which is used for QcCloud debugging) and loader.TryCreateAlgorithmInstanceWithIsolator() (which loads the Algorithm from the DLL)
            // But we don't need these locally. It justs slows our processing if we create a new thread for doing nothing. We eliminate this code.

            // Original QC code reads the whole Algorithm.CSharp DLL as binary and create the Algorithm instance from that. Total waste of time.
            string algName = ((BacktestNodePacket)algorithmNodePacket).BacktestId;
            QCAlgorithm algorithm = algName switch
            {
                "BasicTemplateFrameworkAlgorithm" => new BasicTemplateFrameworkAlgorithm(),
                "SqSPYMonFriAtMoc" => new SqSPYMonFriAtMoc(),
                "SqDualMomentum" => new SqDualMomentum(),
                "SqPctAllocation" => new SqPctAllocation(),
                "SqFundamentalDataFiltered" => new SqFundamentalDataFilteredUniv(),
                "SqTradeAccumulation" => new SqTradeAccumulation(),
                "SqCxoMomentum" => new SqCxoMomentum(),
                "SqCxoValue" => new SqCxoValue(),
                "SqCxoCombined" => new SqCxoCombined(),
                _ => throw new SqException($"QcAlgorithm name '{algName}' is unrecognized."),
            };
            return algorithm;
            // SqCore Change END
        }

        /// <summary>
        /// Creates a new <see cref="BacktestingBrokerage"/> instance
        /// </summary>
        /// <param name="algorithmNodePacket">Job packet</param>
        /// <param name="uninitializedAlgorithm">The algorithm instance before Initialize has been called</param>
        /// <param name="factory">The brokerage factory</param>
        /// <returns>The brokerage instance, or throws if error creating instance</returns>
        public IBrokerage CreateBrokerage(AlgorithmNodePacket algorithmNodePacket, IAlgorithm uninitializedAlgorithm, out IBrokerageFactory factory)
        {
            factory = new BacktestingBrokerageFactory();
            var optionMarketSimulation = new BasicOptionAssignmentSimulation();
            return new BacktestingBrokerage(uninitializedAlgorithm, optionMarketSimulation);
        }

        /// <summary>
        /// Setup the algorithm cash, dates and data subscriptions as desired.
        /// </summary>
        /// <param name="parameters">The parameters object to use</param>
        /// <returns>Boolean true on successfully initializing the algorithm</returns>
        public bool Setup(SetupHandlerParameters parameters)
        {
            var algorithm = parameters.Algorithm;
            var job = parameters.AlgorithmNodePacket as BacktestNodePacket;
            if (job == null)
            {
                throw new ArgumentException("Expected BacktestNodePacket but received " + parameters.AlgorithmNodePacket.GetType().Name);
            }

            Utils.Logger.Trace($"BacktestingSetupHandler.Setup(): Setting up job: UID: {job.UserId.ToStringInvariant()}, " +
                $"PID: {job.ProjectId.ToStringInvariant()}, Version: {job.Version}, Source: {job.RequestSource}"
            );

            if (algorithm == null)
            {
                Errors.Add(new AlgorithmSetupException("Could not create instance of algorithm"));
                return false;
            }

            algorithm.Name = job.GetAlgorithmName();

            //Make sure the algorithm start date ok.
            if (job.PeriodStart == default(DateTime))
            {
                Errors.Add(new AlgorithmSetupException("Algorithm start date was never set"));
                return false;
            }

            var controls = job.Controls;

// SqCore Change NEW:

            // var isolator = new Isolator();
            // var initializeComplete = isolator.ExecuteWithTimeLimit(TimeSpan.FromMinutes(5), () =>
            // {
            try
            {
                parameters.ResultHandler.SendStatusUpdate(AlgorithmStatus.Initializing, "Initializing algorithm...");
                //Set our parameters
                algorithm.SetParameters(job.Parameters);
                algorithm.SetAvailableDataTypes(BaseSetupHandler.GetConfiguredDataFeeds());

                //Algorithm is backtesting, not live:
                algorithm.SetLiveMode(false);

                //Set the source impl for the event scheduling
                algorithm.Schedule.SetEventSchedule(parameters.RealTimeHandler);

                // set the option chain provider
                algorithm.SetOptionChainProvider(new CachingOptionChainProvider(new BacktestingOptionChainProvider(parameters.DataCacheProvider, parameters.MapFileProvider)));

                // set the future chain provider
                algorithm.SetFutureChainProvider(new CachingFutureChainProvider(new BacktestingFutureChainProvider(parameters.DataCacheProvider)));

                // set the object store
                algorithm.SetObjectStore(parameters.ObjectStore);

                // before we call initialize
                BaseSetupHandler.LoadBacktestJobAccountCurrency(algorithm, job);

                //Initialise the algorithm, get the required data:
                algorithm.Initialize();

                // set start and end date if present in the job
                if (job.PeriodStart.HasValue)
                {
                    algorithm.SetStartDate(job.PeriodStart.Value);
                }
                if (job.PeriodFinish.HasValue)
                {
                    algorithm.SetEndDate(job.PeriodFinish.Value);
                }

                // after we call initialize
                BaseSetupHandler.LoadBacktestJobCashAmount(algorithm, job);

                // finalize initialization
                algorithm.PostInitialize();
            }
            catch (Exception err)
            {
                Errors.Add(new AlgorithmSetupException("During the algorithm initialization, the following exception has occurred: ", err));
            }
            // }, controls.RamAllocation,
            //     sleepIntervalMillis: 100,  // entire system is waiting on this, so be as fast as possible
            //     workerThread: WorkerThread);

            if (Errors.Count > 0)
            {
                // if we already got an error just exit right away
                return false;
            }

            //Before continuing, detect if this is ready:
            //if (!initializeComplete) return false;

            // SqCore Change ORIGINAL:
            // var isolator = new Isolator();
            // var initializeComplete = isolator.ExecuteWithTimeLimit(TimeSpan.FromMinutes(5), () =>
            // {
            //     try
            //     {
            //         parameters.ResultHandler.SendStatusUpdate(AlgorithmStatus.Initializing, "Initializing algorithm...");
            //         //Set our parameters
            //         algorithm.SetParameters(job.Parameters);
            //         algorithm.SetAvailableDataTypes(BaseSetupHandler.GetConfiguredDataFeeds());

            //         //Algorithm is backtesting, not live:
            //         algorithm.SetLiveMode(false);

            //         //Set the source impl for the event scheduling
            //         algorithm.Schedule.SetEventSchedule(parameters.RealTimeHandler);

            //         // set the option chain provider
            //         algorithm.SetOptionChainProvider(new CachingOptionChainProvider(new BacktestingOptionChainProvider(parameters.DataCacheProvider, parameters.MapFileProvider)));

            //         // set the future chain provider
            //         algorithm.SetFutureChainProvider(new CachingFutureChainProvider(new BacktestingFutureChainProvider(parameters.DataCacheProvider)));

            //         // set the object store
            //         algorithm.SetObjectStore(parameters.ObjectStore);

            //         // before we call initialize
            //         BaseSetupHandler.LoadBacktestJobAccountCurrency(algorithm, job);

            //         //Initialise the algorithm, get the required data:
            //         algorithm.Initialize();

            //         // set start and end date if present in the job
            //         if (job.PeriodStart.HasValue)
            //         {
            //             algorithm.SetStartDate(job.PeriodStart.Value);
            //         }
            //         if (job.PeriodFinish.HasValue)
            //         {
            //             algorithm.SetEndDate(job.PeriodFinish.Value);
            //         }

            //         // after we call initialize
            //         BaseSetupHandler.LoadBacktestJobCashAmount(algorithm, job);

            //         // finalize initialization
            //         algorithm.PostInitialize();
            //     }
            //     catch (Exception err)
            //     {
            //         Errors.Add(new AlgorithmSetupException("During the algorithm initialization, the following exception has occurred: ", err));
            //     }
            // }, controls.RamAllocation,
            //     sleepIntervalMillis: 100,  // entire system is waiting on this, so be as fast as possible
            //     workerThread: WorkerThread);

            // if (Errors.Count > 0)
            // {
            //     // if we already got an error just exit right away
            //     return false;
            // }

            // //Before continuing, detect if this is ready:
            // if (!initializeComplete) return false;

            // SqCore Change END

            MaximumRuntime = TimeSpan.FromMinutes(job.Controls.MaximumRuntimeMinutes);

            BaseSetupHandler.SetupCurrencyConversions(algorithm, parameters.UniverseSelection);
            StartingPortfolioValue = algorithm.Portfolio.Cash;

            // we set the free portfolio value based on the initial total value and the free percentage value
            algorithm.Settings.FreePortfolioValue =
                algorithm.Portfolio.TotalPortfolioValue * algorithm.Settings.FreePortfolioValuePercentage;

            // Get and set maximum orders for this job
            MaxOrders = job.Controls.BacktestingMaxOrders;
            algorithm.SetMaximumOrders(MaxOrders);

            //Starting date of the algorithm:
            StartingDate = algorithm.StartDate;

            //Put into log for debugging:
            Utils.Logger.Trace("SetUp Backtesting: User: " + job.UserId + " ProjectId: " + job.ProjectId + " AlgoId: " + job.AlgorithmId);
            Utils.Logger.Trace($"Dates: Start: {algorithm.StartDate.ToStringInvariant("d")} " +
                      $"End: {algorithm.EndDate.ToStringInvariant("d")} " +
                      $"Cash: {StartingPortfolioValue.ToStringInvariant("C")} " +
                      $"MaximumRuntime: {MaximumRuntime} " +
                      $"MaxOrders: {MaxOrders}");

            // return initializeComplete;
            return true;
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        /// <filterpriority>2</filterpriority>
        public void Dispose()
        {
        }
    } // End Result Handler Thread:

} // End Namespace
