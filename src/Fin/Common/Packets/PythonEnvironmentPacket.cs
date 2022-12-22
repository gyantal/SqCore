using Newtonsoft.Json;

namespace QuantConnect.Packets
{
    /// <summary>
    /// Python Environment Packet is an abstract packet that contains a PythonVirtualEnvironment
    /// definition. Intended to be used by inheriting classes that may use a PythonVirtualEnvironment
    /// </summary>
    public abstract class PythonEnvironmentPacket : Packet
    {
        /// <summary>
        /// Default constructor for a PythonEnvironmentPacket
        /// </summary>
        /// <param name="type"></param>
        protected PythonEnvironmentPacket(PacketType type) : base(type)
        {
        }

        /// <summary>
        /// Virtual environment ID used to find PythonEvironments
        /// Ideally MD5, but environment names work as well.
        /// </summary>
        [JsonProperty(PropertyName = "sPythonVirtualEnvironment")]
        public string PythonVirtualEnvironment { get; set; }
    }
}
