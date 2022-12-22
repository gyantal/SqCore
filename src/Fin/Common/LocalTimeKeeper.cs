using System;
using NodaTime;

namespace QuantConnect
{
    /// <summary>
    /// Represents the current local time. This object is created via the <see cref="TimeKeeper"/> to
    /// manage conversions to local time.
    /// </summary>
    public class LocalTimeKeeper
    {
        /// <summary>
        /// Event fired each time <see cref="UpdateTime"/> is called
        /// </summary>
        public event EventHandler<TimeUpdatedEventArgs> TimeUpdated;

        /// <summary>
        /// Gets the time zone of this <see cref="LocalTimeKeeper"/>
        /// </summary>
        public DateTimeZone TimeZone { get; }

        /// <summary>
        /// Gets the current time in terms of the <see cref="TimeZone"/>
        /// </summary>
        public DateTime LocalTime { get; private set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="LocalTimeKeeper"/> class
        /// </summary>
        /// <param name="utcDateTime">The current time in UTC</param>
        /// <param name="timeZone">The time zone</param>
        internal LocalTimeKeeper(DateTime utcDateTime, DateTimeZone timeZone)
        {
            TimeZone = timeZone;
            LocalTime = utcDateTime.ConvertTo(DateTimeZone.Utc, TimeZone);
        }

        /// <summary>
        /// Updates the current time of this time keeper
        /// </summary>
        /// <param name="utcDateTime">The current time in UTC</param>
        internal void UpdateTime(DateTime utcDateTime)
        {
            LocalTime = utcDateTime.ConvertTo(DateTimeZone.Utc, TimeZone);
            TimeUpdated?.Invoke(this, new TimeUpdatedEventArgs(LocalTime, TimeZone));
        }
    }
}