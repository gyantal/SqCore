using Newtonsoft.Json;

namespace QuantConnect.Packets
{
    /// <summary>
    /// Simple log message instruction from the lean engine.
    /// </summary>
    public class LogPacket : Packet
    {
        /// <summary>
        /// Log message to the users console:
        /// </summary>
        [JsonProperty(PropertyName = "sMessage")]
        public string Message;

        /// <summary>
        /// Algorithm Id requesting this logging
        /// </summary>
        [JsonProperty(PropertyName = "sAlgorithmID")]
        public string AlgorithmId;

        /// <summary>
        /// Default constructor for JSON
        /// </summary>
        public LogPacket()
            : base (PacketType.Log)
        { }

        /// <summary>
        /// Create a new instance of the notify Log packet:
        /// </summary>
        public LogPacket(string algorithmId, string message)
            : base(PacketType.Log)
        {
            Message = message;
            AlgorithmId = algorithmId;
        }
    
    } // End Work Packet:

} // End of Namespace:
