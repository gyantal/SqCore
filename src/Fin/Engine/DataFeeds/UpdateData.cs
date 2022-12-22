using System;
using System.Collections.Generic;
using QuantConnect.Data;

namespace QuantConnect.Lean.Engine.DataFeeds
{
    /// <summary>
    /// Transport type for algorithm update data. This is intended to provide a
    /// list of base data used to perform updates against the specified target
    /// </summary>
    /// <typeparam name="T">The target type</typeparam>
    public class UpdateData<T>
    {
        /// <summary>
        /// Flag indicating whether <see cref="Data"/> contains any fill forward bar or not
        /// </summary>
        /// <remarks>This is useful for performance, it allows consumers to skip re enumerating the entire data
        /// list to filter any fill forward data</remarks>
        public readonly bool? ContainsFillForwardData;

        /// <summary>
        /// The target, such as a security or subscription data config
        /// </summary>
        public readonly T Target;

        /// <summary>
        /// The data used to update the target
        /// </summary>
        public readonly IReadOnlyList<BaseData> Data;

        /// <summary>
        /// The type of data in the data list
        /// </summary>
        public readonly Type DataType;

        /// <summary>
        /// True if this update data corresponds to an internal subscription
        /// such as currency or security benchmark
        /// </summary>
        public readonly bool IsInternalConfig;

        /// <summary>
        /// Initializes a new instance of the <see cref="UpdateData{T}"/> class
        /// </summary>
        /// <param name="target">The end consumer/user of the dat</param>
        /// <param name="dataType">The type of data in the list</param>
        /// <param name="data">The update data</param>
        /// <param name="isInternalConfig">True if this update data corresponds to an internal subscription
        /// such as currency or security benchmark</param>
        /// <param name="containsFillForwardData">True if this update data contains fill forward bars</param>
        public UpdateData(T target, Type dataType, IReadOnlyList<BaseData> data, bool isInternalConfig, bool? containsFillForwardData = null)
        {
            Target = target;
            Data = data;
            DataType = dataType;
            IsInternalConfig = isInternalConfig;
            ContainsFillForwardData = containsFillForwardData;
        }
    }
}