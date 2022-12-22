using System;
using System.Collections.Generic;

namespace QuantConnect.Scheduling
{
    /// <summary>
    /// Specifies dates that events should be fired, used in conjunction with the <see cref="ITimeRule"/>
    /// </summary>
    public interface IDateRule
    {
        /// <summary>
        /// Gets a name for this rule
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Gets the dates produced by this date rule between the specified times
        /// </summary>
        /// <param name="start">The start of the interval to produce dates for</param>
        /// <param name="end">The end of the interval to produce dates for</param>
        /// <returns>All dates in the interval matching this date rule</returns>
        IEnumerable<DateTime> GetDates(DateTime start, DateTime end);
    }
}