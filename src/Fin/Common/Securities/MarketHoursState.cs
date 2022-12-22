using System.Runtime.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace QuantConnect.Securities
{
    /// <summary>
    /// Specifies the open/close state for a <see cref="MarketHoursSegment"/>
    /// </summary>
    [JsonConverter(typeof(StringEnumConverter))]
    public enum MarketHoursState
    {
        /// <summary>
        /// The market is not open (0)
        /// </summary>
        [EnumMember(Value = "closed")]
        Closed,

        /// <summary>
        /// The market is open, but before normal trading hours (1)
        /// </summary>
        [EnumMember(Value = "premarket")]
        PreMarket,

        /// <summary>
        /// The market is open and within normal trading hours (2)
        /// </summary>
        [EnumMember(Value = "market")]
        Market,

        /// <summary>
        /// The market is open, but after normal trading hours (3)
        /// </summary>
        [EnumMember(Value = "postmarket")]
        PostMarket
    }
}