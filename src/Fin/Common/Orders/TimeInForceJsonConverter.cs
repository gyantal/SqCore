using System;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using QuantConnect.Orders.TimeInForces;

namespace QuantConnect.Orders
{
    /// <summary>
    /// Provides an implementation of <see cref="JsonConverter"/> that can deserialize TimeInForce objects
    /// </summary>
    public class TimeInForceJsonConverter : JsonConverter
    {
        /// <summary>
        /// Gets a value indicating whether this <see cref="T:Newtonsoft.Json.JsonConverter"/> can write JSON.
        /// </summary>
        /// <value>
        /// <c>true</c> if this <see cref="T:Newtonsoft.Json.JsonConverter"/> can write JSON; otherwise, <c>false</c>.
        /// </value>
        public override bool CanWrite => true;

        /// <summary>
        /// Determines whether this instance can convert the specified object type.
        /// </summary>
        /// <param name="objectType">Type of the object.</param>
        /// <returns>
        /// <c>true</c> if this instance can convert the specified object type; otherwise, <c>false</c>.
        /// </returns>
        public override bool CanConvert(Type objectType)
        {
            return typeof(TimeInForce).IsAssignableFrom(objectType);
        }

        /// <summary>
        /// Writes the JSON representation of the object.
        /// </summary>
        /// <param name="writer">The <see cref="T:Newtonsoft.Json.JsonWriter"/> to write to.</param><param name="value">The value.</param><param name="serializer">The calling serializer.</param>
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            var timeInForce = value as TimeInForce;
            if (ReferenceEquals(timeInForce, null)) return;

            var jo = new JObject();

            var type = value.GetType();
            // don't add if its the default value used by the reader
            if (type != typeof(GoodTilCanceledTimeInForce))
            {
                jo.Add("$type", type.FullName);
            }

            foreach (var property in type.GetProperties())
            {
                if (property.CanRead)
                {
                    var propertyValue = property.GetValue(value, null);
                    if (propertyValue != null)
                    {
                        jo.Add(property.Name, JToken.FromObject(propertyValue, serializer));
                    }
                }
            }

            jo.WriteTo(writer);
        }

        /// <summary>
        /// Reads the JSON representation of the object.
        /// </summary>
        /// <param name="reader">The <see cref="T:Newtonsoft.Json.JsonReader"/> to read from.</param><param name="objectType">Type of the object.</param><param name="existingValue">The existing value of object being read.</param><param name="serializer">The calling serializer.</param>
        /// <returns>
        /// The object value.
        /// </returns>
        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            var jObject = JToken.Load(reader);

            Type type;
            var array = jObject as JArray;
            if (array != null)
            {
                if (array.Count != 0)
                {
                    throw new InvalidOperationException($"Unexpected time in force value: {jObject}");
                }
                // default value if not present. for php [] & {} are the same representation of empty object
                type = typeof(GoodTilCanceledTimeInForce);
            }
            else if (jObject["$type"] != null)
            {
                var jToken = jObject["$type"];
                var typeName = jToken.ToString();
                type = Type.GetType(typeName, throwOnError: false, ignoreCase: true);
                if (type == null)
                {
                    throw new InvalidOperationException($"Unable to find the type: {typeName}");
                }
            }
            else
            {
                // default value if not present
                type = typeof(GoodTilCanceledTimeInForce);
            }

            var constructor = type.GetConstructor(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new Type[0], null);
            if (constructor == null)
            {
                throw new NotImplementedException($"Unable to find a constructor for type: {type.FullName}");
            }

            var timeInForce = constructor.Invoke(null);

            foreach (var property in timeInForce.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                var value = jObject[property.Name];
                if (value != null)
                {
                    property.SetValue(timeInForce, value.ToObject(property.PropertyType));
                }
            }

            return timeInForce;
        }
    }
}
