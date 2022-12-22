using System;
using QuantConnect.Data.Market;

namespace QuantConnect.Data.Consolidators
{
    /// <summary>
    /// Represents the simplest DataConsolidator implementation, one that is defined
    /// by a straight pass through of the data. No projection or aggregation is performed.
    /// </summary>
    /// <typeparam name="T">The type of data</typeparam>
    public class IdentityDataConsolidator<T> : DataConsolidator<T>
        where T : IBaseData
    {
        private static readonly bool IsTick = typeof (T) == typeof (Tick);

        private T _last;

        /// <summary>
        /// Gets a clone of the data being currently consolidated
        /// </summary>
        public override IBaseData WorkingData
        {
            get { return _last == null ? null : _last.Clone(); }
        }

        /// <summary>
        /// Gets the type produced by this consolidator
        /// </summary>
        public override Type OutputType
        {
            get { return typeof (T); }
        }

        /// <summary>
        /// Updates this consolidator with the specified data
        /// </summary>
        /// <param name="data">The new data for the consolidator</param>
        public override void Update(T data)
        {
            if (IsTick || _last == null || _last.EndTime != data.EndTime)
            {
                OnDataConsolidated(data);
                _last = data;
            }
        }

        /// <summary>
        /// Scans this consolidator to see if it should emit a bar due to time passing
        /// </summary>
        /// <param name="currentLocalTime">The current time in the local time zone (same as <see cref="BaseData.Time"/>)</param>
        public override void Scan(DateTime currentLocalTime)
        {
        }
    }
}