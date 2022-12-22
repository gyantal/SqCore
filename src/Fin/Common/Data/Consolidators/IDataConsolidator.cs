using System;

namespace QuantConnect.Data.Consolidators
{
    /// <summary>
    /// Event handler type for the IDataConsolidator.DataConsolidated event
    /// </summary>
    /// <param name="sender">The consolidator that fired the event</param>
    /// <param name="consolidated">The consolidated piece of data</param>
    public delegate void DataConsolidatedHandler(object sender, IBaseData consolidated);

    /// <summary>
    /// Represents a type capable of taking BaseData updates and firing events containing new
    /// 'consolidated' data. These types can be used to produce larger bars, or even be used to
    /// transform the data before being sent to another component. The most common usage of these
    /// types is with indicators.
    /// </summary>
    public interface IDataConsolidator : IDisposable
    {
        /// <summary>
        /// Gets the most recently consolidated piece of data. This will be null if this consolidator
        /// has not produced any data yet.
        /// </summary>
        IBaseData Consolidated { get; }

        /// <summary>
        /// Gets a clone of the data being currently consolidated
        /// </summary>
        IBaseData WorkingData { get; }

        /// <summary>
        /// Gets the type consumed by this consolidator
        /// </summary>
        Type InputType { get; }

        /// <summary>
        /// Gets the type produced by this consolidator
        /// </summary>
        Type OutputType { get; }

        /// <summary>
        /// Updates this consolidator with the specified data
        /// </summary>
        /// <param name="data">The new data for the consolidator</param>
        void Update(IBaseData data);

        /// <summary>
        /// Scans this consolidator to see if it should emit a bar due to time passing
        /// </summary>
        /// <param name="currentLocalTime">The current time in the local time zone (same as <see cref="BaseData.Time"/>)</param>
        void Scan(DateTime currentLocalTime);

        /// <summary>
        /// Event handler that fires when a new piece of data is produced
        /// </summary>
        event DataConsolidatedHandler DataConsolidated;
    }
}