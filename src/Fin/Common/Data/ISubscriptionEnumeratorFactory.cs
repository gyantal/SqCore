using System.Collections.Generic;
using QuantConnect.Data.UniverseSelection;
using QuantConnect.Interfaces;

namespace QuantConnect.Data
{
    /// <summary>
    /// Create an <see cref="IEnumerator{BaseData}"/> 
    /// </summary>
    public interface ISubscriptionEnumeratorFactory
    {
        /// <summary>
        /// Creates an enumerator to read the specified request
        /// </summary>
        /// <param name="request">The subscription request to be read</param>
        /// <param name="dataProvider">Provider used to get data when it is not present on disk</param>
        /// <returns>An enumerator reading the subscription request</returns>
        IEnumerator<BaseData> CreateEnumerator(SubscriptionRequest request, IDataProvider dataProvider);
    }
}
