using System;
using QuantConnect.Orders;
using QuantConnect.Statistics;
using System.Collections.Generic;

namespace QuantConnect.Packets
{
    /// <summary>
    /// Defines the parameters for <see cref="BacktestResult"/>
    /// </summary>
    public class BacktestResultParameters : BaseResultParameters
    {
        /// <summary>
        /// Rolling window detailed statistics.
        /// </summary>
        public Dictionary<string, AlgorithmPerformance> RollingWindow { get; set; }

        /// <summary>
        /// Rolling window detailed statistics.
        /// </summary>
        public AlgorithmPerformance TotalPerformance { get; set; }

        /// <summary>
        /// Creates a new instance
        /// </summary>
        public BacktestResultParameters(IDictionary<string, Chart> charts,
            IDictionary<int, Order> orders,
            IDictionary<DateTime, decimal> profitLoss,
            IDictionary<string, string> statistics,
            IDictionary<string, string> runtimeStatistics,
            Dictionary<string, AlgorithmPerformance> rollingWindow,
            List<OrderEvent> orderEvents,
            AlgorithmPerformance totalPerformance = null,
            AlphaRuntimeStatistics alphaRuntimeStatistics = null,
            AlgorithmConfiguration algorithmConfiguration = null)
        {
            Charts = charts;
            Orders = orders;
            ProfitLoss = profitLoss;
            Statistics = statistics;
            RuntimeStatistics = runtimeStatistics;
            RollingWindow = rollingWindow;
            OrderEvents = orderEvents;
            TotalPerformance = totalPerformance;
            AlphaRuntimeStatistics = alphaRuntimeStatistics;
            AlgorithmConfiguration = algorithmConfiguration;
        }
    }
}
