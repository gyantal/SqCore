using System;
using System.Collections.Generic;
using QuantConnect.Interfaces;
using QuantConnect.Packets;
using QuantConnect.Securities;

namespace QuantConnect.Brokerages
{
    /// <summary>
    /// Provides a base implementation of IBrokerageFactory that provides a helper for reading data from a job's brokerage data dictionary
    /// </summary>
    public abstract class BrokerageFactory : IBrokerageFactory
    {
        private readonly Type _brokerageType;

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        /// <filterpriority>2</filterpriority>
        public abstract void Dispose();

        /// <summary>
        /// Gets the type of brokerage produced by this factory
        /// </summary>
        public Type BrokerageType
        {
            get { return _brokerageType; }
        }

        /// <summary>
        /// Gets the brokerage data required to run the brokerage from configuration/disk
        /// </summary>
        /// <remarks>
        /// The implementation of this property will create the brokerage data dictionary required for
        /// running live jobs. See <see cref="IJobQueueHandler.NextJob"/>
        /// </remarks>
        public abstract Dictionary<string, string> BrokerageData { get; }

        /// <summary>
        /// Gets a brokerage model that can be used to model this brokerage's unique behaviors
        /// </summary>
        /// <param name="orderProvider">The order provider</param>
        public abstract IBrokerageModel GetBrokerageModel(IOrderProvider orderProvider);

        /// <summary>
        /// Creates a new IBrokerage instance
        /// </summary>
        /// <param name="job">The job packet to create the brokerage for</param>
        /// <param name="algorithm">The algorithm instance</param>
        /// <returns>A new brokerage instance</returns>
        public abstract IBrokerage CreateBrokerage(LiveNodePacket job, IAlgorithm algorithm);

        /// <summary>
        /// Gets a brokerage message handler
        /// </summary>
        public virtual IBrokerageMessageHandler CreateBrokerageMessageHandler(IAlgorithm algorithm, AlgorithmNodePacket job, IApi api)
        {
            return new DefaultBrokerageMessageHandler(algorithm, job, api);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="BrokerageFactory"/> class for the specified <paramref name="brokerageType"/>
        /// </summary>
        /// <param name="brokerageType">The type of brokerage created by this factory</param>
        protected BrokerageFactory(Type brokerageType)
        {
            _brokerageType = brokerageType;
        }

        /// <summary>
        /// Reads a value from the brokerage data, adding an error if the key is not found
        /// </summary>
        protected static T Read<T>(IReadOnlyDictionary<string, string> brokerageData, string key, ICollection<string> errors)
            where T : IConvertible
        {
            string value;
            if (!brokerageData.TryGetValue(key, out value))
            {
                errors.Add("BrokerageFactory.CreateBrokerage(): Missing key: " + key);
                return default(T);
            }

            try
            {
                return value.ConvertTo<T>();
            }
            catch (Exception err)
            {
                errors.Add($"BrokerageFactory.CreateBrokerage(): Error converting key '{key}' with value '{value}'. {err.Message}");
                return default(T);
            }
        }
    }
}