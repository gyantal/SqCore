using System;
using QuantConnect.Orders;
using QuantConnect.Securities;
using System.Collections.Generic;

namespace QuantConnect.Packets
{
    /// <summary>
    /// Defines the parameters for <see cref="LiveResult"/>
    /// </summary>
    public class LiveResultParameters : BaseResultParameters
    {
        /// <summary>
        /// Holdings dictionary of algorithm holdings information
        /// </summary>
        public IDictionary<string, Holding> Holdings { get; set; }

        /// <summary>
        /// Cashbook for the algorithm's live results.
        /// </summary>
        public CashBook CashBook { get; set; }

        /// <summary>
        /// Server status information, including CPU/RAM usage, ect...
        /// </summary>
        public IDictionary<string, string> ServerStatistics { get; set; }

        /// <summary>
        /// Creates a new instance
        /// </summary>
        public LiveResultParameters(IDictionary<string, Chart> charts,
            IDictionary<int, Order> orders,
            IDictionary<DateTime, decimal> profitLoss,
            IDictionary<string, Holding> holdings,
            CashBook cashBook,
            IDictionary<string, string> statistics,
            IDictionary<string, string> runtimeStatistics,
            List<OrderEvent> orderEvents,
            IDictionary<string, string> serverStatistics = null,
            AlphaRuntimeStatistics alphaRuntimeStatistics = null,
            AlgorithmConfiguration algorithmConfiguration = null)
        {
            Charts = charts;
            Orders = orders;
            ProfitLoss = profitLoss;
            Holdings = holdings;
            CashBook = cashBook;
            Statistics = statistics;
            RuntimeStatistics = runtimeStatistics;
            OrderEvents = orderEvents;
            ServerStatistics = serverStatistics ?? OS.GetServerStatistics();
            AlphaRuntimeStatistics = alphaRuntimeStatistics;
            AlgorithmConfiguration = algorithmConfiguration;
        }
    }
}
