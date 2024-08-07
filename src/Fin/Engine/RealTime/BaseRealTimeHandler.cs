using System;
using System.Threading;
using QuantConnect.Util;
using QuantConnect.Packets;
using QuantConnect.Algorithm;
using QuantConnect.Interfaces;
using QuantConnect.Scheduling;
using QuantConnect.Securities;
using System.Collections.Generic;
using System.Collections.Concurrent;
using QuantConnect.Lean.Engine.Results;
using QuantConnect.Data.UniverseSelection;
using QuantConnect.AlgorithmFactory.Python.Wrappers;

namespace QuantConnect.Lean.Engine.RealTime
{
    /// <summary>
    /// Base class for the real time handler <see cref="LiveTradingRealTimeHandler"/>
    /// and <see cref="BacktestingRealTimeHandler"/> implementations
    /// </summary>
    public abstract class BaseRealTimeHandler : IRealTimeHandler
    {
        private int _scheduledEventUniqueId;
        // For performance only add OnEndOfDay Symbol scheduled events if the method is implemented.
        // When there are many securities it adds a significant overhead
        private bool _implementsOnEndOfDaySymbol;
        private bool _implementsOnEndOfDay;

        /// <summary>
        /// Keep track of this event so we can remove it when we need to update it
        /// </summary>
        private ScheduledEvent _algorithmOnEndOfDay;

        /// <summary>
        /// Keep a separate track of these scheduled events so we can remove them
        /// if the security gets removed
        /// </summary>
        private readonly ConcurrentDictionary<Symbol, ScheduledEvent> _securityOnEndOfDay = new();

        /// <summary>
        /// The result handler instance
        /// </summary>
        private IResultHandler ResultHandler { get; set; }

        /// <summary>
        /// Thread status flag.
        /// </summary>
        public abstract bool IsActive { get; protected set; }

        /// <summary>
        /// The scheduled events container
        /// </summary>
        /// <remarks>Initialize this immediately since the Initialize method gets
        /// called after IAlgorithm.Initialize, so we want to be ready to accept
        /// events as soon as possible</remarks>
        protected ConcurrentDictionary<ScheduledEvent, int> ScheduledEvents { get; } = new();

        /// <summary>
        /// The isolator limit result provider instance
        /// </summary>
        protected IIsolatorLimitResultProvider IsolatorLimitProvider { get; private set; }

        /// <summary>
        /// The algorithm instance
        /// </summary>
        protected IAlgorithm Algorithm { get; private set; }

        /// <summary>
        /// The time monitor instance to use
        /// </summary>
        protected TimeMonitor TimeMonitor { get; private set; }

        /// <summary>
        /// Adds the specified event to the schedule
        /// </summary>
        /// <param name="scheduledEvent">The event to be scheduled, including the date/times
        /// the event fires and the callback</param>
        public abstract void Add(ScheduledEvent scheduledEvent);

        /// <summary>
        /// Removes the specified event from the schedule
        /// </summary>
        /// <param name="scheduledEvent">The event to be removed</param>
        public abstract void Remove(ScheduledEvent scheduledEvent);

        /// <summary>
        /// Set the current time for the event scanner (so we can use same code for backtesting and live events)
        /// </summary>
        /// <param name="time">Current real or backtest time.</param>
        public abstract void SetTime(DateTime time);

        /// <summary>
        /// Scan for past events that didn't fire because there was no data at the scheduled time.
        /// </summary>
        /// <param name="time">Current time.</param>
        public abstract void ScanPastEvents(DateTime time);

        /// <summary>
        /// Initializes the real time handler for the specified algorithm and job.
        /// Adds EndOfDayEvents
        /// </summary>
        public virtual void Setup(IAlgorithm algorithm, AlgorithmNodePacket job, IResultHandler resultHandler, IApi api, IIsolatorLimitResultProvider isolatorLimitProvider)
        {
            Algorithm = algorithm;
            ResultHandler = resultHandler;
            TimeMonitor = new TimeMonitor(GetTimeMonitorTimeout());
            IsolatorLimitProvider = isolatorLimitProvider;

            if (job.Language == Language.CSharp)
            {
                var method = Algorithm.GetType().GetMethod("OnEndOfDay", new[] { typeof(Symbol) });
                var method2 = Algorithm.GetType().GetMethod("OnEndOfDay", new[] { typeof(string) });
                if (method != null && method.DeclaringType != typeof(QCAlgorithm)
                    || method2 != null && method2.DeclaringType != typeof(QCAlgorithm))
                {
                    _implementsOnEndOfDaySymbol = true;
                }

                // Also determine if we are using the soon to be deprecated EOD so we don't use it
                // unnecessarily and post messages about its deprecation to the user
                var eodMethod = Algorithm.GetType().GetMethod("OnEndOfDay", Type.EmptyTypes);
                if (eodMethod != null && eodMethod.DeclaringType != typeof(QCAlgorithm))
                {
                    _implementsOnEndOfDay = true;
                }
            }
            else if (job.Language == Language.Python)
            {
                var wrapper = Algorithm as AlgorithmPythonWrapper;
                if (wrapper != null)
                {
                    _implementsOnEndOfDaySymbol = wrapper.IsOnEndOfDaySymbolImplemented;
                    _implementsOnEndOfDay = wrapper.IsOnEndOfDayImplemented;
                }
            }
            else
            {
                throw new ArgumentException(nameof(job.Language));
            }

            // Here to maintain functionality until deprecation in August 2021
            AddAlgorithmEndOfDayEvent(start: algorithm.Time, end: algorithm.EndDate, currentUtcTime: algorithm.UtcTime);
        }

