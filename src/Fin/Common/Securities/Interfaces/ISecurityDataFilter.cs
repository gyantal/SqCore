using QuantConnect.Data;

namespace QuantConnect.Securities.Interfaces
{
    /// <summary>
    /// Security data filter interface. Defines pattern for the user defined data filter techniques.
    /// </summary>
    /// <remarks>
    ///     Intended for use primarily with US equities tick data. The tick data is provided in raw 
    ///     and complete format which is more information that more retail feeds provide. In order to match
    ///     retail feeds the ticks much be filtered to show only public-on market trading.
    /// 
    ///     For tradebars this filter has already been done.
    /// </remarks>
    public interface ISecurityDataFilter 
    {
        /// <summary>
        /// Filter out a tick from this security, with this new data:
        /// </summary>
        /// <param name="data">New data packet we're checking</param>
        /// <param name="vehicle">Security of this filter.</param>
        bool Filter(Security vehicle, BaseData data);

    } // End Data Filter Interface

} // End QC Namespace
