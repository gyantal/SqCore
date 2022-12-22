using QuantConnect.Data;
using System;
using System.Collections;
using System.Collections.Generic;

namespace QuantConnect.Lean.Engine.DataFeeds.Enumerators
{
    /// <summary>
    /// Represents an enumerator capable of synchronizing other slice enumerators in time.
    /// This assumes that all enumerators have data time stamped in the same time zone
    /// </summary>
    public class SynchronizingSliceEnumerator : SynchronizingEnumerator<Slice>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SynchronizingSliceEnumerator"/> class
        /// </summary>
        /// <param name="enumerators">The enumerators to be synchronized. NOTE: Assumes the same time zone for all data</param>
        public SynchronizingSliceEnumerator(params IEnumerator<Slice>[] enumerators)
            : this((IEnumerable<IEnumerator>)enumerators)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SynchronizingSliceEnumerator"/> class
        /// </summary>
        /// <param name="enumerators">The enumerators to be synchronized. NOTE: Assumes the same time zone for all data</param>
        public SynchronizingSliceEnumerator(IEnumerable<IEnumerator> enumerators) : base((IEnumerable<IEnumerator<Slice>>)enumerators)
        {
        }

        /// <summary>
        /// Gets the Timestamp for the data
        /// </summary>
        protected override DateTime GetInstanceTime(Slice instance)
        {
            return instance.UtcTime;
        }
    }
}
