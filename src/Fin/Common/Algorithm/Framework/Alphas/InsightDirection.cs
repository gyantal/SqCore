using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace QuantConnect.Algorithm.Framework.Alphas
{
    /// <summary>
    /// Specifies the predicted direction for a insight (price/volatility)
    /// </summary>
    [JsonConverter(typeof(StringEnumConverter), true)]
    public enum InsightDirection
    {
        /// <summary>
        /// The value will go down (-1)
        /// </summary>
        Down = -1,

        /// <summary>
        /// The value will stay flat (0)
        /// </summary>
        Flat = 0,

        /// <summary>
        /// The value will go up (1)
        /// </summary>
        Up = 1
    }
}