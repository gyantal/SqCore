using QuantConnect.Util;

namespace QuantConnect.Algorithm.Framework.Alphas.Serialization
{
    /// <summary>
    /// Defines how insights should be serialized to json
    /// </summary>
    public class InsightJsonConverter : TypeChangeJsonConverter<Insight, SerializedInsight>
    {
        /// <summary>
        /// Convert the input value to a value to be serialized
        /// </summary>
        /// <param name="value">The input value to be converted before serialization</param>
        /// <returns>A new instance of TResult that is to be serialized</returns>
        protected override SerializedInsight Convert(Insight value)
        {
            return new SerializedInsight(value);
        }

        /// <summary>
        /// Converts the input value to be deserialized
        /// </summary>
        /// <param name="value">The deserialized value that needs to be converted to T</param>
        /// <returns>The converted value</returns>
        protected override Insight Convert(SerializedInsight value)
        {
            return Insight.FromSerializedInsight(value);
        }
    }
}
