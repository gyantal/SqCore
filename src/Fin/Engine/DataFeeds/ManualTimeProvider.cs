using System;
using NodaTime;
using QuantConnect.Util;

namespace QuantConnect.Lean.Engine.DataFeeds
{
    /// <summary>
    /// Provides an implementation of <see cref="ITimeProvider"/> that can be
    /// manually advanced through time
    /// </summary>
    public class ManualTimeProvider : ITimeProvider
    {
        private volatile ReferenceWrapper<DateTime> _currentTime;
        private readonly DateTimeZone _setCurrentTimeTimeZone;

        /// <summary>
        /// Initializes a new instance of the <see cref="ManualTimeProvider"/>
        /// </summary>
        /// <param name="setCurrentTimeTimeZone">Specify to use this time zone when calling <see cref="SetCurrentTime"/>,
        /// leave null for the default of <see cref="TimeZones.Utc"/></param>
        public ManualTimeProvider(DateTimeZone setCurrentTimeTimeZone = null)
        {
            _setCurrentTimeTimeZone = setCurrentTimeTimeZone ?? TimeZones.Utc;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ManualTimeProvider"/> class
        /// </summary>
        /// <param name="currentTime">The current time in the specified time zone, if the time zone is
        /// null then the time is interpreted as being in <see cref="TimeZones.Utc"/></param>
        /// <param name="setCurrentTimeTimeZone">Specify to use this time zone when calling <see cref="SetCurrentTime"/>,
        /// leave null for the default of <see cref="TimeZones.Utc"/></param>
        public ManualTimeProvider(DateTime currentTime, DateTimeZone setCurrentTimeTimeZone = null)
            : this(setCurrentTimeTimeZone)
        {
            _currentTime = new ReferenceWrapper<DateTime>(currentTime.ConvertToUtc(_setCurrentTimeTimeZone));
        }

        /// <summary>
        /// Gets the current time in UTC
        /// </summary>
        /// <returns>The current time in UTC</returns>
        public DateTime GetUtcNow()
        {
            return _currentTime.Value;
        }

        /// <summary>
        /// Sets the current time interpreting the specified time as a UTC time
        /// </summary>
        /// <param name="time">The current time in UTC</param>
        public void SetCurrentTimeUtc(DateTime time)
        {
            _currentTime = new ReferenceWrapper<DateTime>(time);
        }

        /// <summary>
        /// Sets the current time interpeting the specified time as a local time
        /// using the time zone used at instatiation.
        /// </summary>
        /// <param name="time">The local time to set the current time time, will be
        /// converted into UTC</param>
        public void SetCurrentTime(DateTime time)
        {
            SetCurrentTimeUtc(time.ConvertToUtc(_setCurrentTimeTimeZone));
        }

        /// <summary>
        /// Advances the current time by the specified span
        /// </summary>
        /// <param name="span">The amount of time to advance the current time by</param>
        public void Advance(TimeSpan span)
        {
            _currentTime = new ReferenceWrapper<DateTime>(_currentTime.Value + span);
        }

        /// <summary>
        /// Advances the current time by the specified number of seconds
        /// </summary>
        /// <param name="seconds">The number of seconds to advance the current time by</param>
        public void AdvanceSeconds(double seconds)
        {
            Advance(TimeSpan.FromSeconds(seconds));
        }
    }
}