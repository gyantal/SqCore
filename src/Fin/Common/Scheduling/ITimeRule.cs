using System;
using System.Collections.Generic;

namespace QuantConnect.Scheduling
{
    /// <summary>
    /// Specifies times times on dates for events, used in conjunction with <see cref="IDateRule"/>
    /// </summary>
    public interface ITimeRule
    {
        /// <summary>
        /// Gets a name for this rule
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Creates the event times for the specified dates in UTC
        /// </summary>
        /// <param name="dates">The dates to apply times to</param>
        /// <returns>An enumerable of date times that is the result
        /// of applying this rule to the specified dates</returns>
        IEnumerable<DateTime> CreateUtcEventTimes(IEnumerable<DateTime> dates);
    }
}