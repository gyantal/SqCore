using System;

namespace QuantConnect.Lean.Engine.DataFeeds.Enumerators
{
    /// <summary>
    /// Interface which will provide an event handler
    /// who will be fired with each new tradable day
    /// </summary>
    public interface ITradableDatesNotifier
    {
        /// <summary>
        /// Event fired when there is a new tradable date
        /// </summary>
        event EventHandler<NewTradableDateEventArgs> NewTradableDate;
    }
}
