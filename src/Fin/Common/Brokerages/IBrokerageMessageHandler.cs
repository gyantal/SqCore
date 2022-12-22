namespace QuantConnect.Brokerages
{
    /// <summary>
    /// Provides an plugin point to allow algorithms to directly handle the messages
    /// that come from their brokerage
    /// </summary>
    public interface IBrokerageMessageHandler
    {
        /// <summary>
        /// Handles the message
        /// </summary>
        /// <param name="message">The message to be handled</param>
        void Handle(BrokerageMessageEvent message);
    }
}
