using Newtonsoft.Json;

namespace QuantConnect.Api
{
    /// <summary>
    /// Estimate response packet from the QuantConnect.com API.
    /// </summary>
    public class Estimate
    {
        /// <summary>
        /// Estimate id
        /// </summary>
        [JsonProperty(PropertyName = "estimateId")]
        public string EstimateId { get; set; }

        /// <summary>
        /// Estimate time in seconds
        /// </summary>
        [JsonProperty(PropertyName = "time")]
        public int Time { get; set; }

        /// <summary>
        /// Estimate balance in QCC
        /// </summary>
        [JsonProperty(PropertyName = "balance")]
        public int Balance { get; set; }
    }

    /// <summary>
    /// Wrapper class for Optimizations/* endpoints JSON response
    /// Currently used by Optimizations/Estimate
    /// </summary>
    public class EstimateResponseWrapper : RestResponse
    {
        /// <summary>
        /// Estimate object
        /// </summary>
        [JsonProperty(PropertyName = "estimate")]
        public Estimate Estimate { get; set; }
    }
}
