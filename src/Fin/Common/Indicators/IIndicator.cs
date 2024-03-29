using System;
using QuantConnect.Data;

namespace QuantConnect.Indicators
{
    /// <summary>
    /// KEEPING THIS INTERFACE FOR BACKWARDS COMPATIBILITY.
    /// Represents an indicator that can receive data updates and emit events when the value of
    /// the indicator has changed.
    /// </summary>
    public interface IIndicator<T> : IComparable<IIndicator<T>>, IIndicator
        where T : IBaseData
    {
    }

    /// <summary>
    /// Represents an indicator that can receive data updates and emit events when the value of
    /// the indicator has changed.
    /// </summary>
    public interface IIndicator : IComparable
    {
        /// <summary>
        /// Event handler that fires after this indicator is updated
        /// </summary>
        event IndicatorUpdatedHandler Updated;

        /// <summary>
        /// Gets a name for this indicator
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Gets a flag indicating when this indicator is ready and fully initialized
        /// </summary>
        bool IsReady { get; }

        /// <summary>
        /// Gets the current state of this indicator. If the state has not been updated
        /// then the time on the value will equal DateTime.MinValue.
        /// </summary>
        IndicatorDataPoint Current { get; }

        /// <summary>
        /// Gets the number of samples processed by this indicator
        /// </summary>
        long Samples { get; }

        /// <summary>
        /// Updates the state of this indicator with the given value and returns true
        /// if this indicator is ready, false otherwise
        /// </summary>
        /// <param name="input">The value to use to update this indicator</param>
        /// <returns>True if this indicator is ready, false otherwise</returns>
        bool Update(IBaseData input);

        /// <summary>
        /// Resets this indicator to its initial state
        /// </summary>
        void Reset();
    }
}
