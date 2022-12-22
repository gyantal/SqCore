using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace QuantConnect.Packets
{
    /// <summary>
    /// Algorithm status update information packet
    /// </summary>
    public class AlgorithmStatusPacket : Packet
    {
        /// <summary>
        /// Current algorithm status
        /// </summary>
        [JsonProperty(PropertyName = "eStatus")]
        [JsonConverter(typeof(StringEnumConverter))]
        public AlgorithmStatus Status;

        /// <summary>
        /// Chart we're subscribed to for live trading.
        /// </summary>
        [JsonProperty(PropertyName = "sChartSubscription")]
        public string ChartSubscription;

        /// <summary>
        /// Optional message or reason for state change.
        /// </summary>
        [JsonProperty(PropertyName = "sMessage")]
        public string Message;

        /// <summary>
        /// Algorithm Id associated with this status packet
        /// </summary>
        [JsonProperty(PropertyName = "sAlgorithmID")]
        public string AlgorithmId;

        /// <summary>
        /// OptimizationId for this result packet if any
        /// </summary>
        [JsonProperty(PropertyName = "sOptimizationID")]
        public string OptimizationId;

        /// <summary>
        /// Project Id associated with this status packet
        /// </summary>
        [JsonProperty(PropertyName = "iProjectID")]
        public int ProjectId;

        /// <summary>
        /// The current state of the channel
        /// </summary>
        [JsonProperty(PropertyName = "sChannelStatus")]
        public string ChannelStatus;

        /// <summary>
        /// Default constructor for JSON
        /// </summary>
        public AlgorithmStatusPacket()
            : base(PacketType.AlgorithmStatus)
        {
        }

        /// <summary>
        /// Initialize algorithm state packet:
        /// </summary>
        public AlgorithmStatusPacket(string algorithmId, int projectId, AlgorithmStatus status, string message = "")
            : base (PacketType.AlgorithmStatus)
        {
            Status = status;
            ProjectId = projectId;
            AlgorithmId = algorithmId;
            Message = message;
        }   
    }
}
