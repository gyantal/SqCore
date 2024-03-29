﻿using System.Collections.Generic;
using QuantConnect.Interfaces;
using QuantConnect.Packets;
using QuantConnect.Securities;

namespace QuantConnect.Brokerages.Backtesting
{
    /// <summary>
    /// Factory type for the <see cref="BacktestingBrokerage"/>
    /// </summary>
    public class BacktestingBrokerageFactory : BrokerageFactory
    {
        /// <summary>
        /// Gets the brokerage data required to run the IB brokerage from configuration
        /// </summary>
        /// <remarks>
        /// The implementation of this property will create the brokerage data dictionary required for
        /// running live jobs. See <see cref="IJobQueueHandler.NextJob"/>
        /// </remarks>
        public override Dictionary<string, string> BrokerageData
        {
            get { return new Dictionary<string, string>(); }
        }

        /// <summary>
        /// Gets a new instance of the <see cref="InteractiveBrokersBrokerageModel"/>
        /// </summary>
        /// <param name="orderProvider">The order provider</param>
        public override IBrokerageModel GetBrokerageModel(IOrderProvider orderProvider) => new InteractiveBrokersBrokerageModel();

        /// <summary>
        /// Creates a new IBrokerage instance
        /// </summary>
        /// <param name="job">The job packet to create the brokerage for</param>
        /// <param name="algorithm">The algorithm instance</param>
        /// <returns>A new brokerage instance</returns>
        public override IBrokerage CreateBrokerage(LiveNodePacket job, IAlgorithm algorithm)
        {
            return new BacktestingBrokerage(algorithm);
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        /// <filterpriority>2</filterpriority>
        public override void Dispose()
        {
            // NOP
        }

        /// <summary>
        ///Initializes a new instance of the <see cref="BacktestingBrokerageFactory"/> class
        /// </summary>
        public BacktestingBrokerageFactory()
            : base(typeof(BacktestingBrokerage))
        {
        }
    }
}
