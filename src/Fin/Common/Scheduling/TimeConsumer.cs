using System;

namespace QuantConnect.Scheduling
{
    /// <summary>
    /// Represents a timer consumer instance
    /// </summary>
    public class TimeConsumer
    {
        /// <summary>
        /// True if the consumer already finished it's work and no longer consumes time
        /// </summary>
        public bool Finished { get; set; }

        /// <summary>
        /// The time provider associated with this consumer
        /// </summary>
        public ITimeProvider TimeProvider { get; set; }

        /// <summary>
        /// The isolator limit provider to be used with this consumer
        /// </summary>
        public IIsolatorLimitResultProvider IsolatorLimitProvider { get; set; }

        /// <summary>
        /// The next time, base on the <see cref="TimeProvider"/>, that time should be requested
        /// to be <see cref="IsolatorLimitProvider"/>
        /// </summary>
        public DateTime? NextTimeRequest { get; set; }
    }
}
