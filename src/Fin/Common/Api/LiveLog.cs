using System.Collections.Generic;
using Newtonsoft.Json;

namespace QuantConnect.Api
{
    /// <summary>
    /// Logs from a live algorithm
    /// </summary>
    public class LiveLog : RestResponse
    {
        /// <summary>
        /// List of logs from the live algorithm
        /// </summary>
        [JsonProperty(PropertyName = "LiveLogs")]
        public List<string> Logs { get; set; }
    }
}
