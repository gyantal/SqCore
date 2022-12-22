using System;
using QuantConnect.Data;
using QuantConnect.Interfaces;
using System.Collections.Generic;
using QuantConnect.Data.Auxiliary;

namespace QuantConnect.Lean.Engine.DataFeeds.Enumerators
{
    /// <summary>
    /// Interface for event providers for new tradable dates
    /// </summary>
    public interface ITradableDateEventProvider
    {
        /// <summary>
        /// Called each time there is a new tradable day
        /// </summary>
        /// <param name="eventArgs">The new tradable day event arguments</param>
        /// <returns>New corporate event if any</returns>
        IEnumerable<BaseData> GetEvents(NewTradableDateEventArgs eventArgs);

        /// <summary>
        /// Initializes the event provider instance
        /// </summary>
        /// <param name="config">The <see cref="SubscriptionDataConfig"/></param>
        /// <param name="factorFileProvider">The factor file provider to use</param>
        /// <param name="mapFileProvider">The <see cref="MapFile"/> provider to use</param>
        /// <param name="startTime">Start date for the data request</param>
        void Initialize(SubscriptionDataConfig config,
            IFactorFileProvider factorFileProvider,
            IMapFileProvider mapFileProvider,
            DateTime startTime);
    }
}
