using System;
using System.Collections.Generic;
using System.Linq;
using QuantConnect.Util;

namespace QuantConnect.Scheduling
{
    /// <summary>
    /// Combines multiple time rules into a single rule that emits for each rule
    /// </summary>
    public class CompositeTimeRule : ITimeRule
    {
        /// <summary>
        /// Gets the individual rules for this composite rule
        /// </summary>
        public readonly IReadOnlyList<ITimeRule> Rules;

        /// <summary>
        /// Initializes a new instance of the <see cref="CompositeTimeRule"/> class
        /// </summary>
        /// <param name="timeRules">The time rules to compose</param>
        public CompositeTimeRule(params ITimeRule[] timeRules)
            : this((IEnumerable<ITimeRule>) timeRules)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CompositeTimeRule"/> class
        /// </summary>
        /// <param name="timeRules">The time rules to compose</param>
        public CompositeTimeRule(IEnumerable<ITimeRule> timeRules)
        {
            Rules = timeRules.ToList();
        }

        /// <summary>
        /// Gets a name for this rule
        /// </summary>
        public string Name
        {
            get { return string.Join(",", Rules.Select(x => x.Name)); }
        }

        /// <summary>
        /// Creates the event times for the specified dates in UTC
        /// </summary>
        /// <param name="dates">The dates to apply times to</param>
        /// <returns>An enumerable of date times that is the result
        /// of applying this rule to the specified dates</returns>
        public IEnumerable<DateTime> CreateUtcEventTimes(IEnumerable<DateTime> dates)
        {
            foreach (var date in dates)
            {
                // make unqiue times and order the events before yielding
                var enumerable = new[] {date};
                var times = Rules.SelectMany(time => time.CreateUtcEventTimes(enumerable)).ToHashSet().OrderBy(x => x);
                foreach (var time in times)
                {
                    yield return time;
                }
            }
        }
    }
}
