using System;
using System.Collections.Generic;
using QuantConnect.Data;
using QuantConnect.Data.UniverseSelection;
using QuantConnect.Interfaces;

namespace QuantConnect.Lean.Engine.DataFeeds
{
    /// <summary>
    /// Represents a grouping of data emitted at a certain time.
    /// </summary>
    public class TimeSlice
    {
        /// <summary>
        /// Gets the count of data points in this <see cref="TimeSlice"/>
        /// </summary>
        public int DataPointCount { get; }

        /// <summary>
        /// Gets the UTC time this data was emitted
        /// </summary>
        public DateTime Time { get; }

        /// <summary>
        /// Gets the data in the time slice
        /// </summary>
        public List<DataFeedPacket> Data { get; }

        /// <summary>
        /// Gets the <see cref="Slice"/> that will be used as input for the algorithm
        /// </summary>
        public Slice Slice { get; }

        /// <summary>
        /// Gets the data used to update securities
        /// </summary>
        public List<UpdateData<ISecurityPrice>> SecuritiesUpdateData { get; }

        /// <summary>
        /// Gets the data used to update the consolidators
        /// </summary>
        public List<UpdateData<SubscriptionDataConfig>> ConsolidatorUpdateData { get; }

        /// <summary>
        /// Gets all the custom data in this <see cref="TimeSlice"/>
        /// </summary>
        public List<UpdateData<ISecurityPrice>> CustomData { get; }

        /// <summary>
        /// Gets the changes to the data subscriptions as a result of universe selection
        /// </summary>
        public SecurityChanges SecurityChanges { get; }

        /// <summary>
        /// Gets the universe data generated this time step.
        /// </summary>
        public Dictionary<Universe, BaseDataCollection> UniverseData { get; }

        /// <summary>
        /// True indicates this time slice is a time pulse for the algorithm containing no data
        /// </summary>
        public bool IsTimePulse { get; }

        /// <summary>
        /// Initializes a new <see cref="TimeSlice"/> containing the specified data
        /// </summary>
        public TimeSlice(DateTime time,
            int dataPointCount,
            Slice slice,
            List<DataFeedPacket> data,
            List<UpdateData<ISecurityPrice>> securitiesUpdateData,
            List<UpdateData<SubscriptionDataConfig>> consolidatorUpdateData,
            List<UpdateData<ISecurityPrice>> customData,
            SecurityChanges securityChanges,
            Dictionary<Universe, BaseDataCollection> universeData,
            bool isTimePulse = false)
        {
            Time = time;
            Data = data;
            Slice = slice;
            CustomData = customData;
            DataPointCount = dataPointCount;
            SecuritiesUpdateData = securitiesUpdateData;
            ConsolidatorUpdateData = consolidatorUpdateData;
            SecurityChanges = securityChanges;
            UniverseData = universeData;
            IsTimePulse = isTimePulse;
        }
    }
}