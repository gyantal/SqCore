﻿using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace QuantConnect.Util
{
    /// <summary>
    /// Provides a base class for a <see cref="JsonConverter"/> that serializes a
    /// an input type as some other output type
    /// </summary>
    /// <typeparam name="T">The type to be serialized</typeparam>
    /// <typeparam name="TResult">The output serialized type</typeparam>
    public abstract class TypeChangeJsonConverter<T, TResult> : JsonConverter
    {
        // we use a json serializer which allows using non public default constructor
        private readonly JsonSerializer _jsonSerializer = new JsonSerializer {ConstructorHandling = ConstructorHandling.AllowNonPublicDefaultConstructor};

        /// <summary>
        /// True will populate TResult object returned by <see cref="Convert(TResult)"/> with json properties
        /// </summary>
        protected virtual bool PopulateProperties => true;

        /// <summary>
        /// Reads the JSON representation of the object.
        /// </summary>
        /// <param name="reader">The <see cref="T:Newtonsoft.Json.JsonReader"/> to read from.</param><param name="objectType">Type of the object.</param><param name="existingValue">The existing value of object being read.</param><param name="serializer">The calling serializer.</param>
        /// <returns>
        /// The object value.
        /// </returns>
        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            // Load token from stream
            var token = JToken.Load(reader);

            // Create target object based on token
            var target = Create(objectType, token);

            return target;
        }

        /// <summary>
        /// Writes the JSON representation of the object.
        /// </summary>
        /// <param name="writer">The <see cref="T:Newtonsoft.Json.JsonWriter"/> to write to.</param><param name="value">The value.</param><param name="serializer">The calling serializer.</param>
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            // Convert the value into TResult to be serialized
            var valueToSerialize = Convert((T)value);

            serializer.Serialize(writer, valueToSerialize);
        }

        /// <summary>
        /// Determines whether this instance can convert the specified object type.
        /// </summary>
        /// <param name="objectType">Type of the object.</param>
        /// <returns>
        /// <c>true</c> if this instance can convert the specified object type; otherwise, <c>false</c>.
        /// </returns>
        public override bool CanConvert(Type objectType)
        {
            return typeof(T) == objectType;
        }

        /// <summary>
        /// Creates an instance of the un-projected type to be deserialized
        /// </summary>
        /// <param name="type">The input object type, this is the data held in the token</param>
        /// <param name="token">The input data to be converted into a T</param>
        /// <returns>A new instance of T that is to be serialized using default rules</returns>
        protected virtual T Create(Type type, JToken token)
        {
            // reads the token as an object type
            if (typeof(TResult).IsClass && typeof(T) != typeof(string))
            {
                return Convert(token.ToObject<TResult>(_jsonSerializer));
            }

            // reads the token as a value type
            return Convert(token.Value<TResult>());
        }

        /// <summary>
        /// Convert the input value to a value to be serialized
        /// </summary>
        /// <param name="value">The input value to be converted before serialziation</param>
        /// <returns>A new instance of TResult that is to be serialzied</returns>
        protected abstract TResult Convert(T value);

        /// <summary>
        /// Converts the input value to be deserialized
        /// </summary>
        /// <param name="value">The deserialized value that needs to be converted to T</param>
        /// <returns>The converted value</returns>
        protected abstract T Convert(TResult value);
    }
}
