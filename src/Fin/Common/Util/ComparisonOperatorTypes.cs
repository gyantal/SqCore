using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace QuantConnect.Util
{
    /// <summary>
    /// Comparison operators
    /// </summary>
    [JsonConverter(typeof(StringEnumConverter), true)]
    public enum ComparisonOperatorTypes
    {
        /// <summary>
        /// Check if their operands are equal
        /// </summary>
        Equals,

        /// <summary>
        /// Check if their operands are not equal
        /// </summary>
        NotEqual,

        /// <summary>
        /// Checks left-hand operand is greater than its right-hand operand
        /// </summary>
        Greater,

        /// <summary>
        /// Checks left-hand operand is greater or equal to its right-hand operand
        /// </summary>
        GreaterOrEqual,

        /// <summary>
        /// Checks left-hand operand is less than its right-hand operand
        /// </summary>
        Less,

        /// <summary>
        /// Checks left-hand operand is less or equal to its right-hand operand
        /// </summary>
        LessOrEqual
    }
}
