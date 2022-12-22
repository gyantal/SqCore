using System;
using System.Globalization;
using Newtonsoft.Json;

namespace QuantConnect.Util
{
    /// <summary>
    /// Helper <see cref="JsonConverter"/> that will round decimal and double types,
    /// to <see cref="FractionalDigits"/> fractional digits
    /// </summary>
    public class JsonRoundingConverter : JsonConverter
    {
        /// <summary>
        /// The number of fractional digits to round to
        /// </summary>
        public const int FractionalDigits = 4;

        /// <summary>
        /// Will always return false.
        /// Gets a value indicating whether this <see cref="T:Newtonsoft.Json.JsonConverter" /> can read JSON.
        /// </summary>
        public override bool CanRead => false;

        /// <summary>
        /// Determines whether this instance can convert the specified object type.
        /// </summary>
        /// <param name="objectType">Type of the object.</param>
        /// <returns>True if this instance can convert the specified object type</returns>
        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(decimal)
                || objectType == typeof(double);
        }

        /// <summary>
        /// Not implemented, will throw <see cref="NotImplementedException"/>
        /// </summary>
        /// <param name="reader">The <see cref="T:Newtonsoft.Json.JsonReader"/> to read from.</param>
        /// <param name="objectType">Type of the object.</param>
        /// <param name="existingValue">The existing value of object being read.</param>
        /// <param name="serializer">The calling serializer.</param>
        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Writes the JSON representation of the object.
        /// </summary>
        /// <param name="writer">The <see cref="T:Newtonsoft.Json.JsonWriter"/> to write to.</param>
        /// <param name="value">The value.</param>
        /// <param name="serializer">The calling serializer.</param>
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            if (value is double)
            {
                var rounded = Math.Round((double)value, FractionalDigits);
                writer.WriteValue(rounded.ToString(CultureInfo.InvariantCulture));
            }
            else
            {
                // we serialize decimal as string so that json doesn't use exponential notation which actually will lose precision
                var rounded = Math.Round((decimal)value, FractionalDigits);
                writer.WriteValue(rounded.ToString(CultureInfo.InvariantCulture));
            }
        }
    }
}