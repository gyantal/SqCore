using System.Collections.Generic;
using System.Threading;

namespace QuantConnect.Lean.Engine.DataFeeds
{
    /// <summary>
    /// Interface which provides the data to stream to the algorithm
    /// </summary>
    public interface ISynchronizer
    {
        /// <summary>
        /// Returns an enumerable which provides the data to stream to the algorithm
        /// </summary>
        IEnumerable<TimeSlice> StreamData(CancellationToken cancellationToken);
    }
}
