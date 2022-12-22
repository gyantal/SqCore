using QuantConnect.Data;
using System;
using System.Collections;
using System.Collections.Generic;

namespace QuantConnect.Lean.Engine.DataFeeds.Enumerators
{
    /// <summary>
    /// Represents an enumerator capable of synchronizing other base data enumerators in time.
    /// This assumes that all enumerators have data time stamped in the same time zone
    /// </summary>
    public class SynchronizingBaseDataEnumerator : SynchronizingEnumerator<BaseData>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SynchronizingBaseDataEnumerator"/> class
        /// </summary>
        /// <param name="enumerators">The enumerators to be synchronized. NOTE: Assumes the same time zone for all data</param>
        public SynchronizingBaseDataEnumerator(params IEnumerator<BaseData>[] enumerators)
            : this((IEnumerable<IEnumerator>)enumerators)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SynchronizingBaseDataEnumerator"/> class
        /// </summary>
        /// <param name="enumerators">The enumerators to be synchronized. NOTE: Assumes the same time zone for all data</param>
        public SynchronizingBaseDataEnumerator(IEnumerable<IEnumerator> enumerators) : base((IEnumerable<IEnumerator<BaseData>>)enumerators)
        {
        }

        /// <summary>
        /// Gets the Timestamp for the data
        /// </summary>
        protected override DateTime GetInstanceTime(BaseData instance)
        {
            return instance.EndTime;
        }
    }
}
