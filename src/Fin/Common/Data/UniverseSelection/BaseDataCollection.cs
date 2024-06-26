using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;

namespace QuantConnect.Data.UniverseSelection
{
    /// <summary>
    /// This type exists for transport of data as a single packet
    /// </summary>
    public class BaseDataCollection : BaseData, IEnumerable<BaseData>
    {
        private DateTime _endTime;

        /// <summary>
        /// The associated underlying price data if any
        /// </summary>
        public BaseData Underlying { get; set; }

        /// <summary>
        /// Gets or sets the contracts selected by the universe
        /// </summary>
        public IReadOnlyCollection<Symbol> FilteredContracts { get; set; }

        /// <summary>
        /// Gets the data list
        /// </summary>
        public List<BaseData> Data { get; set; }

        /// <summary>
        /// Gets or sets the end time of this data
        /// </summary>
        public override DateTime EndTime
        {
            get { return _endTime; }
            set { _endTime = value; }
        }

        /// <summary>
        /// Initializes a new default instance of the <see cref="BaseDataCollection"/> c;ass
        /// </summary>
        public BaseDataCollection()
            : this(DateTime.MinValue, Symbol.Empty)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="BaseDataCollection"/> class
        /// </summary>
        /// <param name="time">The time of this data</param>
        /// <param name="symbol">A common identifier for all data in this packet</param>
        /// <param name="data">The data to add to this collection</param>
        public BaseDataCollection(DateTime time, Symbol symbol, IEnumerable<BaseData> data = null)
            : this(time, time, symbol, data)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="BaseDataCollection"/> class
        /// </summary>
        /// <param name="time">The start time of this data</param>
        /// <param name="endTime">The end time of this data</param>
        /// <param name="symbol">A common identifier for all data in this packet</param>
        /// <param name="data">The data to add to this collection</param>
        /// <param name="underlying">The associated underlying price data if any</param>
        /// <param name="filteredContracts">The contracts selected by the universe</param>
        public BaseDataCollection(DateTime time, DateTime endTime, Symbol symbol, IEnumerable<BaseData> data = null, BaseData underlying = null, IReadOnlyCollection<Symbol> filteredContracts = null)
            : this(time, endTime, symbol, data != null ? data.ToList() : new List<BaseData>(), underlying, filteredContracts)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="BaseDataCollection"/> class
        /// </summary>
        /// <param name="time">The start time of this data</param>
        /// <param name="endTime">The end time of this data</param>
        /// <param name="symbol">A common identifier for all data in this packet</param>
        /// <param name="data">The data to add to this collection</param>
        /// <param name="underlying">The associated underlying price data if any</param>
        /// <param name="filteredContracts">The contracts selected by the universe</param>
        public BaseDataCollection(DateTime time, DateTime endTime, Symbol symbol, List<BaseData> data, BaseData underlying, IReadOnlyCollection<Symbol> filteredContracts)
        {
            Symbol = symbol;
            Time = time;
            _endTime = endTime;
            Underlying = underlying;
            FilteredContracts = filteredContracts;
            Data = data ?? new List<BaseData>();
        }

        /// <summary>
        /// Adds a new data point to this collection
        /// </summary>
        /// <param name="newDataPoint">The new data point to add</param>
        public virtual void Add(BaseData newDataPoint)
        {
            Data.Add(newDataPoint);
        }

        /// <summary>
        /// Adds a new data points to this collection
        /// </summary>
        /// <param name="newDataPoints">The new data points to add</param>
        public virtual void AddRange(IEnumerable<BaseData> newDataPoints)
        {
            Data.AddRange(newDataPoints);
        }

        /// <summary>
        /// Return a new instance clone of this object, used in fill forward
        /// </summary>
        /// <remarks>
        /// This base implementation uses reflection to copy all public fields and properties
        /// </remarks>
        /// <returns>A clone of the current object</returns>
        public override BaseData Clone()
        {
            return new BaseDataCollection(Time, EndTime, Symbol, Data, Underlying, FilteredContracts);
        }

        /// <summary>
        /// Returns an IEnumerator for this enumerable Object.  The enumerator provides
        /// a simple way to access all the contents of a collection.
        /// </summary>
        public IEnumerator<BaseData> GetEnumerator()
        {
            return (Data ?? Enumerable.Empty<BaseData>()).GetEnumerator();
        }

        /// <summary>
        /// Returns an IEnumerator for this enumerable Object.  The enumerator provides
        /// a simple way to access all the contents of a collection.
        /// </summary>
        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
