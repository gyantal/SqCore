using QuantConnect.Data;

namespace QuantConnect.Securities.Forex 
{
    /// <summary>
    /// Forex packet by packet data filtering mechanism for dynamically detecting bad ticks.
    /// </summary>
    /// <seealso cref="SecurityDataFilter"/>
    public class ForexDataFilter : SecurityDataFilter
    {
        /// <summary>
        /// Initialize forex data filter class:
        /// </summary>
        public ForexDataFilter()
            : base() 
        {
            
        }

        /// <summary>
        /// Forex data filter: a true value means accept the packet, a false means fail.
        /// </summary>
        /// <param name="data">Data object we're scanning to filter</param>
        /// <param name="vehicle">Security asset</param>
        public override bool Filter(Security vehicle, BaseData data)
        {
            //FX data is from FXCM and fairly clean already. Accept all packets.
            return true;
        }

    } //End Filter

} //End Namespace