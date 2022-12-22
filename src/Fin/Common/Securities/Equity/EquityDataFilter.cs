using QuantConnect.Data;

namespace QuantConnect.Securities.Equity 
{
    /// <summary>
    /// Equity security type data filter 
    /// </summary>
    /// <seealso cref="SecurityDataFilter"/>
    public class EquityDataFilter : SecurityDataFilter
    {
        /// <summary>
        /// Initialize Data Filter Class:
        /// </summary>
        public EquityDataFilter() : base()
        {

        }

        /// <summary>
        /// Equity filter the data: true - accept, false - fail.
        /// </summary>
        /// <param name="data">Data class</param>
        /// <param name="vehicle">Security asset</param>
        public override bool Filter(Security vehicle, BaseData data)
        {
            // No data filter for bad ticks. All raw data will be piped into algorithm
            return true;
        }

    } //End Filter

} //End Namespace