using System.Linq;

namespace QuantConnect.Indicators
{
    /// <summary>
    /// The Advance Decline Volume Ratio is a Breadth indicator calculated as ratio of 
    /// summary volume of advancing stocks to summary volume of declining stocks. 
    /// AD Volume Ratio is used in technical analysis to see where the main trading activity is focused.
    /// </summary>
    public class AdvanceDeclineVolumeRatio : AdvanceDeclineIndicator
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AdvanceDeclineVolumeRatio"/> class
        /// </summary>
        public AdvanceDeclineVolumeRatio(string name) : base(name, (entries) => entries.Sum(s => s.Volume)) { }
    }
}
