using System;
using System.Collections.Generic;
using QuantConnect.Securities;

namespace QuantConnect.Data.UniverseSelection
{
    /// <summary>
    /// A universe implementing this interface will NOT use it's SubscriptionDataConfig to generate data
    /// that is used to 'pulse' the universe selection function -- instead, the times output by
    /// GetTriggerTimes are used to 'pulse' the universe selection function WITHOUT data.
    /// </summary>
    public interface ITimeTriggeredUniverse
    {
        /// <summary>
        /// Returns an enumerator that defines when this user defined universe will be invoked
        /// </summary>
        /// <returns>An enumerator of DateTime that defines when this universe will be invoked</returns>
        IEnumerable<DateTime> GetTriggerTimes(DateTime startTimeUtc, DateTime endTimeUtc, MarketHoursDatabase marketHoursDatabase);
    }
}