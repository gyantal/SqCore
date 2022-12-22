using QuantConnect.Interfaces;

namespace QuantConnect.Brokerages
{
    /// <summary>
    /// Event arguments class for the <see cref="IBrokerage.DelistingNotification"/> event
    /// </summary>
    public class DelistingNotificationEventArgs
    {
        /// <summary>
        /// Gets the option symbol which has received a notification
        /// </summary>
        public Symbol Symbol { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="DelistingNotificationEventArgs"/> class
        /// </summary>
        /// <param name="symbol">The symbol</param>
        public DelistingNotificationEventArgs(Symbol symbol)
        {
            Symbol = symbol;
        }
    }
}
