using System;
using System.Collections.Generic;
using System.Linq;
using SqCommon;

namespace QuantConnect.Scheduling
{
    /// <summary>
    /// Real time self scheduling event
    /// </summary>
    public class ScheduledEvent : IDisposable
    {
        /// <summary>
        /// Gets the default time before market close end of trading day events will fire
        /// </summary>
        public static readonly TimeSpan SecurityEndOfDayDelta = TimeSpan.FromMinutes(10);

        /// <summary>
        /// Gets the default time before midnight end of day events will fire
        /// </summary>
        public static readonly TimeSpan AlgorithmEndOfDayDelta = TimeSpan.FromMinutes(2);

        private bool _needsMoveNext;
        private bool _endOfScheduledEvents;
        private readonly Action<string, DateTime> _callback;
        private readonly IEnumerator<DateTime> _orderedEventUtcTimes;

        /// <summary>
        /// Event that fires each time this scheduled event happens
        /// </summary>
        public event Action<string, DateTime> EventFired;

        /// <summary>
        /// Gets or sets whether this event is enabled
        /// </summary>
        public bool Enabled
        {
            get; set;
        }

        /// <summary>
        /// Gets or sets whether this event will log each time it fires
        /// </summary>
        internal bool IsLoggingEnabled
        {
            get; set;
        }

        /// <summary>
        /// Gets the next time this scheduled event will fire in UTC
        /// </summary>
        public DateTime NextEventUtcTime
        {
            get
            {
                if (_endOfScheduledEvents)
                {
                    return DateTime.MaxValue;
                }

                if (_needsMoveNext)
                {
                    _needsMoveNext = false;
                    _endOfScheduledEvents = !_orderedEventUtcTimes.MoveNext();
                    return NextEventUtcTime;
                }
                return _orderedEventUtcTimes.Current;
            }
        }

        /// <summary>
        /// Gets an identifier for this event
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="ScheduledEvent"/> class
        /// </summary>
        /// <param name="name">An identifier for this event</param>
        /// <param name="eventUtcTime">The date time the event should fire</param>
        /// <param name="callback">Delegate to be called when the event time passes</param>
        public ScheduledEvent(string name, DateTime eventUtcTime, Action<string, DateTime> callback = null)
            : this(name, new[] { eventUtcTime }.AsEnumerable().GetEnumerator(), callback)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ScheduledEvent"/> class
        /// </summary>
        /// <param name="name">An identifier for this event</param>
        /// <param name="orderedEventUtcTimes">An enumerable that emits event times</param>
        /// <param name="callback">Delegate to be called each time an event passes</param>
        public ScheduledEvent(string name, IEnumerable<DateTime> orderedEventUtcTimes, Action<string, DateTime> callback = null)
            : this(name, orderedEventUtcTimes.GetEnumerator(), callback)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ScheduledEvent"/> class
        /// </summary>
        /// <param name="name">An identifier for this event</param>
        /// <param name="orderedEventUtcTimes">An enumerator that emits event times</param>
        /// <param name="callback">Delegate to be called each time an event passes</param>
        public ScheduledEvent(string name, IEnumerator<DateTime> orderedEventUtcTimes, Action<string, DateTime> callback = null)
        {
            Name = name;
            Enabled = true;
            _callback = callback;
            // we don't move next until we are requested, this allows the algorithm to support the warmup period correctly
            _needsMoveNext = true;
            _orderedEventUtcTimes = orderedEventUtcTimes;

        }

        /// <summary>Serves as the default hash function. </summary>
        /// <returns>A hash code for the current object.</returns>
        /// <filterpriority>2</filterpriority>
        public override int GetHashCode()
        {
            return Name.GetHashCode();
        }

        /// <summary>Determines whether the specified object is equal to the current object.</summary>
        /// <returns>true if the specified object  is equal to the current object; otherwise, false.</returns>
        /// <param name="obj">The object to compare with the current object. </param>
        /// <filterpriority>2</filterpriority>
        public override bool Equals(object obj)
        {
            return !ReferenceEquals(null, obj) && ReferenceEquals(this, obj);
        }

        /// <summary>
        /// Scans this event and fires the callback if an event happened
        /// </summary>
        /// <param name="utcTime">The current time in UTC</param>
        internal void Scan(DateTime utcTime)
        {
            if (_endOfScheduledEvents)
            {
                return;
            }

            do
            {
                if (_needsMoveNext)
                {
                    // if we've passed an event or are just priming the pump, we need to move next
                    if (!_orderedEventUtcTimes.MoveNext())
                    {
                        if (IsLoggingEnabled)
                        {
                            Utils.Logger.Trace($"ScheduledEvent.{Name}: Completed scheduled events.");
                        }
                        _endOfScheduledEvents = true;
                        return;
                    }
                    if (IsLoggingEnabled)
                    {
                        Utils.Logger.Trace($"ScheduledEvent.{Name}: Next event: {_orderedEventUtcTimes.Current.ToStringInvariant(DateFormat.UI)} UTC");
                    }
                }

                // if time has passed our event
                if (utcTime >= _orderedEventUtcTimes.Current)
                {
                    if (IsLoggingEnabled)
                    {
                        Utils.Logger.Trace($"ScheduledEvent.{Name}: Firing at {utcTime.ToStringInvariant(DateFormat.UI)} UTC " +
                            $"Scheduled at {_orderedEventUtcTimes.Current.ToStringInvariant(DateFormat.UI)} UTC"
                        );
                    }
                    // fire the event
                    OnEventFired(_orderedEventUtcTimes.Current);
                    _needsMoveNext = true;
                }
                else
                {
                    // we haven't passed the event time yet, so keep waiting on this Current
                    _needsMoveNext = false;
                }
            }
            // keep checking events until we pass the current time, this will fire
            // all 'skipped' events back to back in order, perhaps this should be handled
            // in the real time handler
            while (_needsMoveNext);
        }

        /// <summary>
        /// Fast forwards this schedule to the specified time without invoking the events
        /// </summary>
        /// <param name="utcTime">Frontier time</param>
        internal void SkipEventsUntil(DateTime utcTime)
        {
            do
            {
                // zoom through the enumerator until we get to the desired time
                if (utcTime <= NextEventUtcTime)
                {
                    if (IsLoggingEnabled)
                    {
                        Utils.Logger.Trace($"ScheduledEvent.{Name}: Skipped events before {utcTime.ToStringInvariant(DateFormat.UI)}. " +
                            $"Next event: {_orderedEventUtcTimes.Current.ToStringInvariant(DateFormat.UI)}"
                        );
                    }
                    return;
                }
            }
            while (_orderedEventUtcTimes.MoveNext());

            if (IsLoggingEnabled)
            {
                Utils.Logger.Trace($"ScheduledEvent.{Name}: Exhausted event stream during skip until {utcTime.ToStringInvariant(DateFormat.UI)}");
            }
            _endOfScheduledEvents = true;
        }

        /// <summary>
        /// Will return the ScheduledEvents name
        /// </summary>
        public override string ToString()
        {
            return $"{Name}";
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        /// <filterpriority>2</filterpriority>
        void IDisposable.Dispose()
        {
            _orderedEventUtcTimes.Dispose();
        }

        /// <summary>
        /// Event invocator for the <see cref="EventFired"/> event
        /// </summary>
        /// <param name="triggerTime">The event's time in UTC</param>
        protected void OnEventFired(DateTime triggerTime)
        {
            // don't fire the event if we're turned off
            if (!Enabled) return;

            _callback?.Invoke(Name, _orderedEventUtcTimes.Current);
            EventFired?.Invoke(Name, triggerTime);
        }
    }
}
