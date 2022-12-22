using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Reflection;

namespace QuantConnect.Optimizer.Parameters
{
    /// <summary>
    /// Override <see cref="OptimizationParameter"/> deserialization method.
    /// Can handle <see cref="OptimizationArrayParameter"/> and <see cref="OptimizationStepParameter"/> instances
    /// </summary>
    public class OptimizationParameterJsonConverter : JsonConverter
    {
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            JObject jo = new JObject();
            Type type = value.GetType();

            foreach (PropertyInfo prop in type.GetProperties())
            {
                if (prop.CanRead)
                {
                    var attribute = prop.GetCustomAttribute<JsonPropertyAttribute>();
                    object propVal = prop.GetValue(value, null);
                    if (propVal != null)
                    {
                        jo.Add(attribute.PropertyName ?? prop.Name, JToken.FromObject(propVal, serializer));
                    }
                }
            }
            jo.WriteTo(writer);
        }

        public override object ReadJson(
            JsonReader reader,
            Type objectType,
            object existingValue,
            JsonSerializer serializer
            )
        {
            JObject token = JObject.Load(reader);
            var parameterName = token.GetValue("name", StringComparison.OrdinalIgnoreCase)?.Value<string>();
            if (string.IsNullOrEmpty(parameterName))
            {
                throw new ArgumentException("Optimization parameter name is not specified.");
            }

            JToken value;
            JToken minToken;
            JToken maxToken;
            OptimizationParameter optimizationParameter = null;
            if (token.TryGetValue("value", StringComparison.OrdinalIgnoreCase, out value))
            {
                optimizationParameter = new StaticOptimizationParameter(parameterName, value.Value<string>());
            }
            else if (token.TryGetValue("min", StringComparison.OrdinalIgnoreCase, out minToken) &&
                token.TryGetValue("max", StringComparison.OrdinalIgnoreCase, out maxToken))
            {
                var stepToken = token.GetValue("step", StringComparison.OrdinalIgnoreCase)?.Value<decimal>();
                var minStepToken = token.GetValue("min-step", StringComparison.OrdinalIgnoreCase)?.Value<decimal>();
                if (stepToken.HasValue)
                {
                    if (minStepToken.HasValue)
                    {
                        optimizationParameter = new OptimizationStepParameter(parameterName,
                            minToken.Value<decimal>(),
                            maxToken.Value<decimal>(),
                            stepToken.Value,
                            minStepToken.Value);
                    }
                    else
                    {
                        optimizationParameter = new OptimizationStepParameter(parameterName,
                            minToken.Value<decimal>(),
                            maxToken.Value<decimal>(),
                            stepToken.Value);
                    }
                }
                else
                {
                    optimizationParameter = new OptimizationStepParameter(parameterName,
                        minToken.Value<decimal>(),
                        maxToken.Value<decimal>());
                }
            }

            if (optimizationParameter == null)
            {
                throw new ArgumentException(
                    "Optimization parameter are not currently supported.");
            }

            return optimizationParameter;
        }

        public override bool CanConvert(Type objectType) => typeof(OptimizationParameter).IsAssignableFrom(objectType);
    }
}
