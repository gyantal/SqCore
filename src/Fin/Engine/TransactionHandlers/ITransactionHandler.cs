using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using QuantConnect.Interfaces;
using QuantConnect.Lean.Engine.Results;
using QuantConnect.Orders;
using QuantConnect.Securities;

namespace QuantConnect.Lean.Engine.TransactionHandlers
{
    /// <summary>
    /// Transaction handlers define how the transactions are processed and set the order fill information.
    /// The pass this information back to the algorithm portfolio and ensure the cash and portfolio are synchronized.
    /// </summary>
    [InheritedExport(typeof(ITransactionHandler))]
    public interface ITransactionHandler : IOrderProcessor, IOrderEventProvider
    {
        /// <summary>
        /// Boolean flag indicating the thread is busy.
        /// False indicates it is completely finished processing and ready to be terminated.
        /// </summary>
        bool IsActive
        {
            get;
        }

        /// <summary>
        /// Gets the permanent storage for all orders
        /// </summary>
        ConcurrentDictionary<int, Order> Orders
        {
            get;
        }

        /// <summary>
        /// Gets all order events
        /// </summary>
        IEnumerable<OrderEvent> OrderEvents { get; }

        /// <summary>
        /// Gets the permanent storage for all order tickets
        /// </summary>
        ConcurrentDictionary<int, OrderTicket> OrderTickets
        {
            get;
        }

        /// <summary>
        /// Initializes the transaction handler for the specified algorithm using the specified brokerage implementation
        /// </summary>
        void Initialize(IAlgorithm algorithm, IBrokerage brokerage, IResultHandler resultHandler);

        /// <summary>
        /// Signal a end of thread request to stop montioring the transactions.
        /// </summary>
        void Exit();

        /// <summary>
        /// Process any synchronous events from the primary algorithm thread.
        /// </summary>
        void ProcessSynchronousEvents();

        /// <summary>
        /// Register an already open Order
        /// </summary>
        void AddOpenOrder(Order order, OrderTicket orderTicket);
    }
}
