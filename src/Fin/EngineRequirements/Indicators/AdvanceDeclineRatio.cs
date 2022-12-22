using System.Linq;

namespace QuantConnect.Indicators
{
    /// <summary>
    /// The advance-decline ratio (ADR) compares the number of stocks 
    /// that closed higher against the number of stocks 
    /// that closed lower than their previous day's closing prices.
    /// </summary>
    public class AdvanceDeclineRatio : AdvanceDeclineIndicator
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AdvanceDeclineRatio"/> class
        /// </summary>
        public AdvanceDeclineRatio(string name) : base(name, (entries) => entries.Count()) { }
    }
}
