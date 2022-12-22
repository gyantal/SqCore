using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace QuantConnect.Algorithm.Framework.Alphas
{
    /// <summary>
    /// Defines a specific type of score for a insight
    /// </summary>
    [JsonConverter(typeof(StringEnumConverter), true)]
    public enum InsightScoreType
    {
        /// <summary>
        /// Directional accuracy (0)
        /// </summary>
        Direction,

        /// <summary>
        /// Magnitude accuracy (1)
        /// </summary>
        Magnitude
    }
}