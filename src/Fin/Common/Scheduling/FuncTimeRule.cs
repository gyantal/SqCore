using System;
using System.Collections.Generic;

namespace QuantConnect.Scheduling
{
    /// <summary>
    /// Uses a function to define a time rule as a projection of date times to date times
    /// </summary>
    public class FuncTimeRule : ITimeRule
    {
        private readonly Func<IEnumerable<DateTime>, IEnumerable<DateTime>> _createUtcEventTimesFunction;

        /// <summary>
        /// Initializes a new instance of the <see cref="FuncTimeRule"/> class
        /// </summary>
        /// <param name="name">The name of the time rule</param>
        /// <param name="createUtcEventTimesFunction">Function used to transform dates into event date times</param>
        public FuncTimeRule(string name, Func<IEnumerable<DateTime>, IEnumerable<DateTime>> createUtcEventTimesFunction)
        {
            Name = name;
            _createUtcEventTimesFunction = createUtcEventTimesFunction;
        }

        /// <summary>
        /// Gets a name for this rule
        /// </summary>
        public string Name
        {
            get; private set;
        }

        /// <summary>
        /// Creates the event times for the specified dates in UTC
        /// </summary>
        /// <param name="dates">The dates to apply times to</param>
        /// <returns>An enumerable of date times that is the result
        /// of applying this rule to the specified dates</returns>
        public IEnumerable<DateTime> CreateUtcEventTimes(IEnumerable<DateTime> dates)
        {
            return _createUtcEventTimesFunction(dates);
        }
    }
}