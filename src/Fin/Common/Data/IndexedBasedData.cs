using System;

namespace QuantConnect.Data
{
    /// <summary>
    /// Abstract indexed base data class of QuantConnect.
    /// It is intended to be extended to define customizable data types which are stored
    /// using an intermediate index source
    /// </summary>
    public abstract class IndexedBaseData : BaseData
    {
        /// <summary>
        /// Returns the source for a given index value
        /// </summary>
        /// <param name="config">Configuration object</param>
        /// <param name="date">Date of this source file</param>
        /// <param name="index">The index value for which we want to fetch the source</param>
        /// <param name="isLiveMode">true if we're in live mode, false for backtesting mode</param>
        /// <returns>The <see cref="SubscriptionDataSource"/> instance to use</returns>
        public virtual SubscriptionDataSource GetSourceForAnIndex(SubscriptionDataConfig config, DateTime date, string index, bool isLiveMode)
        {
            throw new NotImplementedException($"{nameof(IndexedBaseData)} types should implement 'GetSourceForAnIndex'. " +
                                              "The implementation should determine the source to use for a given index value.");
        }

        /// <summary>
        /// Returns the index source for a date
        /// </summary>
        /// <param name="config">Configuration object</param>
        /// <param name="date">Date of this source file</param>
        /// <param name="isLiveMode">true if we're in live mode, false for backtesting mode</param>
        /// <returns>The <see cref="SubscriptionDataSource"/> instance to use</returns>
        public override SubscriptionDataSource GetSource(SubscriptionDataConfig config, DateTime date, bool isLiveMode)
        {
            throw new NotImplementedException($"{nameof(IndexedBaseData)} types should implement 'GetSource'. " +
                                              "The implementation should determine the index source to use for a given date.");
        }
    }
}