        /// <summary>
        /// Gets a new scheduled event unique id
        /// </summary>
        /// <remarks>This value is used to order scheduled events in a deterministic way</remarks>
        protected int GetScheduledEventUniqueId()
        {
            return Interlocked.Increment(ref _scheduledEventUniqueId);
        }

        /// <summary>
        /// Get's the timeout the scheduled task time monitor should use
        /// </summary>
        protected virtual int GetTimeMonitorTimeout()
        {
            return 100;
        }

        /// <summary>
        /// Creates a new <see cref="ScheduledEvent"/> that will fire before market close by the specified time
        /// </summary>
        /// <param name="start">The date to start the events</param>
        /// <param name="end">The date to end the events</param>
        /// <param name="currentUtcTime">Specifies the current time in UTC, before which,
        /// no events will be scheduled. Specify null to skip this filter.</param>
        [Obsolete("This method is deprecated. It will add ScheduledEvents for the deprecated IAlgorithm.OnEndOfDay()")]
        private void AddAlgorithmEndOfDayEvent(DateTime start, DateTime end, DateTime? currentUtcTime = null)
        {
            // If the algorithm didn't implement it no need to support it.
            if (!_implementsOnEndOfDay) { return; }

            if (_algorithmOnEndOfDay != null)
            {
                // if we already set it once we remove the previous and
                // add a new one, we don't want to keep both
                Remove(_algorithmOnEndOfDay);
            }

            // add end of day events for each tradeable day
            _algorithmOnEndOfDay = ScheduledEventFactory.EveryAlgorithmEndOfDay(
                Algorithm,
                ResultHandler,
                start,
                end,
                ScheduledEvent.AlgorithmEndOfDayDelta,
                currentUtcTime);

            Add(_algorithmOnEndOfDay);
        }

        /// <summary>
        /// Creates a new <see cref="ScheduledEvent"/> that will fire before market
        /// close by the specified time for each provided securities.
        /// </summary>
        /// <param name="securities">The securities for which we want to add the OnEndOfDay event</param>
        /// <param name="start">The date to start the events</param>
        /// <param name="end">The date to end the events</param>
        /// <param name="currentUtcTime">Specifies the current time in UTC, before which,
        /// no events will be scheduled. Specify null to skip this filter.</param>
        private void AddSecurityDependentEndOfDayEvents(
            IEnumerable<Security> securities,
            DateTime start,
            DateTime end,
            DateTime? currentUtcTime = null)
        {
            if (_implementsOnEndOfDaySymbol)
            {
                // add end of trading day events for each security
                foreach (var security in securities)
                {
                    if (!security.IsInternalFeed())
                    {
                        var scheduledEvent = ScheduledEventFactory.EverySecurityEndOfDay(
                            Algorithm, ResultHandler, security, start, end, ScheduledEvent.SecurityEndOfDayDelta, currentUtcTime);

                        // we keep separate track so we can remove it later
                        _securityOnEndOfDay[security.Symbol] = scheduledEvent;

                        // assumes security.Exchange has been updated with today's hours via RefreshMarketHoursToday
                        Add(scheduledEvent);
                    }
                }
            }
        }

        /// <summary>
        /// Event fired each time that we add/remove securities from the data feed
        /// </summary>
        public void OnSecuritiesChanged(SecurityChanges changes)
        {
            if (changes != SecurityChanges.None)
            {
                if (_implementsOnEndOfDaySymbol)
                {
                    AddSecurityDependentEndOfDayEvents(changes.AddedSecurities,
                        Algorithm.UtcTime,
                        Algorithm.EndDate,
                        Algorithm.UtcTime);

                    foreach (var security in changes.RemovedSecurities)
                    {
                        ScheduledEvent scheduledEvent;
                        if (_securityOnEndOfDay.TryRemove(security.Symbol, out scheduledEvent))
                        {
                            // we remove the schedule events of the securities that were removed
                            Remove(scheduledEvent);
                        }
                    }
                }

                // we re add the algorithm end of day event because it depends on the securities
                // tradable dates
                // Here to maintain functionality until deprecation in August 2021
                AddAlgorithmEndOfDayEvent(Algorithm.UtcTime, Algorithm.EndDate, Algorithm.UtcTime);
            }
        }

        /// <summary>
        /// Stop the real time thread
        /// </summary>
        public virtual void Exit()
        {
            TimeMonitor.DisposeSafely();
            TimeMonitor = null;
        }
    }
}
