using Newtonsoft.Json;

namespace QuantConnect.Packets
{
    /// <summary>
    /// Send a simple debug message from the users algorithm to the console.
    /// </summary>
    public class DebugPacket : Packet
    {
        /// <summary>
        /// String debug message to send to the users console
        /// </summary>
        [JsonProperty(PropertyName = "sMessage")]
        public string Message;

        /// <summary>
        /// Associated algorithm Id.
        /// </summary>
        [JsonProperty(PropertyName = "sAlgorithmID")]
        public string AlgorithmId;

        /// <summary>
        /// Compile id of the algorithm sending this message
        /// </summary>
        [JsonProperty(PropertyName = "sCompileID")]
        public string CompileId;

        /// <summary>
        /// Project Id for this message
        /// </summary>
        [JsonProperty(PropertyName = "iProjectID")]
        public int ProjectId;

        /// <summary>
        /// True to emit message as a popup notification (toast),
        /// false to emit message in console as text
        /// </summary>
        [JsonProperty(PropertyName = "bToast")]
        public bool Toast;

        /// <summary>
        /// Default constructor for JSON
        /// </summary>
        public DebugPacket()
            : base (PacketType.Debug)
        { }

        /// <summary>
        /// Constructor for inherited types
        /// </summary>
        /// <param name="packetType">The type of packet to create</param>
        protected DebugPacket(PacketType packetType)
            : base(packetType)
        { }

        /// <summary>
        /// Create a new instance of the notify debug packet:
        /// </summary>
        public DebugPacket(int projectId, string algorithmId, string compileId, string message, bool toast = false)
            : base(PacketType.Debug)
        {
            ProjectId = projectId;
            Message = message;
            CompileId = compileId;
            AlgorithmId = algorithmId;
            Toast = toast;
        }
    
    } // End Work Packet:

} // End of Namespace:
