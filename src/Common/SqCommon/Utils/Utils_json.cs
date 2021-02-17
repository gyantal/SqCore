using System;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;


namespace SqCommon
{
    public class DoubleJsonConverterToStr : JsonConverter<double>   // the number is written as a string, with quotes: "previousClose":"272.48"
    {
        public override double Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
                Double.Parse(reader.GetString() ?? String.Empty);

        public override void Write(Utf8JsonWriter writer, double doubleValue, JsonSerializerOptions options) =>
                writer.WriteStringValue(doubleValue.ToString("0.####")); // use 4 decimals instead of 2, because of penny stocks and MaxDD of "-0.2855" means -28.55%. ToString(): 24.00155 is rounded up to 24.0016
    }

    public class DoubleJsonConverterToNumber4D : JsonConverter<double>   // the number is written as a number, without quotes: ("previousClose":"272.4800109863281") should be ("previousClose":272.48) 
    {
        public override double Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
                Double.Parse(reader.GetString() ?? String.Empty);

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

        public static T? LoadFromJSON<T>(string p_str)
        {
            try
            {
                //don't use FileStream directly to serializer
                //using (FileStream stream = File.OpenRead(filePath))
                // Encountered unexpected character 'ï', because
                // "Please note that DataContractJsonSerializer only supports the following encodings: UTF-8"
                // see http://blogs.msdn.com/b/cie/archive/2014/03/19/encountered-unexpected-character-239-error-serializing-json.aspx

                //string p_str = System.IO.File.ReadAllText(p_filePath);
                MemoryStream ms = new MemoryStream(Encoding.UTF8.GetBytes(p_str));
                DataContractJsonSerializerSettings settings = new DataContractJsonSerializerSettings() { DateTimeFormat = new DateTimeFormat("yyyy-MM-dd'T'HH:mm:ssZ") };    // the 'T' is used by Javascript in HealthMonitor website. 'Z' shows that it is UTC (Zero TimeZone).  That is the reason of the format.
                settings.UseSimpleDictionaryFormat = true;
                DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(T), settings);
                T? contract = (T?)serializer.ReadObject(ms);
                return contract;
                //}
            }
            catch
            {
                Utils.Logger.Info("LoadFromJSON(): Cannot deserialize json " + p_str);      // Not even a warning. It is quite expected that sometimes, Json serialization fails. The caller will handle rethrown exception.
                throw;
            }
        }

        public static string SaveToJSON<T>(T p_obj)
        {
            try
            {
                DataContractJsonSerializerSettings settings = new DataContractJsonSerializerSettings() { DateTimeFormat = new DateTimeFormat("yyyy-MM-dd'T'HH:mm:ssZ") };
                settings.UseSimpleDictionaryFormat = true;
                DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(T), settings);

                using (MemoryStream ms = new MemoryStream())
                {
                    serializer.WriteObject(ms, p_obj);
                    ms.Position = 0;
                    StreamReader sr = new StreamReader(ms);
                    return sr.ReadToEnd();

//                    return Encoding.Unicode.GetString(ms.ToArray());  //UTF-16 is used for in-memory strings because it is faster per character to parse and maps directly to unicode character class and other tables. All string functions in Windows use UTF-16 and have for years.
                }
            }
            catch (System.Exception ex)
            {
                Utils.Logger.Info(ex, "Cannot serialize object " + p_obj!.ToString());
                throw;
            }
        }
    }

}