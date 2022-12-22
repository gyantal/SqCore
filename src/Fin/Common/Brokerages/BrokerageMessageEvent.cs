using static QuantConnect.StringExtensions;

namespace QuantConnect.Brokerages
{
    /// <summary>
    /// Represents a message received from a brokerage
    /// </summary>
    public class BrokerageMessageEvent
    {
        /// <summary>
        /// Gets the type of brokerage message
        /// </summary>
        public BrokerageMessageType Type { get; private set; }

        /// <summary>
        /// Gets the brokerage specific code for this message, zero if no code was specified
        /// </summary>
        public string Code { get; private set; }

        /// <summary>
        /// Gets the message text received from the brokerage
        /// </summary>
        public string Message { get; private set; }

        /// <summary>
        /// Initializes a new instance of the BrokerageMessageEvent class
        /// </summary>
        /// <param name="type">The type of brokerage message</param>
        /// <param name="code">The brokerage specific code</param>
        /// <param name="message">The message text received from the brokerage</param>
        public BrokerageMessageEvent(BrokerageMessageType type, int code, string message)
        {
            Type = type;
            Code = code.ToStringInvariant();
            Message = message;
        }

        /// <summary>
        /// Initializes a new instance of the BrokerageMessageEvent class
        /// </summary>
        /// <param name="type">The type of brokerage message</param>
        /// <param name="code">The brokerage specific code</param>
        /// <param name="message">The message text received from the brokerage</param>
        public BrokerageMessageEvent(BrokerageMessageType type, string code, string message)
        {
            Type = type;
            Code = code;
            Message = message;
        }

        /// <summary>
        /// Creates a new <see cref="BrokerageMessageEvent"/> to represent a disconnect message
        /// </summary>
        /// <param name="message">The message from the brokerage</param>
        /// <returns>A brokerage disconnect message</returns>
        public static BrokerageMessageEvent Disconnected(string message)
        {
            return new BrokerageMessageEvent(BrokerageMessageType.Disconnect, "Disconnect", message);
        }

        /// <summary>
        /// Creates a new <see cref="BrokerageMessageEvent"/> to represent a reconnect message
        /// </summary>
        /// <param name="message">The message from the brokerage</param>
        /// <returns>A brokerage reconnect message</returns>
        public static BrokerageMessageEvent Reconnected(string message)
        {
            return new BrokerageMessageEvent(BrokerageMessageType.Reconnect, "Reconnect", message);
        }

        /// <summary>
        /// Returns a string that represents the current object.
        /// </summary>
        /// <returns>
        /// A string that represents the current object.
        /// </returns>
        /// <filterpriority>2</filterpriority>
        public override string ToString()
        {
            return Invariant($"{Type} - Code: {Code} - {Message}");
        }
    }
}