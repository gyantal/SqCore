using Newtonsoft.Json;

namespace QuantConnect.Packets
{
    /// <summary>
    /// Algorithm runtime error packet from the lean engine. 
    /// This is a managed error which stops the algorithm execution.
    /// </summary>
    public class RuntimeErrorPacket : Packet
    {
        /// <summary>
        /// Runtime error message from the exception
        /// </summary>
        [JsonProperty(PropertyName = "sMessage")]
        public string Message;

        /// <summary>
        /// Algorithm id which generated this runtime error
        /// </summary>
        [JsonProperty(PropertyName = "sAlgorithmID")]
        public string AlgorithmId;

        /// <summary>
        /// Error stack trace information string passed through from the Lean exception
        /// </summary>
        [JsonProperty(PropertyName = "sStackTrace")]
        public string StackTrace;

        /// <summary>
        /// User Id associated with the backtest that threw the error
        /// </summary>
        [JsonProperty(PropertyName = "iUserID")]
        public int UserId = 0;

        /// <summary>
        /// Default constructor for JSON
        /// </summary>
        public RuntimeErrorPacket()
            : base (PacketType.RuntimeError)
        { }

        /// <summary>
        /// Create a new runtime error packet
        /// </summary>
        public RuntimeErrorPacket(int userId, string algorithmId, string message, string stacktrace = "")
            : base(PacketType.RuntimeError)
        {
            UserId = userId;
            Message = message;
            AlgorithmId = algorithmId;
            StackTrace = stacktrace;
        }
    
    } // End Work Packet:

} // End of Namespace:
