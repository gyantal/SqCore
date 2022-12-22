using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace QuantConnect.Api
{
    /// <summary>
    /// Response from the compiler on a build event
    /// </summary>
    public class Compile : RestResponse
    {
        /// <summary>
        /// Compile Id for a sucessful build
        /// </summary>
        [JsonProperty(PropertyName = "compileId")]
        public string CompileId { get; set; }

        /// <summary>
        /// True on successful compile
        /// </summary>
        [JsonProperty(PropertyName = "state")]
        [JsonConverter(typeof(StringEnumConverter))]
        public CompileState State { get; set; }

        /// <summary>
        /// Logs of the compilation request
        /// </summary>
        [JsonProperty(PropertyName = "logs")]
        public List<string> Logs { get; set; }
    }
}
