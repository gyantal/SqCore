using System;
using Newtonsoft.Json;
using QuantConnect.Orders;
using QuantConnect.Packets;
using System.Collections.Generic;

namespace QuantConnect
{
    /// <summary>
    /// Base class for backtesting and live results that packages result data.
    /// <see cref="LiveResult"/>
    /// <see cref="BacktestResult"/>
    /// </summary>
    public class Result
    {
        /// <summary>
        /// Contains population averages scores over the life of the algorithm
        /// </summary>
        [JsonProperty(PropertyName = "AlphaRuntimeStatistics", NullValueHandling = NullValueHandling.Ignore)]
        public AlphaRuntimeStatistics AlphaRuntimeStatistics;

        /// <summary>
        /// Charts updates for the live algorithm since the last result packet
        /// </summary>
        [JsonProperty(PropertyName = "Charts", NullValueHandling = NullValueHandling.Ignore)]
        public IDictionary<string, Chart> Charts;

        /// <summary>
        /// Order updates since the last result packet
        /// </summary>
        [JsonProperty(PropertyName = "Orders", NullValueHandling = NullValueHandling.Ignore)]
        public IDictionary<int, Order> Orders;

        /// <summary>
        /// OrderEvent updates since the last result packet
        /// </summary>
        [JsonProperty(PropertyName = "OrderEvents", NullValueHandling = NullValueHandling.Ignore)]
        public List<OrderEvent> OrderEvents;

        /// <summary>
        /// Trade profit and loss information since the last algorithm result packet
        /// </summary>
        [JsonProperty(PropertyName = "ProfitLoss", NullValueHandling = NullValueHandling.Ignore)]
        public IDictionary<DateTime, decimal> ProfitLoss;

        /// <summary>
        /// Statistics information sent during the algorithm operations.
        /// </summary>
        /// <remarks>Intended for update mode -- send updates to the existing statistics in the result GUI. If statistic key does not exist in GUI, create it</remarks>
        [JsonProperty(PropertyName = "Statistics", NullValueHandling = NullValueHandling.Ignore)]
        public IDictionary<string, string> Statistics;

        /// <summary>
        /// Runtime banner/updating statistics in the title banner of the live algorithm GUI.
        /// </summary>
        [JsonProperty(PropertyName = "RuntimeStatistics", NullValueHandling = NullValueHandling.Ignore)]
        public IDictionary<string, string> RuntimeStatistics;

        /// <summary>
        /// Server status information, including CPU/RAM usage, ect...
        /// </summary>
        [JsonProperty(PropertyName = "ServerStatistics", NullValueHandling = NullValueHandling.Ignore)]
        public IDictionary<string, string> ServerStatistics;

        /// <summary>
        /// The algorithm's configuration required for report generation
        /// </summary>
        [JsonProperty(PropertyName = "AlgorithmConfiguration", NullValueHandling = NullValueHandling.Ignore)]
        public AlgorithmConfiguration AlgorithmConfiguration;
    }
}
