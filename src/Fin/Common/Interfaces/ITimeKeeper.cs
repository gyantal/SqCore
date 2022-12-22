using System;
using NodaTime;

namespace QuantConnect.Interfaces
{
    /// <summary>
    /// Interface implemented by <see cref="TimeKeeper"/>
    /// </summary>
    public interface ITimeKeeper
    {
        /// <summary>
        /// Gets the current time in UTC
        /// </summary>
        DateTime UtcTime { get; }

        /// <summary>
        /// Adds the specified time zone to this time keeper
        /// </summary>
        /// <param name="timeZone"></param>
        void AddTimeZone(DateTimeZone timeZone);

        /// <summary>
        /// Gets the <see cref="LocalTimeKeeper"/> instance for the specified time zone
        /// </summary>
        /// <param name="timeZone">The time zone whose <see cref="LocalTimeKeeper"/> we seek</param>
        /// <returns>The <see cref="LocalTimeKeeper"/> instance for the specified time zone</returns>
        LocalTimeKeeper GetLocalTimeKeeper(DateTimeZone timeZone);
    }
}
