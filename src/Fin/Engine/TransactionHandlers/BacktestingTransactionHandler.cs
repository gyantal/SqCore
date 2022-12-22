using System;
using QuantConnect.Brokerages.Backtesting;
using QuantConnect.Data.Market;
using QuantConnect.Interfaces;
using QuantConnect.Lean.Engine.Results;
using QuantConnect.Orders;
using QuantConnect.Util;
using SqCommon;

namespace QuantConnect.Lean.Engine.TransactionHandlers
{
    /// <summary>
    /// This transaction handler is used for processing transactions during backtests
    /// </summary>
    public class BacktestingTransactionHandler : BrokerageTransactionHandler
    {
        // save off a strongly typed version of the brokerage
        private BacktestingBrokerage _brokerage;
        private IAlgorithm _algorithm;
        private Delistings _lastestDelistings;

        /// <summary>
        /// Gets current time UTC. This is here to facilitate testing
        /// </summary>
        protected override DateTime CurrentTimeUtc => _algorithm.UtcTime;

        /// <summary>
        /// Creates a new BacktestingTransactionHandler using the BacktestingBrokerage
        /// </summary>
        /// <param name="algorithm">The algorithm instance</param>
        /// <param name="brokerage">The BacktestingBrokerage</param>
        /// <param name="resultHandler"></param>
        public override void Initialize(IAlgorithm algorithm, IBrokerage brokerage, IResultHandler resultHandler)
        {
            if (!(brokerage is BacktestingBrokerage))
            {
                throw new ArgumentException("Brokerage must be of type BacktestingBrokerage for use wth the BacktestingTransactionHandler");
            }

            _brokerage = (BacktestingBrokerage) brokerage;
            _algorithm = algorithm;

            base.Initialize(algorithm, brokerage, resultHandler);

            // non blocking implementation
            _orderRequestQueue = new BusyCollection<OrderRequest>();
        }

        /// <summary>
        /// Processes all synchronous events that must take place before the next time loop for the algorithm
        /// </summary>
        public override void ProcessSynchronousEvents()
        {
            // we process pending order requests our selves
            Run();

            base.ProcessSynchronousEvents();

            _brokerage.SimulateMarket();
            _brokerage.Scan();

            // Run our delistings processing, only do this once a slice
            if (_algorithm.CurrentSlice != null && _algorithm.CurrentSlice.Delistings != _lastestDelistings)
            {
                _lastestDelistings = _algorithm.CurrentSlice.Delistings;
                _brokerage.ProcessDelistings(_algorithm.CurrentSlice.Delistings);
            }
        }

        /// <summary>
        /// Processes asynchronous events on the transaction handler's thread
        /// </summary>
        public override void ProcessAsynchronousEvents()
        {
            base.ProcessAsynchronousEvents();

            _brokerage.SimulateMarket();
            _brokerage.Scan();
        }

        /// <summary>
        /// For backtesting we will submit the order ourselves
        /// </summary>
        /// <param name="ticket">The <see cref="OrderTicket"/> expecting to be submitted</param>
        protected override void WaitForOrderSubmission(OrderTicket ticket)
        {
            // we submit the order request our selves
            Run();

            if (!ticket.OrderSet.WaitOne(0))
            {
                // this could happen if there was some error handling the order
                // and it was not set
                Utils.Logger.Error("BacktestingTransactionHandler.WaitForOrderSubmission(): " +
                    $"The order request (Id={ticket.OrderId}) was not submitted. " +
                    "See the OrderRequest.Response for more information");
            }
        }

        /// <summary>
        /// For backtesting order requests will be processed by the algorithm thread
        /// sequentially at <see cref="WaitForOrderSubmission"/> and <see cref="ProcessSynchronousEvents"/>
        /// </summary>
        protected override void InitializeTransactionThread()
        {
            // nop
        }
    }
}
