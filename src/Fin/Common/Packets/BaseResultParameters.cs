using System;
using QuantConnect.Orders;
using System.Collections.Generic;

namespace QuantConnect.Packets
{
    /// <summary>
    /// Base parameters used by <see cref="LiveResultParameters"/> and <see cref="BacktestResultParameters"/>
    /// </summary>
    public class BaseResultParameters
    {
        /// <summary>
        /// Contains population averages scores over the life of the algorithm
        /// </summary>
        public AlphaRuntimeStatistics AlphaRuntimeStatistics { get; set; }

        /// <summary>
        /// Trade profit and loss information since the last algorithm result packet
        /// </summary>
        public IDictionary<DateTime, decimal> ProfitLoss { get; set; }

        /// <summary>
        /// Charts updates for the live algorithm since the last result packet
        /// </summary>
        public IDictionary<string, Chart> Charts { get; set; }

        /// <summary>
        /// Order updates since the last result packet
        /// </summary>
        public IDictionary<int, Order> Orders { get; set; }

        /// <summary>
        /// Order events updates since the last result packet
        /// </summary>
        public List<OrderEvent> OrderEvents { get; set; }

        /// <summary>
        /// Statistics information sent during the algorithm operations.
        /// </summary>
        public IDictionary<string, string> Statistics { get; set; }

        /// <summary>
        /// Runtime banner/updating statistics in the title banner of the live algorithm GUI.
        /// </summary>
        public IDictionary<string, string> RuntimeStatistics { get; set; }

        /// <summary>
        /// The algorithm's configuration required for report generation
        /// </summary>
        public AlgorithmConfiguration AlgorithmConfiguration { get; set; }
    }
}
