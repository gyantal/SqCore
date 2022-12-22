using QuantConnect.Data.UniverseSelection;

namespace QuantConnect.Algorithm.Framework
{
    /// <summary>
    /// Types implementing this interface will be called when the algorithm's set of securities changes
    /// </summary>
    public interface INotifiedSecurityChanges
    {
        /// <summary>
        /// Event fired each time the we add/remove securities from the data feed
        /// </summary>
        /// <param name="algorithm">The algorithm instance that experienced the change in securities</param>
        /// <param name="changes">The security additions and removals from the algorithm</param>
        void OnSecuritiesChanged(QCAlgorithm algorithm, SecurityChanges changes);
    }
}
