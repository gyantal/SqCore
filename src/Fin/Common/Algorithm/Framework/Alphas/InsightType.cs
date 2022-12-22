using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace QuantConnect.Algorithm.Framework.Alphas
{
    /// <summary>
    /// Specifies the type of insight
    /// </summary>
    [JsonConverter(typeof(StringEnumConverter), true)]
    public enum InsightType
    {
        /// <summary>
        /// The insight is for a security's price (0)
        /// </summary>
        Price,

        /// <summary>
        /// The insight is for a security's price volatility (1)
        /// </summary>
        Volatility
    }
}