using System.Collections.Generic;
using Newtonsoft.Json;

namespace QuantConnect.Packets
{
    /// <summary>
    /// Security types packet contains information on the markets the user data has requested.
    /// </summary>
    public class SecurityTypesPacket : Packet
    {
        /// <summary>
        /// List of Security Type the user has requested (Equity, Forex, Futures etc).
        /// </summary>
        [JsonProperty(PropertyName = "aMarkets")]
        public List<SecurityType> Types = new List<SecurityType>();

        /// <summary>
        /// CSV formatted, lower case list of SecurityTypes for the web API.
        /// </summary>
        public string TypesCSV
        {
            get
            {
                var result = "";
                foreach (var type in Types)
                {
                    result += type + ",";
                }
                result = result.TrimEnd(',');
                return result.ToLowerInvariant();
            }
        }

        /// <summary>
        /// Default constructor for JSON
        /// </summary>
        public SecurityTypesPacket()
            : base (PacketType.SecurityTypes)
        { }

    } // End Work Packet:

} // End of Namespace:
