using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;


namespace SqCommon
{
    public class DoubleJsonConverterToStr : JsonConverter<double>   // the number is written as a string, with quotes: "previousClose":"272.48"
    {
        public override double Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
                Double.Parse(reader.GetString());

        public override void Write(Utf8JsonWriter writer, double doubleValue, JsonSerializerOptions options) =>
                writer.WriteStringValue(doubleValue.ToString("0.####")); // use 4 decimals instead of 2, because of penny stocks and MaxDD of "-0.2855" means -28.55%. ToString(): 24.00155 is rounded up to 24.0016
    }

    public class DoubleJsonConverterToNumber4D : JsonConverter<double>   // the number is written as a number, without quotes: ("previousClose":"272.4800109863281") should be ("previousClose":272.48) 
    {
        public override double Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
                Double.Parse(reader.GetString());

        public override void Write(Utf8JsonWriter writer, double doubleValue, JsonSerializerOptions options)
        {
            // https://github.com/dotnet/runtime/issues/31024
            // ArgumentException' in System.Text.Json.dll: '.NET number values such as positive and negative infinity cannot be written as valid JSON.'
            // This is a more complicated scenario as some floating-point values (+infinity, -infinity, and NaN) can't be represented as JSON "numbers".
            // The only way these can be successfully serialized/deserialized is by representing them by using an alternative format (such as strings).
            // Writes ["Infinity","NaN",0.1,1.0002,3.141592653589793]
            // And the following succeeds: JsonConvert.DeserializeObject<double[]>("[\"Infinity\",\"NaN\",0.1,1.0002,3.141592653589793]");
            if (double.IsFinite(doubleValue))
            {
                writer.WriteNumberValue(Convert.ToDecimal(Math.Round(doubleValue, 4))); // use 4 decimals instead of 2, because of penny stocks and MaxDD of "-0.2855" means -28.55%. ToString(): 24.00155 is rounded up to 24.0016
            }
            else
            {
                writer.WriteStringValue(doubleValue.ToString());
            }
        }
    }


    public static partial class Utils
    {
        static JsonSerializerOptions g_camelJsonSerializeOpt = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        };

        // in Javascript, class field members should use CamelCase: https://www.robinwieruch.de/javascript-naming-conventions  "AnyParam" turns to "anyParam"
        public static string CamelCaseSerialize<TValue>(TValue obj)
        {
            return JsonSerializer.Serialize(obj, g_camelJsonSerializeOpt);
        }
    }

}