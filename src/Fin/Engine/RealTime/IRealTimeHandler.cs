using System;
using System.ComponentModel.Composition;
using QuantConnect.Data.UniverseSelection;
using QuantConnect.Interfaces;
using QuantConnect.Lean.Engine.Results;
using QuantConnect.Packets;
using QuantConnect.Scheduling;

namespace QuantConnect.Lean.Engine.RealTime
{
    /// <summary>
    /// Real time event handler, trigger functions at regular or pretimed intervals
    /// </summary>
    [InheritedExport(typeof(IRealTimeHandler))]
    public interface IRealTimeHandler : IEventSchedule
    {
        /// <summary>
        /// Thread status flag.
        /// </summary>
        bool IsActive
        {
            get;
        }

        // SqCore Change NEW:
        void Initialize();
        // SqCore Change END
        
        /// <summary>
        /// Initializes the real time handler for the specified algorithm and job
        /// </summary>
        void Setup(IAlgorithm algorithm, AlgorithmNodePacket job, IResultHandler resultHandler, IApi api, IIsolatorLimitResultProvider isolatorLimitProvider);

        /// <summary>
        /// Set the current time for the event scanner (so we can use same code for backtesting and live events)
        /// </summary>
        /// <param name="time">Current real or backtest time.</param>
        void SetTime(DateTime time);

        /// <summary>
        /// Scan for past events that didn't fire because there was no data at the scheduled time.
        /// </summary>
        /// <param name="time">Current time.</param>
        void ScanPastEvents(DateTime time);

        /// <summary>
        /// Trigger and exit signal to terminate real time event scanner.
        /// </summary>
        void Exit();

        /// <summary>
        /// Event fired each time that we add/remove securities from the data feed
        /// </summary>
        void OnSecuritiesChanged(SecurityChanges changes);
    }
}
