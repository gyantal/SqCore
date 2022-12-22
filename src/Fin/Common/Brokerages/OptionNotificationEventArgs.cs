using System;
using QuantConnect.Interfaces;

namespace QuantConnect.Brokerages
{
    /// <summary>
    /// Event arguments class for the <see cref="IBrokerage.OptionNotification"/> event
    /// </summary>
    public sealed class OptionNotificationEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the option symbol which has received a notification
        /// </summary>
        public Symbol Symbol { get; }

        /// <summary>
        /// Gets the new option position (positive for long, zero for flat, negative for short)
        /// </summary>
        public decimal Position { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="OptionNotificationEventArgs"/> class
        /// </summary>
        /// <param name="symbol">The symbol</param>
        /// <param name="position">The new option position</param>
        public OptionNotificationEventArgs(Symbol symbol, decimal position)
        {
            Symbol = symbol;
            Position = position;
        }
    }
}
