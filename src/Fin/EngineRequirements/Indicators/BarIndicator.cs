using QuantConnect.Data.Market;

namespace QuantConnect.Indicators
{
    /// <summary>
    /// The BarIndicator is an indicator that accepts IBaseDataBar data as its input.
    /// 
    /// This type is more of a shim/typedef to reduce the need to refer to things as IndicatorBase&lt;IBaseDataBar&gt;
    /// </summary>
    public abstract class BarIndicator : IndicatorBase<IBaseDataBar>
    {
        /// <summary>
        /// Creates a new TradeBarIndicator with the specified name
        /// </summary>
        /// <param name="name">The name of this indicator</param>
        protected BarIndicator(string name)
            : base(name)
        {
        }
    }
}